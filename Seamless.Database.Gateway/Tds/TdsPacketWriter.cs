using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Tds;

public sealed partial class TdsPacketWriter{
    private const int PacketSize=4096;
    private readonly PipeWriter writer;
    private byte packetId=1;

    public TdsPacketWriter(PipeWriter writer){
        this.writer=writer;
    }

    public async ValueTask SendPreLoginResponseAsync(CancellationToken cancellationToken=default){
        // PreLogin オプション構成:
        // Version (Token: 0x00, Offset: 21, Length: 6) -> 16.0.1000.6
        // Encryption (Token: 0x01, Offset: 27, Length: 1) -> 0x02 (Not supported / Encrypt off)
        // InstOpt (Token: 0x02, Offset: 28, Length: 1) -> 0x00
        // ThreadId (Token: 0x03, Offset: 29, Length: 4) -> 0x00000000
        // Mars (Token: 0x04, Offset: 33, Length: 1) -> 0x00 (Mars off)
        // Terminator (0xFF)
        using var stream=new MemoryStream();

        // オプショントークンヘッダ (各5バイト: token 1B, offset 2B BE, length 2B BE)
        WriteOptionHeader(stream,0x00,26,6); // Version
        WriteOptionHeader(stream,0x01,32,1); // Encryption
        WriteOptionHeader(stream,0x02,33,1); // InstOpt
        WriteOptionHeader(stream,0x03,34,4); // ThreadId
        WriteOptionHeader(stream,0x04,38,1); // Mars
        stream.WriteByte(0xFF);              // Terminator

        // ペイロードデータ
        // Version: 16.0.1000, sub-build 6
        stream.WriteByte(0x10); stream.WriteByte(0x00);
        stream.WriteByte(0x03); stream.WriteByte(0xE8);
        stream.WriteByte(0x00); stream.WriteByte(0x06);

        // Encryption: 0x02 (Certificate Not Supported)
        stream.WriteByte(0x02);

        // InstOpt: 0x00
        stream.WriteByte(0x00);

        // ThreadId: 0
        stream.WriteByte(0x00); stream.WriteByte(0x00); stream.WriteByte(0x00); stream.WriteByte(0x00);

        // Mars: 0x00
        stream.WriteByte(0x00);

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public async ValueTask SendLoginAckAsync(string databaseName,CancellationToken cancellationToken=default,string? collation=null){
        using var stream=new MemoryStream();

        // 1. LoginAck Token (0xAD)
        stream.WriteByte(TdsConstants.TokenLoginAck);
        var ackPayload=new MemoryStream();
        ackPayload.WriteByte(0x01); // Interface: SQL_DFLT
        WriteInt32BE(ackPayload,(int)TdsConstants.TdsVersion74); // TDS Version 7.4
        WriteStringWithLength8(ackPayload,"SeamlessDB"); // B_VARCHAR: 1 byte char count + UTF-16LE
        ackPayload.WriteByte(16); // Major: 16
        ackPayload.WriteByte(0);  // Minor: 0
        WriteUInt16BE(ackPayload,1000); // Build: 1000

        var ackBytes=ackPayload.ToArray();
        WriteUInt16LE(stream,(ushort)ackBytes.Length);
        stream.Write(ackBytes);

        // 2. EnvChange: Database changed
        WriteEnvChange(stream,TdsConstants.EnvChangeDatabase,databaseName,"master");
        var packetSize=PacketSize.ToString(global::System.Globalization.CultureInfo.InvariantCulture);WriteEnvChange(stream,TdsConstants.EnvChangePacketSize,packetSize,packetSize);

        // RPCの文字列パラメーターは接続既定collationを参照する（MS-TDS ENVCHANGE type 7）。
        WriteCollationChange(stream,collation,null);

        // 3. Done Token (0xFD)
        WriteDone(stream,TdsConstants.DoneFinal,0);

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public async ValueTask SendQueryResultAsync(QueryResult result,CancellationToken cancellationToken=default){
        await this.SendQueryResultsAsync(new[]{result},cancellationToken);
    }

    public async ValueTask SendQueryResultsAsync(IReadOnlyList<QueryResult> results,CancellationToken cancellationToken=default,bool rpc=false,IReadOnlyList<TdsRpcParameter>? output=null,Func<CancellationToken,ValueTask>? observe=null,CancellationToken transportCancellation=default){
        using var token=new MemoryStream();
        for(var index=0;index<results.Count;index++){
            var result=results[index];long count=0;
            _=result.Columns;if(observe is not null){await observe(cancellationToken);}
            if(result.Information is{}information){WriteInformation(token,information);}
            if(result.Columns.Count>0){
                WriteMetadata(token,result.Columns);
                await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),false,transportCancellation);token.SetLength(0);
                var chunks=new List<(int Start,int Length)>();
                foreach(var row in result.Rows){
                    cancellationToken.ThrowIfCancellationRequested();
                    if(row.Length!=result.Columns.Count){throw new InvalidDataException("A result row does not match its column metadata.");}
                    long rowBytes=32;foreach(var cell in row){if(cell is string text){rowBytes=checked(rowBytes+global::System.Text.Encoding.Unicode.GetByteCount(text));}else if(cell is byte[] bytes){rowBytes=checked(rowBytes+bytes.Length);}else{rowBytes+=32;}}
                    if(rowBytes>16L*1024*1024){throw new InvalidDataException("A TDS row exceeds the configured 16 MiB transmission budget.");}
                    chunks.Clear();token.WriteByte(TdsConstants.TokenRow);
                    for(var i=0;i<row.Length;i++){WriteValue(token,result.Columns[i],row[i],chunks);}
                    var rowData=token.GetBuffer().AsMemory(0,checked((int)token.Length));var position=0;
                    foreach(var chunk in chunks){
                        if(chunk.Start>position){await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,rowData.Slice(position,chunk.Start-position),false,transportCancellation);}
                        await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,rowData.Slice(chunk.Start,chunk.Length),false,transportCancellation);position=chunk.Start+chunk.Length;
                    }
                    if(position<rowData.Length){await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,rowData[position..],false,transportCancellation);}
                    token.SetLength(0);count++;
                }
            }else{count=result.RowsAffected;}
            ushort status=0;if(result.Information is null){status=TdsConstants.DoneCount;}if(index+1<results.Count){status|=TdsConstants.DoneMore;}
            if(rpc){status|=TdsConstants.DoneMore;}
            var doneOffset=(int)token.Position;WriteDone(token,status,count);if(rpc){token.GetBuffer()[doneOffset]=TdsConstants.TokenDoneInProc;}
            await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),!rpc&&index+1==results.Count,transportCancellation);token.SetLength(0);
        }
        if(rpc){
            token.WriteByte(0x79);WriteInt32LE(token,0);
            if(output is not null){
                ushort ordinal=0;
                foreach(var value in output){
                    token.WriteByte(0xAC);WriteUInt16LE(token,ordinal++);WriteStringWithLength8(token,value.Name);token.WriteByte(1);WriteUInt32LE(token,0);WriteUInt16LE(token,0);token.WriteByte(0x26);token.WriteByte(4);token.WriteByte(4);WriteInt32LE(token,Convert.ToInt32(value.Value));
                }
            }
            var start=token.Position;WriteDone(token,TdsConstants.DoneFinal,0);token.GetBuffer()[(int)start]=TdsConstants.TokenDoneProc;
            await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),true,transportCancellation);
        }
    }
    public async ValueTask SendDatabaseChangeAsync(string database,string previous,CancellationToken cancellationToken=default,string? collation=null,string? previousCollation=null){
        using var token=new MemoryStream();WriteEnvChange(token,TdsConstants.EnvChangeDatabase,database,previous);
        if(ResolveCollation(collation)!=ResolveCollation(previousCollation)){WriteCollationChange(token,collation,previousCollation,true);}
        await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),false,cancellationToken);
    }
    public async ValueTask SendTransactionChangeAsync(byte change,ulong descriptor,CancellationToken cancellationToken=default){
        using var token=new MemoryStream();token.WriteByte(TdsConstants.TokenEnvChange);WriteUInt16LE(token,11);token.WriteByte(change);
        if(change==8){token.WriteByte(8);WriteInt64LE(token,checked((long)descriptor));token.WriteByte(0);}
        else{token.WriteByte(0);token.WriteByte(8);WriteInt64LE(token,checked((long)descriptor));}
        await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),false,cancellationToken);
    }
    public async ValueTask SendAttentionAckAsync(CancellationToken cancellationToken=default){
        using var token=new MemoryStream();WriteDone(token,0x20,0);await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),cancellationToken);
    }
    public async ValueTask SendErrorAsync(string message,CancellationToken cancellationToken=default,int number=50000,int line=1){
        using var stream=new MemoryStream();

        stream.WriteByte(TdsConstants.TokenError);
        var errPayload=new MemoryStream();
        WriteInt32LE(errPayload,number); // Error Number
        errPayload.WriteByte(1);        // State
        errPayload.WriteByte(16);       // Class (Severity)
        WriteStringWithLength(errPayload,message);   // US_VARCHAR: USHORT char count + UTF-16LE
        WriteStringWithLength8(errPayload,"SeamlessDB"); // B_VARCHAR: BYTE char count + UTF-16LE
        WriteStringWithLength8(errPayload,"");           // B_VARCHAR: BYTE char count + UTF-16LE
        WriteInt32LE(errPayload,Math.Max(1,line)); // LineNumber (LONG in TDS 7.2+)

        var errBytes=errPayload.ToArray();
        WriteUInt16LE(stream,(ushort)errBytes.Length);
        stream.Write(errBytes);

        WriteDone(stream,TdsConstants.DoneFinal|TdsConstants.DoneError,0);
        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    private ValueTask WritePacketAsync(byte packetType,byte[]payload,CancellationToken cancellationToken)=>this.WriteFragmentAsync(packetType,payload,true,cancellationToken);
    private async ValueTask WriteFragmentAsync(byte packetType,ReadOnlyMemory<byte> payload,bool end,CancellationToken cancellationToken){
        var offset=0;
        do{
            var size=Math.Min(PacketSize-8,payload.Length-offset);var header=new byte[8];header[0]=packetType;
            if(end&&offset+size==payload.Length){header[1]=TdsConstants.StatusEndOfMessage;}
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2,2),checked((ushort)(size+8)));header[6]=this.packetId++;
            // 途中取消でも宣言したpacket長を必ず満たす。token全体は呼出し側のtransport tokenで送る。
            var packet=this.writer.GetMemory(8+size);header.CopyTo(packet);if(size>0){payload.Slice(offset,size).CopyTo(packet[8..]);}this.writer.Advance(8+size);
            var flushed=await this.writer.FlushAsync(cancellationToken);if(flushed.IsCanceled){throw new OperationCanceledException(cancellationToken);}if(flushed.IsCompleted){throw new IOException("The client disconnected.");}
            offset+=size;
        }while(offset<payload.Length);
        if(end){this.packetId=1;}
    }
    private static void WriteOptionHeader(Stream stream,byte token,ushort offset,ushort length){
        stream.WriteByte(token);
        var buf=new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf,offset);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt16BigEndian(buf,length);
        stream.Write(buf);
    }

    private static void WriteCollationChange(Stream stream,string? current,string? previous,bool includePrevious=false){
        var value=ResolveCollation(current);stream.WriteByte(TdsConstants.TokenEnvChange);
        if(!includePrevious){WriteUInt16LE(stream,8);}else{WriteUInt16LE(stream,13);}stream.WriteByte(7);stream.WriteByte(5);WriteUInt32LE(stream,value.Info);stream.WriteByte(value.SortId);
        if(!includePrevious){stream.WriteByte(0);}else{var old=ResolveCollation(previous);stream.WriteByte(5);WriteUInt32LE(stream,old.Info);stream.WriteByte(old.SortId);}
    }
    private static void WriteEnvChange(Stream stream,byte type,string newVal,string oldVal){
        stream.WriteByte(TdsConstants.TokenEnvChange);
        var payload=new MemoryStream();
        payload.WriteByte(type);
        WriteStringWithLength8(payload,newVal);
        WriteStringWithLength8(payload,oldVal);

        var bytes=payload.ToArray();
        WriteUInt16LE(stream,(ushort)bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteInformation(Stream stream,string message){
        using var payload=new MemoryStream();WriteInt32LE(payload,0);payload.WriteByte(1);payload.WriteByte(0);
        WriteStringWithLength(payload,message);WriteStringWithLength8(payload,"SeamlessDB");WriteStringWithLength8(payload,"");WriteInt32LE(payload,0);
        stream.WriteByte(TdsConstants.TokenInfo);WriteUInt16LE(stream,checked((ushort)payload.Length));payload.Position=0;payload.CopyTo(stream);
    }

    private static void WriteDone(Stream stream,ushort status,long rowCount){
        stream.WriteByte(TdsConstants.TokenDone);
        WriteUInt16LE(stream,status);
        WriteUInt16LE(stream,0); // CurCmd
        WriteInt64LE(stream,rowCount);
    }

    private static void WriteStringWithLength(Stream stream,string text){
        WriteUInt16LE(stream,(ushort)text.Length);
        stream.Write(Encoding.Unicode.GetBytes(text));
    }

    private static void WriteStringWithLength8(Stream stream,string text){
        stream.WriteByte((byte)text.Length);
        stream.Write(Encoding.Unicode.GetBytes(text));
    }

    private static void WriteUInt16LE(Stream stream,ushort value){
        var buf=new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt16LE(Stream stream,short value){
        var buf=new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt32LE(Stream stream,int value){
        var buf=new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteUInt16BE(Stream stream,ushort value){
        var buf=new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteUInt32LE(Stream stream,uint value){
        var buf=new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt32BE(Stream stream,int value){
        var buf=new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt64LE(Stream stream,long value){
        var buf=new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf,value);
        stream.Write(buf);
    }
}
