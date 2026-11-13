using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;
using System.Security.Cryptography;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Authentication;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Security;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tds;

internal sealed record TdsProtectedAccess(HttpGatewayAuthorization Policy,Func<AuthenticatedIdentity,CancellationToken,ValueTask<IAuthorizationSession>> SessionFactory);

public sealed partial class TdsSession{
    private const int MaximumProtectedDeclarationCharacters=64*1024;
    private const int MaximumProtectedPreparedCharacters=256*1024;
    private const int MaximumSqlParameters=2100;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly SessionContext sessionContext;
    private readonly Dictionary<int,(string Sql,string Database,IReadOnlyList<SqlParameter> Parameters,IReadOnlySet<string>? Outputs)> prepared=new();
    private CancellationTokenSource? activeRequest;
    private int nextHandle;
    private ulong transactionDescriptor;
    private ulong nextProtectedDescriptor;
    private bool isAuthenticated;
    private string loginDatabase="master";
    private readonly TdsRequestLog? requestLog;
    internal TdsProtectedAccess? Protection{get;init;}
    public TdsSession(long sessionId,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,TdsRequestLog? requestLog=null){this.sqlEngine=sqlEngine;this.sessionContext=new(sessionId,adapter.DatabaseName);this.requestLog=requestLog;}
    public Task ProcessConnectionAsync(PipeReader reader,PipeWriter writer,CancellationToken cancellationToken=default)=>this.ProcessConnectionAsync(reader,writer,cancellationToken,null);
    internal async Task ProcessConnectionAsync(PipeReader reader,PipeWriter writer,CancellationToken cancellationToken,TimeSpan? loginTimeout,TimeSpan? writeTimeout=null,TimeSpan? incompleteMessageTimeout=null,FrameBufferQuota? receiveBuffers=null,FrameBufferQuota? bufferedRequests=null,FrameBufferQuota? pendingWrites=null){
        var packetWriter=new TdsPacketWriter(writer,writeTimeout,pendingWrites);
        using var stopped=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if(loginTimeout is{} timeout){stopped.CancelAfter(timeout);}
        var channel=Channel.CreateBounded<(TdsMessage Message,IDisposable? Reservation)>(new BoundedChannelOptions(1){SingleReader=true,SingleWriter=true});
        var receiving=ReceiveAsync();
        OidcAuthorizationSession? authenticated=null;
        AuthorizationLease? transactionAuthorization=null;
        CancellationTokenRegistration transactionCancellation=default;
        var preLogin=false;
        try{
            await foreach(var entry in channel.Reader.ReadAllAsync(stopped.Token)){
                using var reservation=entry.Reservation;
                stopped.Token.ThrowIfCancellationRequested();
                var message=entry.Message;
                if(message.PacketType==TdsConstants.PacketTypePreLogin){
                    if(this.Protection is not null&&(preLogin||!TdsFedAuthLogin.IsSupportedPreLogin(message.Payload))){break;}
                    await packetWriter.SendPreLoginResponseAsync(this.Protection is not null,stopped.Token);
                    preLogin=true;
                    continue;
                }
                if(!this.isAuthenticated){
                    if(message.PacketType!=0x10){await packetWriter.SendErrorAsync("Expected a LOGIN7 request.",stopped.Token);break;}
                    try{
                        if(this.Protection is{} protection){
                            if(!preLogin){throw TdsFedAuthLogin.Rejected();}
                            var token=TdsFedAuthLogin.ReadToken(message.Payload);
                            authenticated=await OidcAuthorizationSession.AuthenticateWithSessionFactoryAsync(protection.Policy.Tokens,protection.SessionFactory,token,stopped.Token);
                        }
                        var database=LoginString(message.Payload,68);
                        if(string.IsNullOrEmpty(database)){database=this.sqlEngine.Databases.DefaultDatabaseName;}
                        if(authenticated is not null){
                            using var operation=await authenticated.BeginOperationAsync(stopped.Token);
                            if(!this.Protection!.Policy.CanDiscover(operation,database)){throw TdsFedAuthLogin.Rejected();}
                        }
                        _=this.sqlEngine.Databases.Resolve(database);this.sessionContext.DatabaseName=database;this.loginDatabase=database;Volatile.Write(ref this.isAuthenticated,true);
                        await packetWriter.SendLoginAckAsync(database,stopped.Token,this.sqlEngine.Databases.GetDatabaseCollation(database),authenticated is not null);
                        if(loginTimeout.HasValue){stopped.CancelAfter(Timeout.InfiniteTimeSpan);}
                    }catch(Exception ex) when(!stopped.IsCancellationRequested&&ex is not TdsPacketWriter.WriteAborted){
                        var error=this.Protection is null?ex:TdsFedAuthLogin.Rejected();
                        await packetWriter.SendErrorAsync(error.Message,stopped.Token,(error as SqlQueryException)?.Number??50000,(error as SqlQueryException)?.Line??1);break;
                    }finally{if(this.Protection is not null){CryptographicOperations.ZeroMemory(message.Payload);}}
                    continue;
                }
                var previous=this.sessionContext.DatabaseName;
                if((message.Status&TdsConstants.StatusResetConnection)!=0){
                    this.sessionContext.Reset();this.prepared.Clear();this.sessionContext.DatabaseName=this.loginDatabase;
                    if(this.Protection is not null){ReleaseTransactionAuthorization();this.transactionDescriptor=0;}
                }
                if(message.PacketType==TdsConstants.PacketTypeAttention){await packetWriter.SendAttentionAckAsync(stopped.Token);continue;}
                using var request=CancellationTokenSource.CreateLinkedTokenSource(stopped.Token);
                Interlocked.Exchange(ref this.activeRequest,request);
                this.sessionContext.CancellationToken=request.Token;
                var startedUtc=DateTime.UtcNow;var started=global::System.Diagnostics.Stopwatch.GetTimestamp();
                string kind=$"0x{message.PacketType:X2}";string? procedure=null,text=null,declarations=null;IReadOnlyList<TdsRpcParameter> received=[];var resultSets=0;Exception? failure=null;
                try{
                    IReadOnlyList<QueryResult> results;IReadOnlyList<TdsRpcParameter> output=[];var rpc=false;
                    if(this.Protection is{} protectedAccess){
                        if(authenticated is null){throw TdsFedAuthLogin.Rejected();}
                        if(message.PacketType is not(TdsConstants.PacketTypeSqlBatch or TdsConstants.PacketTypeTransactionManager or 0x03)
                            ||(message.Status&0x10)!=0){throw new SqlQueryException("This request is unavailable in protected TDS.",40515);}
                        var managerRequest=message.PacketType==TdsConstants.PacketTypeTransactionManager;
                        ValidateProtectedDescriptor(message.Payload,this.transactionDescriptor,managerRequest);
                        if(this.sessionContext.IsInTransaction&&transactionAuthorization is null){throw new SqlQueryException("The protected transaction authorization is unavailable.",229);}
                        using var operation=transactionAuthorization is null?await authenticated.BeginOperationAsync(request.Token):null;
                        using var revoked=operation?.CancellationToken.Register(()=>{
                            if(!request.IsCancellationRequested){stopped.Cancel();}
                        })??default;
                        var authorization=transactionAuthorization??operation!;
                        var policy=protectedAccess.Policy;
                        if(!policy.CanDiscover(authorization,this.sessionContext.DatabaseName)){throw new SqlQueryException("The identity is not permitted to discover this database.",229);}
                        if(message.PacketType==0x03){
                            kind="rpc";
                            var parsed=TdsRpcRequest.ParseBounded(message.Payload,MaximumSqlParameters+3);
                            procedure=parsed.Procedure;
                            using var rpcBinding=DatabaseRegistry.EnterSqlSession(this.sessionContext);
                            using var module=this.sessionContext.EnterIdentityScope(new());
                            (results,output)=this.ExecuteRpc(parsed,authorization);
                            try{await packetWriter.SendQueryResultsAsync(results,request.Token,true,output,ObserveAsync,stopped.Token);}
                            catch{
                                if(procedure is ("sp_prepare" or "sp_prepexec")&&output.FirstOrDefault()?.Value is int handle){this.prepared.Remove(handle);}
                                throw;
                            }
                            resultSets=results.Count;
                            continue;
                        }
                        kind=managerRequest?"transaction":"batch";
                        text=managerRequest?ReadTransactionCommand(message.Payload,true):ExtractSql(message.Payload);
                        var statement=SqlExecutionEngine.ParseAuthorizedStatement(text,!managerRequest,out var metadataOnly,!managerRequest,out var localBatch);
                        var begin=statement is BeginTransactionStatement{Distributed:false,MarkDefined:false,Name:null};
                        if(begin){
                            if(transactionAuthorization is not null||this.sessionContext.IsInTransaction||this.sqlEngine.Databases.GatewayTransactions is null){
                                throw new SqlQueryException("A protected transaction requires an idle connection and a Gateway transaction owner.",40515);
                            }
                            transactionAuthorization=await authenticated.BeginTransactionAsync(stopped.Token);
                            transactionCancellation=transactionAuthorization.CancellationToken.UnsafeRegister(static value=>((CancellationTokenSource)value!).Cancel(),stopped);
                            request.Token.ThrowIfCancellationRequested();
                            if(!policy.CanDiscover(transactionAuthorization,this.sessionContext.DatabaseName)){throw new SqlQueryException("The identity is not permitted to discover this database.",229);}
                        }
                        if(localBatch is not null){
                            results=this.ExecuteProtectedLocalProcedureBatch(text,localBatch,authorization);
                        }else if(begin||transactionAuthorization is not null&&statement is CommitTransactionStatement{Name:null} or RollbackTransactionStatement or SaveTransactionStatement){
                            request.Token.ThrowIfCancellationRequested();
                            results=[this.sqlEngine.Execute(text,this.sessionContext)];
                        }else{
                            var statementText=text;
                            if(metadataOnly){statementText=text.Substring(statement.StartOffset,statement.FragmentLength);}
                            results=this.ExecuteProtectedSql(statementText,statement,authorization,metadataOnly:metadataOnly);
                        }
                        if(!this.sessionContext.IsInTransaction){ReleaseTransactionAuthorization();}
                        using var binding=DatabaseRegistry.EnterSqlSession(this.sessionContext);
                        await packetWriter.SendQueryResultsAsync(results,request.Token,observe:ObserveAsync,transportCancellation:stopped.Token);
                        resultSets=results.Count;
                        continue;
                    }
                    if(message.PacketType==TdsConstants.PacketTypeSqlBatch){kind="batch";text=ExtractSql(message.Payload);results=this.sqlEngine.ExecuteBatch(text,this.sessionContext);}
                    else if(message.PacketType==0x03){
                        rpc=true;kind="rpc";var parsed=TdsRpcRequest.Parse(message.Payload);procedure=parsed.Procedure;received=parsed.Parameters;
                        if(this.requestLog is not null){(text,declarations)=this.RpcText(parsed);}
                        (results,output)=this.ExecuteRpc(parsed);
                    }
                    else if(message.PacketType==TdsConstants.PacketTypeTransactionManager){kind="transaction";results=this.ExecuteTransaction(message.Payload);}
                    else{throw new NotSupportedException($"TDS request 0x{message.PacketType:X2} is not supported.");}
                    resultSets=results.Count;
                    await packetWriter.SendQueryResultsAsync(results,request.Token,rpc,output,ObserveAsync,stopped.Token);
                }catch(OperationCanceledException ex) when(request.IsCancellationRequested&&!stopped.IsCancellationRequested){failure=ex;await ObserveAsync(stopped.Token);await packetWriter.SendAttentionAckAsync(stopped.Token);}
                catch(Exception ex)when(authenticated is not null&&!stopped.IsCancellationRequested&&ex is AuthorizationException or OidcAuthenticationException){
                    failure=ex;await packetWriter.SendErrorAsync("Operation authorization is unavailable.",stopped.Token,229);
                }
                catch(HttpSessionException ex)when(authenticated is not null&&!stopped.IsCancellationRequested){failure=ex;await packetWriter.SendErrorAsync("The identity is not permitted to use this SQL target.",stopped.Token,229);}
                catch(TdsPacketWriter.WriteAborted error){failure=error;throw;}
                catch(Exception ex) when(!stopped.IsCancellationRequested){failure=ex;if(ex is global::Seamless.Transactions.TransactionCommitException{Outcome:not global::Seamless.Transactions.TransactionOutcomeKind.Aborted}){await packetWriter.SendErrorAsync(ex.Message,stopped.Token);return;}await ObserveAsync(stopped.Token);await packetWriter.SendErrorAsync(ex.Message,stopped.Token,(ex as SqlQueryException)?.Number??50000,(ex as SqlQueryException)?.Line??1);}
                finally{
                    try{this.sessionContext.FinishRequest();}
                    finally{
                        if(!this.sessionContext.IsInTransaction){ReleaseTransactionAuthorization();}
                        Interlocked.CompareExchange(ref this.activeRequest,null,request);this.sessionContext.CancellationToken=CancellationToken.None;
                        this.requestLog?.Write(new(startedUtc,this.sessionContext.SessionId,previous,kind,procedure,text,declarations,received,resultSets,failure,global::System.Diagnostics.Stopwatch.GetElapsedTime(started)));
                    }
                }
                async ValueTask ObserveAsync(CancellationToken token){
                    if(previous!=this.sessionContext.DatabaseName){await packetWriter.SendDatabaseChangeAsync(this.sessionContext.DatabaseName,previous,token,this.sqlEngine.Databases.GetDatabaseCollation(this.sessionContext.DatabaseName),this.sqlEngine.Databases.GetDatabaseCollation(previous));previous=this.sessionContext.DatabaseName;}
                    if(this.sessionContext.IsInTransaction&&this.transactionDescriptor==0){
                        this.transactionDescriptor=this.Protection is null?checked((ulong)this.sessionContext.SessionId):checked(++this.nextProtectedDescriptor);
                        await packetWriter.SendTransactionChangeAsync(8,this.transactionDescriptor,token);
                    }
                    else if(!this.sessionContext.IsInTransaction&&this.transactionDescriptor!=0){await packetWriter.SendTransactionChangeAsync(this.sessionContext.LastTransactionOperation,this.transactionDescriptor,token);this.transactionDescriptor=0;}
                }
            }
        }catch(OperationCanceledException) when(stopped.IsCancellationRequested&&!cancellationToken.IsCancellationRequested){
        }finally{
            stopped.Cancel();
            try{this.activeRequest?.Cancel();this.sessionContext.Dispose();this.prepared.Clear();}
            finally{
                try{await receiving;}catch(OperationCanceledException){}catch(IOException){}
                finally{
                    ReleaseTransactionAuthorization();
                    authenticated?.Dispose();
                    while(channel.Reader.TryRead(out var abandoned)){
                        if(this.Protection is not null&&abandoned.Message.PacketType==0x10){CryptographicOperations.ZeroMemory(abandoned.Message.Payload);}
                        abandoned.Reservation?.Dispose();
                    }
                }
            }
        }
        void ReleaseTransactionAuthorization(){
            if(transactionAuthorization is null){return;}
            transactionCancellation.Dispose();
            transactionCancellation=default;
            transactionAuthorization.Dispose();
            transactionAuthorization=null;
        }
        async Task ReceiveAsync(){
            try{
                while(!stopped.IsCancellationRequested){
                    var next=await TdsPacketReader.ReadMessageAsync(reader,stopped.Token,incompleteMessageTimeout,receiveBuffers,this.Protection is not null&&!Volatile.Read(ref this.isAuthenticated));if(next is null){break;}
                    if(next.Value.PacketType==TdsConstants.PacketTypeAttention){
                        var running=Volatile.Read(ref this.activeRequest);
                        if(running is not null){try{running.Cancel();}catch(ObjectDisposedException){}continue;}
                    }
                    IDisposable? reservation=null;
                    if(bufferedRequests is not null&&next.Value.Payload.Length>0){
                        reservation=bufferedRequests.TryReserve(next.Value.Payload.Length);
                        if(reservation is null){stopped.Cancel();break;}
                    }
                    try{
                        await channel.Writer.WriteAsync((next.Value,reservation),stopped.Token);
                        reservation=null;
                    }finally{reservation?.Dispose();}
                }
                channel.Writer.TryComplete();
            }catch(IOException error)when(error is TdsPacketReader.ReceiveDeadlineExceeded or TdsPacketReader.ReceiveBufferLimitExceeded){stopped.Cancel();channel.Writer.TryComplete();}
            catch(Exception ex){channel.Writer.TryComplete(ex);}
        }
    }
    private IReadOnlyList<QueryResult> ExecuteProtectedSql(string sql,TSqlStatement statement,AuthorizationLease authorization,
        IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null,bool metadataOnly=false){
        var catalog=new HttpAuthorizedSqlCatalog(this.sqlEngine.Databases,this.Protection!.Policy,authorization,
            statement is SelectStatement?name=>this.FindProtectedMetadata(name,authorization):null);
        if(statement is ExecuteStatement execute&&!metadataOnly){
            var (target,arguments)=this.ReadProcedureInvocation(execute);
            var input=arguments.Select(argument=>(argument.Variable?.Name??string.Empty,ReadProcedureArgument(argument.ParameterValue,parameters,values),argument.IsOutput));
            if(this.ExecuteProtectedMetadataProcedure(target,input,authorization) is{} metadata){return [metadata];}
            return this.sqlEngine.ExecuteAuthorizedProcedure(target,this.sessionContext,catalog,(declared,_)=>
                BindNamedOrPositionalValues(declared,input));
        }
        return [this.sqlEngine.ExecuteAuthorizedStatement(sql,statement,this.sessionContext,catalog,catalog.DemandMutation,parameters,values,metadataOnly)];
    }
    private (SqlObjectName Target,IList<ExecuteParameter> Arguments) ReadProcedureInvocation(TSqlStatement statement){
        if(statement is not ExecuteStatement{Options.Count:0,ExecuteSpecification:{Variable:null,LinkedServer:null,ExecuteContext:null,
            ExecutableEntity:ExecutableProcedureReference{AdHocDataSource:null,ProcedureReference:{ProcedureVariable:null,ProcedureReference:{Number:null,Name:{} name}}} procedure}}){
            throw UnsupportedProcedureCall();
        }
        return(this.ProcedureTarget(name),procedure.Parameters);
    }
    private IReadOnlyList<QueryResult> ExecuteProtectedLocalProcedureBatch(string sql,IList<TSqlStatement> statements,AuthorizationLease authorization){
        if(statements.Count<3||statements[^1] is not SelectStatement{Into:null,WithCtesAndXmlNamespaces:null,ComputeClauses.Count:0,OptimizerHints.Count:0,
            QueryExpression:QuerySpecification{FromClause:null,WhereClause:null,GroupByClause:null,HavingClause:null,WindowClause:null,TopRowFilter:null,
                OrderByClause:null,OffsetClause:null,ForClause:null,UniqueRowFilter:UniqueRowFilter.NotSpecified} projection}){throw UnsupportedProcedureCall();}
        var (target,arguments)=this.ReadProcedureInvocation(statements[^2]);
        var catalog=new HttpAuthorizedSqlCatalog(this.sqlEngine.Databases,this.Protection!.Policy,authorization);
        var variables=new Dictionary<string,SqlParameter>(StringComparer.OrdinalIgnoreCase);
        var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
        foreach(var statement in statements.Take(statements.Count-2)){
            if(statement is not DeclareVariableStatement declaration){throw UnsupportedProcedureCall();}
            foreach(var element in declaration.Declarations){
                if(variables.Count>=64||element.DataType is not SqlDataTypeReference type){throw UnsupportedProcedureCall();}
                var variable=ParameterDeclarations.Parse(element.VariableName.Value+" "+sql.Substring(type.StartOffset,type.FragmentLength)).Single();
                if(!SqlExecutionEngine.SupportsOutput(variable.Type)||variables.ContainsKey(variable.Name)){throw UnsupportedProcedureCall();}
                if(variable.Type.ClrType==typeof(string)){variable=variable with{Type=variable.Type with{Collation=catalog.GetDatabaseCollation(target.Database)}};}
                var initial=element.Value is null?null:ReadProcedureArgument(element.Value,variables.Values.ToArray(),values);
                variables.Add(variable.Name,variable);
                values.Add(variable.Name,SqlScalarConversion.Assign(initial,variable.Type));
            }
        }
        var outputs=ReadOutputVariables(arguments,values);
        if(outputs.Count==0||projection.SelectElements.Count is <1 or >64){throw UnsupportedProcedureCall();}
        var names=new string[projection.SelectElements.Count];
        var columns=new ColumnMetadata[names.Length];
        for(var index=0;index<columns.Length;index++){
            if(projection.SelectElements[index] is not SelectScalarExpression{Expression:VariableReference reference} selected
                ||!variables.TryGetValue(reference.Name,out var variable)){throw UnsupportedProcedureCall();}
            var type=variable.Type;
            names[index]=variable.Name;
            columns[index]=new(selected.ColumnName?.Value??string.Empty,type.ClrType,type.Name,true,false,index,type.Length,type.Precision,type.Scale,type.Collation);
        }
        TdsPacketWriter.ValidateMetadata(columns);
        var results=this.ExecuteProtectedProcedureOutputs(target,arguments,variables.Values.ToArray(),values,catalog,(name,_,value)=>values[name]=value);
        return [..results,new QueryResult(columns,[names.Select(name=>values[name]).ToArray()],-1){StatementKind=SqlCommandKind.Select}];
    }
    private SqlObjectName ProcedureTarget(SchemaObjectName? name){
        if(name?.BaseIdentifier is null||name.Identifiers.Count is <1 or >2){throw UnsupportedProcedureCall();}
        // [名前] スキーマを書かなければ、方言の探す順で最初に見つかった手続きを使う。
        var written=new SqlObjectName(this.sessionContext.DatabaseName,name.SchemaIdentifier?.Value??"",name.BaseIdentifier.Value);
        var catalog=this.sqlEngine.Databases;
        _=catalog.SchemaResolution.Find(written,name.SchemaIdentifier is not null,catalog.FindRoutine,out var resolved);return resolved;
    }
    private static object? ReadProcedureArgument(ScalarExpression expression,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values){
        if(expression is VariableReference variable){
            var declaration=parameters?.FirstOrDefault(parameter=>string.Equals(parameter.Name,variable.Name,StringComparison.OrdinalIgnoreCase));
            if(declaration is null||values is null||!values.TryGetValue(declaration.Name,out var value)){throw new SqlQueryException("A procedure argument variable is undeclared.",137);}
            return SqlScalarConversion.Assign(value,declaration.Type);
        }
        return expression switch{
            NullLiteral=>null,
            StringLiteral text=>text.Value,
            IntegerLiteral number=>decimal.Parse(number.Value,global::System.Globalization.CultureInfo.InvariantCulture),
            NumericLiteral number=>decimal.Parse(number.Value,global::System.Globalization.CultureInfo.InvariantCulture),
            RealLiteral number=>double.Parse(number.Value,global::System.Globalization.CultureInfo.InvariantCulture),
            BinaryLiteral bytes=>Convert.FromHexString(bytes.Value[2..]),
            _=>throw UnsupportedProcedureCall()
        };
    }
    private static SqlQueryException UnsupportedProcedureCall()=>new("This procedure invocation is unavailable in protected TDS.",40515);
    private (IReadOnlyList<QueryResult>,IReadOnlyList<TdsRpcParameter>) ExecuteRpc(TdsRpcRequest rpc,AuthorizationLease? authorization=null){
        var protectedRequest=this.Protection is not null;
        if(protectedRequest){
            if(authorization is null){throw TdsFedAuthLogin.Rejected();}
            if(rpc.Procedure is not("sp_executesql" or "sp_prepare" or "sp_prepexec" or "sp_execute" or "sp_unprepare")){
                if(rpc.HasExtendedParameterFlags){throw new SqlQueryException("This RPC parameter mode is unavailable in protected TDS.",40515);}
                var name=new TSql170Parser(true).ParseSchemaObjectName(new StringReader(rpc.Procedure),out var errors);
                if(errors.Count!=0){throw UnsupportedProcedureCall();}
                var target=this.ProcedureTarget(name);
                if(this.ExecuteProtectedMetadataProcedure(target,Arguments(0),authorization) is{} metadata){return([metadata],[]);}
                var catalog=new HttpAuthorizedSqlCatalog(this.sqlEngine.Databases,this.Protection!.Policy,authorization);
                if(!rpc.Parameters.Any(static argument=>argument.Output)){
                    return(this.sqlEngine.ExecuteAuthorizedProcedure(target,this.sessionContext,catalog,
                        (parameters,_)=>BindNamedOrPositionalValues(parameters,Arguments(0))),[]);
                }
                var requested=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
                IReadOnlyList<TdsRpcParameter> outputs=[];
                var results=this.sqlEngine.ExecuteAuthorizedProcedure(target,this.sessionContext,catalog,
                    (parameters,declaredOutputs)=>BindNamedOrPositionalValues(parameters,Arguments(0),declaredOutputs,requested),
                    (parameters,values)=>outputs=parameters.Where(parameter=>requested.ContainsKey(parameter.Name))
                        .Select(parameter=>new TdsRpcParameter(rpc.Parameters[requested[parameter.Name]].Name,parameter.Type,values[parameter.Name],true){Ordinal=requested[parameter.Name]})
                        .OrderBy(parameter=>parameter.Ordinal).ToArray());
                return(results,outputs);
            }
        }
        var p=rpc.Parameters;
        if(rpc.Procedure=="sp_reset_connection"){this.sessionContext.Reset();this.prepared.Clear();this.sessionContext.DatabaseName=this.loginDatabase;return([QueryResult.Empty],[]);}
        if(rpc.Procedure=="sp_unprepare"){if(p.Count!=1||!this.prepared.Remove(Convert.ToInt32(p[0].Value))){throw new SqlQueryException("Prepared statement handle was not found.",8179);}return([QueryResult.Empty],[]);}
        if(rpc.Procedure=="sp_execute"){
            if(p.Count==0||!this.prepared.TryGetValue(Convert.ToInt32(p[0].Value),out var plan)){throw new SqlQueryException("Prepared statement handle was not found.",8179);}
            if(plan.Database!=this.sessionContext.DatabaseName){throw new SqlQueryException("Prepared statement belongs to another database.",8179);}
            if(protectedRequest&&plan.Outputs is{} outputs){return this.ExecuteProtectedProcedureRpc(plan.Sql,plan.Parameters,outputs,rpc,1,authorization!);}
            var values=BindPositionalValues(plan.Parameters,1);
            return(RunSql(plan.Sql,plan.Parameters,values),[]);
        }
        if(rpc.Procedure=="sp_executesql"){
            if(p.Count<1||p[0].Value is not string sql){throw new SqlQueryException("sp_executesql requires SQL text.",201);}
            if(protectedRequest){
                var outputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var bound=ParseProtectedDeclarations(outputs);
                if(outputs.Count>0||p.Skip(2).Any(argument=>argument.Output)){return this.ExecuteProtectedProcedureRpc(sql,bound,outputs,rpc,2,authorization!);}
                return(RunSql(sql,bound,BindNamedOrPositionalValues(bound,Arguments(2))),[]);
            }
            if(p.Skip(2).Any(a=>a.Output)){throw new NotSupportedException("Dynamic SQL output parameters are not supported.");}
            var values=p.Skip(2).ToDictionary(a=>a.Name,a=>a.Value,StringComparer.OrdinalIgnoreCase);
            // 引数型はSQL Server同様に宣言文(@p nvarchar(4000)等)を優先する。値の型情報はNULLなどで長さが縮む(nvarchar(1)等)。宣言が読めない・無い名前は値の型情報へ戻す。
            var declared=ParameterDeclarations.TryParse(p.Count>1?p[1].Value as string:null).ToDictionary(d=>d.Name,StringComparer.OrdinalIgnoreCase);
            var parameters=p.Skip(2).Select(a=>new SqlParameter(a.Name,declared.TryGetValue(a.Name,out var d)?d.Type:a.Type,a.Value is null)).ToArray();
            return(RunSql(sql,parameters,values),[]);
        }
        if(rpc.Procedure is "sp_prepare" or "sp_prepexec"){
            if(p.Count<3||p[2].Value is not string sql){throw new SqlQueryException("Prepare requires parameter declarations and SQL text.",201);}
            if(this.prepared.Count>=1024){throw new SqlQueryException("The session prepared statement limit was reached.");}
            HashSet<string>? declaredOutputs=null;
            IReadOnlyList<SqlParameter> parameters;
            if(protectedRequest){
                var outputs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                parameters=ParseProtectedDeclarations(outputs);
                if(outputs.Count>0){declaredOutputs=outputs;}
            }else{parameters=ParameterDeclarations.Parse(p[1].Value as string??string.Empty);}
            IReadOnlyList<QueryResult> results=[QueryResult.Empty];
            IReadOnlyList<TdsRpcParameter> output=[];
            if(protectedRequest){
                if(rpc.Procedure=="sp_prepare"&&(p.Count>4||p.Count==4&&p[3].Value is not(0 or 1))){
                    throw new SqlQueryException("Prepare metadata options are unavailable in protected TDS.",40515);
                }
                if(sql.Length+parameters.Sum(parameter=>parameter.Name.Length)+this.prepared.Values.Sum(plan=>plan.Sql.Length+plan.Parameters.Sum(parameter=>parameter.Name.Length))>MaximumProtectedPreparedCharacters){
                    throw new SqlQueryException("The session prepared statement size limit was reached.",103);
                }
                if(rpc.Procedure=="sp_prepare"){
                    var metadataBatch=declaredOutputs is null?this.ExecuteJdbcColumnMetadata(sql,parameters,null,authorization!,metadataOnly:true):null;
                    var statement=metadataBatch is null?SqlExecutionEngine.ParseAuthorizedStatement(sql):null;
                    if(declaredOutputs is not null){_ =this.ReadProcedureInvocation(statement!);}
                    else if(metadataBatch is null&&statement is not(SelectStatement or InsertStatement or UpdateStatement or DeleteStatement)){
                        throw new SqlQueryException("This statement is unavailable through the protected SQL endpoint.",40515);
                    }
                    if(declaredOutputs is null&&p.Count==4&&p[3].Value is 1){
                        results=metadataBatch??this.ExecuteProtectedSql(sql,statement!,authorization!,parameters,metadataOnly:true);
                    }
                }
            }
            var handle=checked(++this.nextHandle);
            if(!protectedRequest){this.prepared.Add(handle,(sql,this.sessionContext.DatabaseName,parameters,null));}
            if(rpc.Procedure=="sp_prepexec"){
                if(declaredOutputs is not null){(results,output)=this.ExecuteProtectedProcedureRpc(sql,parameters,declaredOutputs,rpc,3,authorization!);}
                else{results=RunSql(sql,parameters,BindPositionalValues(parameters,3));}
            }
            if(protectedRequest){this.prepared.Add(handle,(sql,this.sessionContext.DatabaseName,parameters,declaredOutputs));}
            return(results,[new TdsRpcParameter(p[0].Name,SqlType.Int,handle,true),..output]);
        }
        throw new NotSupportedException($"RPC '{rpc.Procedure}' is not supported.");

        IReadOnlyList<QueryResult> RunSql(string sql,IReadOnlyList<SqlParameter> parameters,IReadOnlyDictionary<string,object?> values)
            =>protectedRequest?this.ExecuteJdbcColumnMetadata(sql,parameters,values,authorization!)
                ??this.ExecuteProtectedSql(sql,SqlExecutionEngine.ParseAuthorizedStatement(sql),authorization!,parameters,values)
                :this.sqlEngine.ExecuteModuleBatch(sql,this.sessionContext,parameters,values);
        IReadOnlyList<SqlParameter> ParseProtectedDeclarations(HashSet<string>? outputs=null){
            var declaration=p.Count>1?p[1].Value:null;
            if(declaration is not null and not string){throw new SqlQueryException("Invalid SQL parameter declarations.",201);}
            if(declaration is string{Length:>MaximumProtectedDeclarationCharacters}){throw new SqlQueryException("SQL parameter declarations exceed the query limit.",103);}
            var parsed=ParameterDeclarations.Parse(declaration as string??string.Empty,outputs);
            if(parsed.Count>MaximumSqlParameters){throw new SqlQueryException("SQL parameter count exceeds the query limit.",103);}
            return parsed;
        }
        Dictionary<string,object?> BindPositionalValues(IReadOnlyList<SqlParameter> parameters,int start){
            if(protectedRequest&&(p.Count-start!=parameters.Count||p.Skip(start).Any(parameter=>parameter.Output))){
                throw ArgumentsDoNotMatch();
            }
            var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
            for(var i=0;i<parameters.Count;i++){
                if(i+start>=p.Count){throw new SqlQueryException("Prepared parameter is missing.",201);}
                values.Add(parameters[i].Name,p[i+start].Value);
            }
            return values;
        }
        IEnumerable<(string Name,object? Value,bool Output)> Arguments(int start)=>rpc.Parameters.Skip(start).Select(argument=>(argument.Name,argument.Value,argument.Output));
    }
    private (IReadOnlyList<QueryResult>,IReadOnlyList<TdsRpcParameter>) ExecuteProtectedProcedureRpc(string sql,IReadOnlyList<SqlParameter> parameters,
        IReadOnlySet<string> declaredOutputs,TdsRpcRequest rpc,int start,AuthorizationLease authorization){
        if(rpc.HasExtendedParameterFlags||declaredOutputs.Count is <1 or >64){throw UnsupportedProcedureCall();}
        var (target,arguments)=this.ReadProcedureInvocation(SqlExecutionEngine.ParseAuthorizedStatement(sql));
        var callerOrdinals=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var input=BindNamedOrPositionalValues(parameters,rpc.Parameters.Skip(start).Select(argument=>(argument.Name,argument.Value,argument.Output)),declaredOutputs,callerOrdinals);
        if(!ReadOutputVariables(arguments,input).SetEquals(callerOrdinals.Keys)||callerOrdinals.Count!=declaredOutputs.Count){throw ArgumentsDoNotMatch();}
        var catalog=new HttpAuthorizedSqlCatalog(this.sqlEngine.Databases,this.Protection!.Policy,authorization);
        var response=new List<TdsRpcParameter>(callerOrdinals.Count);
        var results=this.ExecuteProtectedProcedureOutputs(target,arguments,parameters,input,catalog,(caller,type,value)=>{
            var ordinal=checked(start+callerOrdinals[caller]);
            response.Add(new(rpc.Parameters[ordinal].Name,type,value,true){Ordinal=ordinal});
        });
        return(results,response.OrderBy(parameter=>parameter.Ordinal).ToArray());
    }
    private static HashSet<string> ReadOutputVariables(IList<ExecuteParameter> arguments,IReadOnlyDictionary<string,object?> input){
        var assigned=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var argument in arguments.Where(static argument=>argument.IsOutput)){
            if(argument.ParameterValue is not VariableReference variable||!input.ContainsKey(variable.Name)||!assigned.Add(variable.Name)){
                throw ArgumentsDoNotMatch();
            }
        }
        return assigned;
    }
    private IReadOnlyList<QueryResult> ExecuteProtectedProcedureOutputs(SqlObjectName target,IList<ExecuteParameter> arguments,IReadOnlyList<SqlParameter> parameters,
        IReadOnlyDictionary<string,object?> input,HttpAuthorizedSqlCatalog catalog,Action<string,SqlType,object?> publish){
        var callerTypes=parameters.ToDictionary(parameter=>parameter.Name,parameter=>parameter.Type,StringComparer.OrdinalIgnoreCase);
        var formalOrdinals=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var outputTypes=new Dictionary<string,SqlType>(StringComparer.OrdinalIgnoreCase);
        return this.sqlEngine.ExecuteAuthorizedProcedure(target,this.sessionContext,catalog,(formal,formalOutputs)=>{
            var bound=BindNamedOrPositionalValues(formal,arguments.Select(argument=>(argument.Variable?.Name??string.Empty,
                ReadProcedureArgument(argument.ParameterValue,parameters,input),argument.IsOutput)),formalOutputs,formalOrdinals);
            foreach(var parameter in formal){
                if(!formalOrdinals.TryGetValue(parameter.Name,out var index)){continue;}
                var caller=((VariableReference)arguments[index].ParameterValue).Name;
                var type=callerTypes[caller];
                // MAXを宣言するドライバーにも、同じ型で上限があるプロシージャの出力を返せる。
                if(type.Length==-1&&type.Name==parameter.Type.Name){type=parameter.Type;}
                if(!SqlExecutionEngine.SupportsOutput(type)){throw UnsupportedProcedureCall();}
                if(type.ClrType==typeof(string)){type=type with{Collation=type.Collation??catalog.GetDatabaseCollation(target.Database)};}
                outputTypes.Add(parameter.Name,type);
            }
            return bound;
        },(formal,values)=>{
            foreach(var parameter in formal){
                if(!formalOrdinals.TryGetValue(parameter.Name,out var index)){continue;}
                var caller=((VariableReference)arguments[index].ParameterValue).Name;
                var type=outputTypes[parameter.Name];
                publish(caller,type,SqlScalarConversion.Assign(values[parameter.Name],type));
            }
        });
    }
    private static Dictionary<string,object?> BindNamedOrPositionalValues(IReadOnlyList<SqlParameter> parameters,IEnumerable<(string Name,object? Value,bool Output)> arguments,
        IReadOnlySet<string>? declaredOutputs=null,Dictionary<string,int>? requestedOutputs=null){
        var supplied=new Dictionary<string,object?>(parameters.Count,StringComparer.OrdinalIgnoreCase);
        var hasNamedArgument=false;
        foreach(var argument in arguments){
            var name=argument.Name;
            if(name.Length==0){
                if(hasNamedArgument||supplied.Count>=parameters.Count){throw ArgumentsDoNotMatch();}
                name=parameters[supplied.Count].Name;
            }else{
                hasNamedArgument=true;
            }
            var ordinal=supplied.Count;
            if(!supplied.TryAdd(name,argument.Value)){throw ArgumentsDoNotMatch();}
            if(argument.Output){
                if(declaredOutputs?.Contains(name)!=true||requestedOutputs is null){throw ArgumentsDoNotMatch();}
                requestedOutputs.Add(name,ordinal);
            }
        }
        if(parameters.Count!=supplied.Count||parameters.Any(parameter=>!supplied.ContainsKey(parameter.Name))){throw ArgumentsDoNotMatch();}
        return supplied;
    }
    private static SqlQueryException ArgumentsDoNotMatch()=>new("SQL arguments do not match their declarations.",201);
    // 記録用にRPCが運ぶSQL本文と引数宣言を取り出す。sp_executeは準備時のSQLを引く。実行の成否には関与しない。
    private (string? Sql,string? Declarations) RpcText(TdsRpcRequest rpc){
        var p=rpc.Parameters;
        try{return Text();}catch(Exception ex) when(ex is FormatException or InvalidCastException or OverflowException){return(null,null);}
        (string?,string?) Text()=>rpc.Procedure switch{
            "sp_executesql"=>(p.Count>0?p[0].Value as string:null,p.Count>1?p[1].Value as string:null),
            "sp_prepare" or "sp_prepexec"=>(p.Count>2?p[2].Value as string:null,p.Count>1?p[1].Value as string:null),
            "sp_execute" when p.Count>0&&p[0].Value is not null&&this.prepared.TryGetValue(Convert.ToInt32(p[0].Value,global::System.Globalization.CultureInfo.InvariantCulture),out var plan)=>(plan.Sql,null),
            _=>(null,null)
        };
    }
    private IReadOnlyList<QueryResult> ExecuteTransaction(byte[] payload)=>[this.sqlEngine.Execute(ReadTransactionCommand(payload,false),this.sessionContext)];
    private static string ReadTransactionCommand(byte[] payload,bool protectedRequest){
        var offset=TdsRpcRequest.HeadersLength(payload);if(payload.Length<offset+2){throw new InvalidDataException("Truncated transaction request.");}
        var operation=BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset,2));
        if(protectedRequest){
            var arguments=payload.AsSpan(offset+2);
            var supported=operation switch{
                5=>arguments.Length==2&&arguments[0] is 0 or 2&&arguments[1]==0,
                7 or 8=>arguments.Length==2&&arguments[0]==0&&arguments[1]==0,
                _=>false
            };
            if(!supported){throw new SqlQueryException("This transaction request is unavailable in protected TDS.",40515);}
        }
        return operation switch{5=>"BEGIN TRANSACTION",7=>"COMMIT TRANSACTION",8=>"ROLLBACK TRANSACTION",_=>throw new NotSupportedException($"Transaction request {operation} is not supported.")};
    }
    private static void ValidateProtectedDescriptor(byte[] payload,ulong expected,bool required){
        var end=TdsRpcRequest.HeadersLength(payload);
        var descriptorFound=false;
        for(var offset=4;offset<end;){
            var header=payload.AsSpan(offset,end-offset);
            if(header.Length<6){throw new SqlQueryException("Invalid TDS request header.",4004);}
            var length=BinaryPrimitives.ReadInt32LittleEndian(header);
            if(length<6||length>header.Length){throw new SqlQueryException("Invalid TDS request header.",4004);}
            if(BinaryPrimitives.ReadUInt16LittleEndian(header[4..])==2){
                if(descriptorFound||length!=18||BinaryPrimitives.ReadUInt64LittleEndian(header[6..])!=expected
                    ||BinaryPrimitives.ReadUInt32LittleEndian(header[14..])!=1){throw new SqlQueryException("Invalid TDS transaction descriptor.",3988);}
                descriptorFound=true;
            }
            offset+=length;
        }
        if(!descriptorFound&&(required||expected!=0)){throw new SqlQueryException("A TDS transaction descriptor is required.",3988);}
    }
    private static string LoginString(byte[] payload,int offset){
        if(payload.Length<offset+4){throw new InvalidDataException("Truncated LOGIN7 packet.");}
        var start=BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset,2));var length=BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset+2,2));
        if(start+length*2>payload.Length){throw new InvalidDataException("LOGIN7 string is out of bounds.");}return Encoding.Unicode.GetString(payload,start,length*2);
    }
    private static string ExtractSql(byte[] payload)=>Encoding.Unicode.GetString(payload,TdsRpcRequest.HeadersLength(payload),payload.Length-TdsRpcRequest.HeadersLength(payload));
}
