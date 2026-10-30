using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
namespace Seamless.Transactions;
public static class WalFormat{
    public const int HeaderBytes=32;
    public const uint Magic=0x32465753;
    public static long EntryPayloadBytes(string codec,int keyBytes,int rowBytes)=>checked(21L+Encoding.UTF8.GetByteCount(codec)+keyBytes+rowBytes);
    public static byte[] Encode(WalTransaction transaction,int maximum=4<<20){
        if(transaction.Lsn<=0||transaction.Draft.Id==Guid.Empty){throw new InvalidDataException("Invalid WAL identity.");}
        var parts=transaction.Draft.Partitions;
        if(parts.Length==0||parts.Any(static p=>p<0)||!parts.SequenceEqual(parts.Distinct().Order())){throw new InvalidDataException("Partitions must be sorted and unique.");}
        long length=HeaderBytes+16L+4+4L*parts.Length+4+transaction.Draft.Metadata.Length+4;
        foreach(var entry in transaction.Draft.Entries){
            ValidateEntry(entry,parts);
            length=checked(length+EntryPayloadBytes(entry.Codec,entry.Key.Length,entry.Row?.Length??0));
            if(length>maximum){throw new InvalidDataException("WAL frame exceeds the configured bound.");}
        }
        if(length>maximum){throw new InvalidDataException("WAL frame exceeds the configured bound.");}
        using var stream=new MemoryStream((int)length-HeaderBytes);
        using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
        writer.Write(transaction.Draft.Id.ToByteArray());
        writer.Write(parts.Length);
        foreach(var part in parts){writer.Write(part);}
        WriteBytes(writer,transaction.Draft.Metadata,maximum);
        writer.Write(transaction.Draft.Entries.Count);
        foreach(var entry in transaction.Draft.Entries){
            ValidateEntry(entry,parts);
            writer.Write(entry.Partition);writer.Write(entry.TableId);writer.Write((byte)entry.Mutation);
            WriteBytes(writer,Encoding.UTF8.GetBytes(entry.Codec),maximum);WriteBytes(writer,entry.Key,maximum);
            if(entry.Row is null){writer.Write(-1);}else{WriteBytes(writer,entry.Row,maximum);}
            if(stream.Length>maximum-HeaderBytes){throw new InvalidDataException("WAL frame exceeds the configured bound.");}
        }
        if(stream.Length>maximum-HeaderBytes){throw new InvalidDataException("WAL frame exceeds the configured bound.");}
        return Frame(1,transaction.Lsn,stream.ToArray());
    }
    private static void ValidateEntry(WalEntry entry,int[] parts){
        if(!parts.Contains(entry.Partition)||entry.TableId<=0||string.IsNullOrEmpty(entry.Codec)||entry.Key.Length==0||!Enum.IsDefined(entry.Mutation)||(entry.Mutation==WalMutation.Delete)!=(entry.Row is null)){
            throw new InvalidDataException("Invalid WAL entry contract.");
        }
    }
    private static void WriteBytes(BinaryWriter writer,byte[] bytes,int maximum){
        if(bytes.Length>maximum){throw new InvalidDataException("WAL field exceeds the configured bound.");}
        writer.Write(bytes.Length);writer.Write(bytes);
    }
    public static byte[] Pad(int length){
        if(length<HeaderBytes){throw new InvalidDataException("PAD is shorter than its header.");}
        return Frame(2,0,new byte[length-HeaderBytes]);
    }
    private static byte[] Frame(int type,long lsn,byte[] body){
        var result=new byte[checked(HeaderBytes+body.Length)];
        var span=result.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span,Magic);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..],type);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..],result.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span[12..],body.Length);
        BinaryPrimitives.WriteInt64LittleEndian(span[16..],lsn);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..],Crc32.HashToUInt32(body));
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..],Crc32.HashToUInt32(span[..28]));
        body.CopyTo(span[HeaderBytes..]);return result;
    }
    public static (int Type,int Length,long Lsn) ReadHeader(ReadOnlySpan<byte> header,int maximum){
        if(header.Length<HeaderBytes||BinaryPrimitives.ReadUInt32LittleEndian(header)!=Magic||BinaryPrimitives.ReadUInt32LittleEndian(header[28..])!=Crc32.HashToUInt32(header[..28])){throw new InvalidDataException("Invalid WAL header/CRC.");}
        var type=BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        var length=BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
        var body=BinaryPrimitives.ReadInt32LittleEndian(header[12..]);
        var lsn=BinaryPrimitives.ReadInt64LittleEndian(header[16..]);
        if(type is not (1 or 2)||length<HeaderBytes||length>maximum||body!=length-HeaderBytes||(type==1&&lsn<=0)||(type==2&&lsn!=0)){throw new InvalidDataException("Invalid WAL frame length/type/LSN.");}
        return(type,length,lsn);
    }
    public static WalTransaction Decode(ReadOnlySpan<byte> frame,int maximum=4<<20){
        var header=ReadHeader(frame,maximum);
        if(header.Type!=1||frame.Length!=header.Length){throw new InvalidDataException("Expected one complete transaction frame.");}
        CheckBody(frame);
        var body=frame[HeaderBytes..];var offset=0;
        var id=new Guid(Take(body,ref offset,16));
        var count=ReadInt(body,ref offset);
        if(count<=0||count>(body.Length-offset)/4){throw new InvalidDataException("Invalid partition count.");}
        var parts=new int[count];
        for(var index=0;index<count;index++){parts[index]=ReadInt(body,ref offset);}
        if(id==Guid.Empty||parts.Any(static p=>p<0)||!parts.SequenceEqual(parts.Distinct().Order())){throw new InvalidDataException("Invalid WAL identity/partitions.");}
        var metadata=ReadBytes(body,ref offset);
        count=ReadInt(body,ref offset);
        if(count<0||count>(body.Length-offset)/21){throw new InvalidDataException("Invalid entry count.");}
        var entries=new List<WalEntry>(count);
        for(var index=0;index<count;index++){
            var part=ReadInt(body,ref offset);var table=ReadInt(body,ref offset);var kind=(WalMutation)Take(body,ref offset,1)[0];
            var codec=new UTF8Encoding(false,true).GetString(ReadBytes(body,ref offset));var key=ReadBytes(body,ref offset);
            var rowLength=ReadInt(body,ref offset);byte[]? row=null;
            if(rowLength!=-1){row=Take(body,ref offset,rowLength).ToArray();}
            var entry=new WalEntry(part,table,kind,codec,key,row);ValidateEntry(entry,parts);entries.Add(entry);
        }
        if(offset!=body.Length){throw new InvalidDataException("Trailing WAL bytes.");}
        return new(header.Lsn,new(id,parts,entries,metadata));
    }
    public static void CheckBody(ReadOnlySpan<byte> frame){
        if(BinaryPrimitives.ReadUInt32LittleEndian(frame[24..])!=Crc32.HashToUInt32(frame[HeaderBytes..])){throw new InvalidDataException("WAL body CRC mismatch.");}
        if(BinaryPrimitives.ReadInt32LittleEndian(frame[4..])==2&&frame[HeaderBytes..].IndexOfAnyExcept((byte)0)>=0){throw new InvalidDataException("Nonzero PAD body.");}
    }
    private static int ReadInt(ReadOnlySpan<byte> body,ref int offset)=>BinaryPrimitives.ReadInt32LittleEndian(Take(body,ref offset,4));
    private static byte[] ReadBytes(ReadOnlySpan<byte> body,ref int offset)=>Take(body,ref offset,ReadInt(body,ref offset)).ToArray();
    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> body,ref int offset,int length){
        if(length<0||length>body.Length-offset){throw new InvalidDataException("Truncated WAL payload.");}
        var result=body.Slice(offset,length);offset+=length;return result;
    }
}
