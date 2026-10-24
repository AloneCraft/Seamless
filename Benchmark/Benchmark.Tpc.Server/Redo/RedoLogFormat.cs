using System.Buffers.Binary;
using System.IO.Hashing;
using Benchmark.Tpc.Core;
namespace Benchmark.Tpc.Server;
/// <summary>
/// 再実行ログの 1 件の形: [長さ int32][CRC32 uint32][中身]。
/// 中身: 通し番号 int64、書き込みの数 int32、書き込みごとに [表 int32][種類 byte][キー長 int32][キー][行の有無 byte][行長 int32][行]。
/// 末尾が途中で切れた、または CRC が合わない記録は、電源断で書き終えなかったものとして読み捨てる。
/// </summary>
public static class RedoLogFormat{
    public const int HeaderLength=8;
    public static byte[] Encode(RedoTransaction transaction){
        using var payload=new MemoryStream();
        var buffer=new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer,transaction.Sequence);
        payload.Write(buffer,0,8);
        BinaryPrimitives.WriteInt32LittleEndian(buffer,transaction.Entries.Count);
        payload.Write(buffer,0,4);
        foreach(var entry in transaction.Entries){
            BinaryPrimitives.WriteInt32LittleEndian(buffer,entry.TableId);
            payload.Write(buffer,0,4);
            payload.WriteByte((byte)entry.Kind);
            BinaryPrimitives.WriteInt32LittleEndian(buffer,entry.Key.Length);
            payload.Write(buffer,0,4);
            payload.Write(entry.Key);
            if(entry.Row is null){
                payload.WriteByte(0);
            }else{
                payload.WriteByte(1);
                BinaryPrimitives.WriteInt32LittleEndian(buffer,entry.Row.Length);
                payload.Write(buffer,0,4);
                payload.Write(entry.Row);
            }
        }
        var body=payload.ToArray();
        var frame=new byte[HeaderLength+body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame,body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4),Crc32.HashToUInt32(body));
        body.CopyTo(frame,HeaderLength);
        return frame;
    }
    /// <summary>1 件を読む。切れている、または壊れていれば false(ここから後ろは捨てる)。</summary>
    public static bool TryDecode(ReadOnlySpan<byte> data,out RedoTransaction? transaction,out int consumed){
        transaction=null;
        consumed=0;
        if(data.Length<HeaderLength){
            return false;
        }
        var length=BinaryPrimitives.ReadInt32LittleEndian(data);
        if(length<12||data.Length-HeaderLength<length){
            return false;
        }
        var body=data.Slice(HeaderLength,length);
        if(Crc32.HashToUInt32(body)!=BinaryPrimitives.ReadUInt32LittleEndian(data[4..])){
            return false;
        }
        var sequence=BinaryPrimitives.ReadInt64LittleEndian(body);
        var count=BinaryPrimitives.ReadInt32LittleEndian(body[8..]);
        var offset=12;
        var entries=new List<RedoEntry>(count);
        for(var index=0;index<count;index++){
            var tableId=BinaryPrimitives.ReadInt32LittleEndian(body[offset..]);
            offset+=4;
            var kind=(WriteKind)body[offset++];
            var keyLength=BinaryPrimitives.ReadInt32LittleEndian(body[offset..]);
            offset+=4;
            var key=body.Slice(offset,keyLength).ToArray();
            offset+=keyLength;
            byte[]? row=null;
            if(body[offset++]!=0){
                var rowLength=BinaryPrimitives.ReadInt32LittleEndian(body[offset..]);
                offset+=4;
                row=body.Slice(offset,rowLength).ToArray();
                offset+=rowLength;
            }
            entries.Add(new RedoEntry(tableId,kind,key,row));
        }
        transaction=new RedoTransaction(sequence,entries);
        consumed=HeaderLength+length;
        return true;
    }
}
