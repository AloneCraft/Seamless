using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Tds;

public sealed class TdsSession{
    private readonly IDatabaseAdapter adapter;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly SessionContext sessionContext;
    private bool isAuthenticated;

    public TdsSession(long sessionId,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine){
        this.adapter=adapter;
        this.sqlEngine=sqlEngine;
        this.sessionContext=new SessionContext(sessionId,adapter.DatabaseName);
    }

    public async Task ProcessConnectionAsync(PipeReader reader,PipeWriter writer,CancellationToken cancellationToken=default){
        var packetWriter=new TdsPacketWriter(writer);

        try{
            while(!cancellationToken.IsCancellationRequested){
                var message=await TdsPacketReader.ReadMessageAsync(reader,cancellationToken);
                if(message is null){
                    break;
                }else{
                }

                var msg=message.Value;
                if(msg.PacketType==TdsConstants.PacketTypePreLogin){
                    await packetWriter.SendPreLoginResponseAsync(cancellationToken);
                }else if(!this.isAuthenticated){
                    // ログインハンドシェイク (PreTds7Login 0x02 または最初の認証パケット)
                    this.isAuthenticated=true;
                    await packetWriter.SendLoginAckAsync(this.adapter.DatabaseName,cancellationToken);
                }else if(msg.PacketType==TdsConstants.PacketTypeSqlBatch){
                    var sql=ExtractSqlFromBatch(msg.Payload);
                    try{
                        var result=this.sqlEngine.Execute(sql,this.sessionContext);
                        await packetWriter.SendQueryResultAsync(result,cancellationToken);
                    }catch(Exception ex){
                        await packetWriter.SendErrorAsync(ex.Message,cancellationToken);
                    }
                }else{
                    // 未知または未サポートのパケット（Attention 等）
                    await packetWriter.SendQueryResultAsync(QueryResult.NonQuery(0),cancellationToken);
                }
            }
        }finally{
            this.sessionContext.Dispose();
        }
    }

    private static string ExtractSqlFromBatch(byte[]payload){
        if(payload.Length<2){
            return string.Empty;
        }else{
        }

        // TDS 7.2+ の ALL_HEADERS をチェック
        var offset=0;
        if(payload.Length>=4){
            var totalLength=BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0,4));
            if(totalLength>0&&totalLength<=payload.Length&&totalLength<=1024){
                // ALL_HEADERS が存在するのでスキップ
                offset=totalLength;
            }else{
            }
        }else{
        }

        var sqlBytes=payload.AsSpan(offset);
        if(sqlBytes.Length==0){
            return string.Empty;
        }else{
            return Encoding.Unicode.GetString(sqlBytes);
        }
    }
}
