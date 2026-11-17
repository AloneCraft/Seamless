using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tds;

public sealed partial class TdsPacketWriter{
    private const int PacketSize=4096;
    private readonly PipeWriter writer;
    private readonly TimeSpan? writeTimeout;
    private readonly FrameBufferQuota? pendingWrites;
    private byte packetId=1;

    public TdsPacketWriter(PipeWriter writer):this(writer,null){}
    internal TdsPacketWriter(PipeWriter writer,TimeSpan? writeTimeout,FrameBufferQuota? pendingWrites=null){this.writer=writer;this.writeTimeout=writeTimeout;this.pendingWrites=pendingWrites;}
    internal abstract class WriteAborted(string message,Exception? cause=null):IOException(message,cause);
    internal sealed class WriteDeadlineExceeded(OperationCanceledException cause):WriteAborted("The TDS transport output has timed out.",cause);
    internal sealed class WriteBudgetExceeded():WriteAborted("The pending TDS write limit has been reached.");

    public ValueTask SendPreLoginResponseAsync(CancellationToken cancellationToken=default)=>this.SendPreLoginResponseAsync(false,cancellationToken);
    internal async ValueTask SendPreLoginResponseAsync(bool federated,CancellationToken cancellationToken){
        using var stream=new MemoryStream();

        // オプショントークンヘッダ (各5バイト: token 1B, offset 2B BE, length 2B BE)
        var offset=federated?31:26;
        WriteOptionHeader(stream,0x00,(ushort)offset,6); // Version
        WriteOptionHeader(stream,0x01,(ushort)(offset+6),1); // Encryption
        WriteOptionHeader(stream,0x02,(ushort)(offset+7),1); // InstOpt
        WriteOptionHeader(stream,0x03,(ushort)(offset+8),4); // ThreadId
        WriteOptionHeader(stream,0x04,(ushort)(offset+12),1); // Mars
        if(federated){WriteOptionHeader(stream,0x06,(ushort)(offset+13),1);} // FedAuthRequired
        stream.WriteByte(0xFF);              // Terminator

        // ペイロードデータ
        // Version: 17.0.1000, sub-build 7
        stream.WriteByte(SqlServerVersion.Major); stream.WriteByte(SqlServerVersion.Minor);
        WriteUInt16BE(stream,SqlServerVersion.Build);
        WriteUInt16BE(stream,SqlServerVersion.Revision);

        // 保護入口ではTLSを確立した後にPRELOGINを受ける。
        stream.WriteByte(federated?(byte)0x01:(byte)0x02);

        // InstOpt: 0x00
        stream.WriteByte(0x00);

        // ThreadId: 0
        stream.WriteByte(0x00); stream.WriteByte(0x00); stream.WriteByte(0x00); stream.WriteByte(0x00);

        // Mars: 0x00
        stream.WriteByte(0x00);
        if(federated){stream.WriteByte(0x01);}

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public ValueTask SendLoginAckAsync(string databaseName,CancellationToken cancellationToken=default,string? collation=null)
        =>this.SendLoginAckAsync(databaseName,cancellationToken,collation,false);
    internal async ValueTask SendLoginAckAsync(string databaseName,CancellationToken cancellationToken,string? collation,bool federated){
        using var stream=new MemoryStream();

        // 1. LoginAck Token (0xAD)
        stream.WriteByte(TdsConstants.TokenLoginAck);
        var ackPayload=new MemoryStream();
        ackPayload.WriteByte(0x01); // Interface: SQL_DFLT
        WriteInt32BE(ackPayload,(int)TdsConstants.TdsVersion74); // TDS Version 7.4
        WriteStringWithLength8(ackPayload,"SeamlessDB"); // B_VARCHAR: 1 byte char count + UTF-16LE
        ackPayload.WriteByte(SqlServerVersion.Major);
        ackPayload.WriteByte(SqlServerVersion.Minor);
        WriteUInt16BE(ackPayload,SqlServerVersion.Build);

        var ackBytes=ackPayload.ToArray();
        WriteUInt16LE(stream,(ushort)ackBytes.Length);
        stream.Write(ackBytes);

        // 2. EnvChange: Database changed
        WriteEnvChange(stream,TdsConstants.EnvChangeDatabase,databaseName,"master");
        var packetSize=PacketSize.ToString(global::System.Globalization.CultureInfo.InvariantCulture);WriteEnvChange(stream,TdsConstants.EnvChangePacketSize,packetSize,packetSize);

        // RPCの文字列パラメーターは接続既定collationを参照する（MS-TDS ENVCHANGE type 7）。
        WriteCollationChange(stream,collation,null);
        if(federated){
            // Security TokenでクライアントのPRELOGINにnonceがない場合のFEATUREEXTACK。
            stream.WriteByte(0xae);
            stream.WriteByte(0x02);
            WriteInt32LE(stream,0);
            stream.WriteByte(0xff);
        }

        // 3. Done Token (0xFD)
        WriteDone(stream,TdsConstants.DoneFinal,0);

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public async ValueTask SendQueryResultAsync(QueryResult result,CancellationToken cancellationToken=default){
        await this.SendQueryResultsAsync(new[]{result},cancellationToken);
    }

    public async ValueTask SendQueryResultsAsync(IReadOnlyList<QueryResult> results,CancellationToken cancellationToken=default,bool rpc=false,IReadOnlyList<TdsRpcParameter>? output=null,Func<CancellationToken,ValueTask>? observe=null,CancellationToken transportCancellation=default){
        using var token=new MemoryStream();
        var combineDoneTokens=results.Any(static result=>result.StatementKind==SqlCommandKind.Select);
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
                    ValidateRow(row,result.Columns.Count);
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
            ushort command=result.StatementKind switch{
                SqlCommandKind.Select=>TdsConstants.SelectCommand,
                SqlCommandKind.Insert=>TdsConstants.InsertCommand,
                SqlCommandKind.Delete=>TdsConstants.DeleteCommand,
                SqlCommandKind.Update=>TdsConstants.UpdateCommand,
                _=>0
            };
            var doneOffset=(int)token.Position;WriteDone(token,status,count,command);if(rpc){token.GetBuffer()[doneOffset]=TdsConstants.TokenDoneInProc;}
            // 混在プロシージャでは、中間の件数を次のメタデータまたはRPC終端と同じ断片へ送る。
            if(combineDoneTokens&&(rpc||index+1<results.Count)){continue;}
            await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),!rpc&&index+1==results.Count,transportCancellation);token.SetLength(0);
        }
        if(rpc){
            token.WriteByte(0x79);WriteInt32LE(token,0);
            if(output is not null){
                for(var index=0;index<output.Count;index++){
                    var value=output[index];
                    var type=value.Type;
                    var column=new ColumnMetadata(value.Name,type.ClrType,type.Name,true,false,0,type.Length,type.Precision,type.Scale,type.Collation);
                    token.WriteByte(0xAC);WriteUInt16LE(token,checked((ushort)(value.Ordinal??index)));WriteStringWithLength8(token,value.Name);token.WriteByte(1);WriteUInt32LE(token,0);WriteUInt16LE(token,0);
                    WriteTypeInfo(token,column);
                    WriteValue(token,column,value.Value,[]);
                }
            }
            var start=token.Position;WriteDone(token,TdsConstants.DoneFinal,0);token.GetBuffer()[(int)start]=TdsConstants.TokenDoneProc;
            await this.WriteFragmentAsync(TdsConstants.PacketTypeTabularResult,token.ToArray(),true,transportCancellation);
        }
    }
    internal static void ValidateRow(object?[] row,int columnCount){
        if(row.Length!=columnCount){throw new InvalidDataException("A result row does not match its column metadata.");}
        long rowBytes=32;
        foreach(var cell in row){
            rowBytes=checked(rowBytes+(cell switch{string text=>global::System.Text.Encoding.Unicode.GetByteCount(text),byte[] bytes=>bytes.Length,_=>32}));
        }
        if(rowBytes>16L*1024*1024){throw new InvalidDataException("A TDS row exceeds the configured 16 MiB transmission budget.");}
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
            var size=Math.Min(PacketSize-8,payload.Length-offset);
            using var reservation=this.pendingWrites?.TryReserve(size+8);
            if(this.pendingWrites is not null&&reservation is null){throw new WriteBudgetExceeded();}
            var header=new byte[8];header[0]=packetType;
            if(end&&offset+size==payload.Length){header[1]=TdsConstants.StatusEndOfMessage;}
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2,2),checked((ushort)(size+8)));header[6]=this.packetId++;
            // 途中取消でも宣言したpacket長を必ず満たす。token全体は呼出し側のtransport tokenで送る。
            var packet=this.writer.GetMemory(8+size);header.CopyTo(packet);if(size>0){payload.Slice(offset,size).CopyTo(packet[8..]);}this.writer.Advance(8+size);
            using var deadline=this.writeTimeout.HasValue?CancellationTokenSource.CreateLinkedTokenSource(cancellationToken):null;
            if(this.writeTimeout is{} timeout){deadline!.CancelAfter(timeout);}
            try{
                var token=deadline?.Token??cancellationToken;
                var flushed=await this.writer.FlushAsync(token);
                if(flushed.IsCanceled){throw new OperationCanceledException(token);}
                if(flushed.IsCompleted){throw new IOException("The client disconnected.");}
            }catch(OperationCanceledException error)when(deadline?.IsCancellationRequested==true&&!cancellationToken.IsCancellationRequested){
                throw new WriteDeadlineExceeded(error);
            }
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

    private static void WriteDone(Stream stream,ushort status,long rowCount,ushort command=0){
        stream.WriteByte(TdsConstants.TokenDone);
        WriteUInt16LE(stream,status);
        WriteUInt16LE(stream,command);
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
