using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tds;

public sealed class TdsSession{
    private readonly SqlExecutionEngine sqlEngine;
    private readonly SessionContext sessionContext;
    private readonly Dictionary<int,(string Sql,string Database,IReadOnlyList<SqlParameter> Parameters)> prepared=new();
    private CancellationTokenSource? activeRequest;
    private int nextHandle;
    private ulong transactionDescriptor;
    private bool isAuthenticated;
    private string loginDatabase="master";
    public TdsSession(long sessionId,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine){this.sqlEngine=sqlEngine;this.sessionContext=new(sessionId,adapter.DatabaseName);}
    public async Task ProcessConnectionAsync(PipeReader reader,PipeWriter writer,CancellationToken cancellationToken=default){
        var packetWriter=new TdsPacketWriter(writer);
        using var stopped=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel=Channel.CreateBounded<TdsMessage>(new BoundedChannelOptions(1){SingleReader=true,SingleWriter=true});
        var receiving=ReceiveAsync();
        try{
            await foreach(var message in channel.Reader.ReadAllAsync(stopped.Token)){
                if(message.PacketType==TdsConstants.PacketTypePreLogin){await packetWriter.SendPreLoginResponseAsync(stopped.Token);continue;}
                if(!this.isAuthenticated){
                    if(message.PacketType!=0x10){await packetWriter.SendErrorAsync("Expected a LOGIN7 request.",stopped.Token);break;}
                    try{
                        var database=LoginString(message.Payload,68);
                        if(string.IsNullOrEmpty(database)){database=this.sqlEngine.Databases.DefaultDatabaseName;}
                        _=this.sqlEngine.Databases.Resolve(database);this.sessionContext.DatabaseName=database;this.loginDatabase=database;this.isAuthenticated=true;
                        await packetWriter.SendLoginAckAsync(database,stopped.Token,this.sqlEngine.Databases.GetDatabaseCollation(database));
                    }catch(Exception ex){await packetWriter.SendErrorAsync(ex.Message,stopped.Token,(ex as SqlQueryException)?.Number??50000,(ex as SqlQueryException)?.Line??1);break;}
                    continue;
                }
                var previous=this.sessionContext.DatabaseName;
                if((message.Status&TdsConstants.StatusResetConnection)!=0){this.sessionContext.Reset();this.prepared.Clear();this.sessionContext.DatabaseName=this.loginDatabase;}
                if(message.PacketType==TdsConstants.PacketTypeAttention){await packetWriter.SendAttentionAckAsync(stopped.Token);continue;}
                using var request=CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
                Interlocked.Exchange(ref this.activeRequest,request);
                this.sessionContext.CancellationToken=request.Token;
                try{
                    IReadOnlyList<QueryResult> results;IReadOnlyList<TdsRpcParameter> output=[];var rpc=false;
                    if(message.PacketType==TdsConstants.PacketTypeSqlBatch){results=this.sqlEngine.ExecuteBatch(ExtractSql(message.Payload),this.sessionContext);}
                    else if(message.PacketType==0x03){rpc=true;(results,output)=this.ExecuteRpc(TdsRpcRequest.Parse(message.Payload));}
                    else if(message.PacketType==TdsConstants.PacketTypeTransactionManager){results=this.ExecuteTransaction(message.Payload);}
                    else{throw new NotSupportedException($"TDS request 0x{message.PacketType:X2} is not supported.");}
                    await packetWriter.SendQueryResultsAsync(results,request.Token,rpc,output,ObserveAsync,stopped.Token);
                }catch(OperationCanceledException) when(request.IsCancellationRequested&&!stopped.IsCancellationRequested){await packetWriter.SendAttentionAckAsync(stopped.Token);}
                catch(Exception ex) when(!stopped.IsCancellationRequested){await packetWriter.SendErrorAsync(ex.Message,stopped.Token,(ex as SqlQueryException)?.Number??50000,(ex as SqlQueryException)?.Line??1);}
                finally{Interlocked.CompareExchange(ref this.activeRequest,null,request);this.sessionContext.CancellationToken=CancellationToken.None;}
                async ValueTask ObserveAsync(CancellationToken token){
                    if(previous!=this.sessionContext.DatabaseName){await packetWriter.SendDatabaseChangeAsync(this.sessionContext.DatabaseName,previous,token,this.sqlEngine.Databases.GetDatabaseCollation(this.sessionContext.DatabaseName),this.sqlEngine.Databases.GetDatabaseCollation(previous));previous=this.sessionContext.DatabaseName;}
                    if(this.sessionContext.IsInTransaction&&this.transactionDescriptor==0){this.transactionDescriptor=checked((ulong)this.sessionContext.SessionId);await packetWriter.SendTransactionChangeAsync(8,this.transactionDescriptor,token);}
                    else if(!this.sessionContext.IsInTransaction&&this.transactionDescriptor!=0){await packetWriter.SendTransactionChangeAsync(this.sessionContext.LastTransactionOperation,this.transactionDescriptor,token);this.transactionDescriptor=0;}
                }
            }
        }finally{
            stopped.Cancel();this.activeRequest?.Cancel();this.sessionContext.Dispose();this.prepared.Clear();
            try{await receiving;}catch(OperationCanceledException){}catch(IOException){}
        }
        async Task ReceiveAsync(){
            try{
                while(!stopped.IsCancellationRequested){
                    var next=await TdsPacketReader.ReadMessageAsync(reader,stopped.Token);if(next is null){break;}
                    if(next.Value.PacketType==TdsConstants.PacketTypeAttention){
                        var running=Volatile.Read(ref this.activeRequest);
                        if(running is not null){try{running.Cancel();}catch(ObjectDisposedException){}continue;}
                    }
                    await channel.Writer.WriteAsync(next.Value,stopped.Token);
                }
                channel.Writer.TryComplete();
            }catch(Exception ex){channel.Writer.TryComplete(ex);}
        }
    }
    private (IReadOnlyList<QueryResult>,IReadOnlyList<TdsRpcParameter>) ExecuteRpc(TdsRpcRequest rpc){
        var p=rpc.Parameters;
        if(rpc.Procedure=="sp_reset_connection"){this.sessionContext.Reset();this.prepared.Clear();this.sessionContext.DatabaseName=this.loginDatabase;return([QueryResult.Empty],[]);}
        if(rpc.Procedure=="sp_unprepare"){if(p.Count!=1||!this.prepared.Remove(Convert.ToInt32(p[0].Value))){throw new SqlQueryException("Prepared statement handle was not found.",8179);}return([QueryResult.Empty],[]);}
        if(rpc.Procedure=="sp_execute"){
            if(p.Count==0||!this.prepared.TryGetValue(Convert.ToInt32(p[0].Value),out var plan)){throw new SqlQueryException("Prepared statement handle was not found.",8179);}
            if(plan.Database!=this.sessionContext.DatabaseName){throw new SqlQueryException("Prepared statement belongs to another database.",8179);}
            var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
            for(var i=0;i<plan.Parameters.Count;i++){if(i+1>=p.Count){throw new SqlQueryException("Prepared parameter is missing.",201);}values.Add(plan.Parameters[i].Name,p[i+1].Value);}
            return(this.sqlEngine.ExecuteBatch(plan.Sql,this.sessionContext,plan.Parameters,values),[]);
        }
        if(rpc.Procedure=="sp_executesql"){
            if(p.Count<1||p[0].Value is not string sql){throw new SqlQueryException("sp_executesql requires SQL text.",201);}
            var values=p.Skip(2).ToDictionary(a=>a.Name,a=>a.Value,StringComparer.OrdinalIgnoreCase);
            var parameters=p.Skip(2).Select(a=>new SqlParameter(a.Name,a.Type,a.Value is null)).ToArray();
            if(p.Skip(2).Any(a=>a.Output)){throw new NotSupportedException("Dynamic SQL output parameters are not supported.");}
            return(this.sqlEngine.ExecuteBatch(sql,this.sessionContext,parameters,values),[]);
        }
        if(rpc.Procedure is "sp_prepare" or "sp_prepexec"){
            if(p.Count<3||p[2].Value is not string sql){throw new SqlQueryException("Prepare requires parameter declarations and SQL text.",201);}
            if(this.prepared.Count>=1024){throw new SqlQueryException("The session prepared statement limit was reached.");}
            var parameters=ParameterDeclarations.Parse(p[1].Value as string??string.Empty);var handle=checked(++this.nextHandle);
            this.prepared.Add(handle,(sql,this.sessionContext.DatabaseName,parameters));
            IReadOnlyList<QueryResult> results=[QueryResult.Empty];
            if(rpc.Procedure=="sp_prepexec"){
                var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
                for(var i=0;i<parameters.Count;i++){if(i+3>=p.Count){throw new SqlQueryException("Prepared parameter is missing.",201);}values[parameters[i].Name]=p[i+3].Value;}
                results=this.sqlEngine.ExecuteBatch(sql,this.sessionContext,parameters,values);
            }
            return(results,[new TdsRpcParameter(p[0].Name,SqlType.Int,handle,true)]);
        }
        throw new NotSupportedException($"RPC '{rpc.Procedure}' is not supported.");
    }
    private IReadOnlyList<QueryResult> ExecuteTransaction(byte[] payload){
        var offset=TdsRpcRequest.HeadersLength(payload);if(payload.Length<offset+2){throw new InvalidDataException("Truncated transaction request.");}
        var operation=BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset,2));
        var sql=operation switch{5=>"BEGIN TRANSACTION",7=>"COMMIT TRANSACTION",8=>"ROLLBACK TRANSACTION",_=>throw new NotSupportedException($"Transaction request {operation} is not supported.")};
        return [this.sqlEngine.Execute(sql,this.sessionContext)];
    }
    private static string LoginString(byte[] payload,int offset){
        if(payload.Length<offset+4){throw new InvalidDataException("Truncated LOGIN7 packet.");}
        var start=BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset,2));var length=BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset+2,2));
        if(start+length*2>payload.Length){throw new InvalidDataException("LOGIN7 string is out of bounds.");}return Encoding.Unicode.GetString(payload,start,length*2);
    }
    private static string ExtractSql(byte[] payload)=>Encoding.Unicode.GetString(payload,TdsRpcRequest.HeadersLength(payload),payload.Length-TdsRpcRequest.HeadersLength(payload));
}
