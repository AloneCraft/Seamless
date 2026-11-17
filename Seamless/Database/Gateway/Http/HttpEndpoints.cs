using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text.Json;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Execution;
using Seamless.Security;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Http;

public static class HttpEndpoints{
    internal const string CorsOrigin="*";
    internal const string CorsMethods="GET, POST, DELETE, OPTIONS";
    internal const string CorsHeaders="Authorization, Content-Type, X-Seamless-Session, X-Seamless-Transaction";
    internal static bool IsTablePath(string? path)=>path=="/api/tables"||path?.StartsWith("/api/tables/",StringComparison.Ordinal)==true;
    public static Task HandleRequestAsync(global::System.Net.HttpListenerContext context,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,CancellationToken cancellationToken=default)=>HandleRequestAsync(new Exchange(context),adapter,sqlEngine,null,cancellationToken);
    internal static Task HandleRequestAsync(HttpContext context,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,HttpSessionStore? sessions,CancellationToken cancellationToken,HttpGatewayAuthorization? protection=null,AuthorizationLease? operation=null,ResourceBudget? requestBodyBudget=null,long? maximumRequestBodyBytes=null,FrameBufferQuota? pendingWrites=null,TimeSpan? responseWriteTimeout=null,Func<ValueTask<(AuthorizationLease Lease,byte[] Proof)?>>? beginTransaction=null,HttpSessionStore.Lease? continuedSession=null,FrameBufferQuota? sharedRequestBodies=null)=>HandleRequestAsync(new Exchange(context,pendingWrites,responseWriteTimeout),adapter,sqlEngine,sessions,cancellationToken,protection,operation,requestBodyBudget,maximumRequestBodyBytes,beginTransaction,continuedSession,sharedRequestBodies);
    private static async Task HandleRequestAsync(Exchange context,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,HttpSessionStore? sessions,CancellationToken cancellationToken,HttpGatewayAuthorization? protection=null,AuthorizationLease? operation=null,ResourceBudget? requestBodyBudget=null,long? maximumRequestBodyBytes=null,Func<ValueTask<(AuthorizationLease Lease,byte[] Proof)?>>? beginTransaction=null,HttpSessionStore.Lease? continuedSession=null,FrameBufferQuota? sharedRequestBodies=null){
        var req=context;var res=context;var responseStarted=false;var lease=continuedSession;SessionContext? transient=null;var token=req.Header("X-Seamless-Session");
        res.SetLegacyCorsHeaders();
        try{
            if(req.HttpMethod=="OPTIONS"){res.StatusCode=204;return;}
            var path=req.Path;
            var subject=operation?.Snapshot.Subject;
            if(lease is null&&!string.IsNullOrEmpty(token)){if(sessions is null){throw new HttpSessionException(404,"HTTP sessions are not enabled.");}lease=await sessions.AcquireAsync(token,cancellationToken,subject);}
            if(continuedSession is null&&lease?.Entry.TransactionAuthorization is not null){
                lease.Dispose();lease=null;throw new HttpSessionException(409,"Continuing this transaction requires its original bearer credential and transaction ID.");
            }
            var selected=req.Query("database")??lease?.Session.DatabaseName??sqlEngine.Databases.DefaultDatabaseName;
            if(protection is not null&&(path=="/api/session"&&req.HttpMethod!="DELETE"||IsTablePath(path)||path=="/api/sql")&&!protection.CanDiscover(operation!,selected)){
                throw new HttpSessionException(403,"The identity is not permitted to discover this database.");
            }
            if(lease is not null&&!selected.Equals(lease.Session.DatabaseName,StringComparison.OrdinalIgnoreCase)){throw new HttpSessionException(409,"Use SQL USE to change the session database.");}
            var session=lease?.Session??(transient=new(0,selected));session.CancellationToken=cancellationToken;
            if(protection is null||path!="/api/databases"){
                adapter=session.Run(()=>{using var binding=DatabaseRegistry.EnterSqlSession(session);return sqlEngine.Databases.Resolve(selected).Adapter;});
            }else{
            }
            if(path!="/api/session"||req.HttpMethod=="DELETE"){ValidateTransaction(req.Header("X-Seamless-Transaction"),lease?.Session);}
            if(path is "/" or "/index.html"){responseStarted=true;await ServeHtmlAsync(res,adapter.DatabaseName,cancellationToken);return;}
            var sqlBody=path=="/api/sql"&&req.HttpMethod=="POST";
            using var bodyReservation=sqlBody?requestBodyBudget?.ReserveMemory(0):null;
            using var sharedBodyReservation=sqlBody?sharedRequestBodies?.ReserveGrowing():null;
            var budgetedBody=bodyReservation is not null||sharedBodyReservation is not null;
            var bodyLimit=Math.Min(maximumRequestBodyBytes??long.MaxValue,Math.Min(requestBodyBudget?.MemoryLimitBytes??long.MaxValue,sharedRequestBodies?.MaximumBytes??long.MaxValue));
            if(budgetedBody&&req.ContentLength is{} contentLength){
                ReserveBody(bodyReservation,sharedBodyReservation,contentLength,bodyLimit);
            }
            using var scope=sessions?.Execution(cancellationToken)??new ExecutionScope(new(){CancellationToken=cancellationToken});
            if(path=="/api/databases"&&sessions is not null){
                using var listing=new DatabaseListBuffer(scope);
                using(var writer=new Utf8JsonWriter(listing)){
                    writer.WriteStartArray();
                    foreach(var database in sqlEngine.Databases.Databases){
                        cancellationToken.ThrowIfCancellationRequested();
                        if(protection is not null&&!protection.CanDiscover(operation!,database.Name)){continue;}
                        writer.WriteStartObject();
                        writer.WriteString("Name",database.Name);
                        writer.WriteBoolean("IsReadOnly",database.IsReadOnly);
                        writer.WriteEndObject();
                        if(writer.BytesPending>=2048){await writer.FlushAsync(cancellationToken);}
                    }
                    writer.WriteEndArray();
                    await writer.FlushAsync(cancellationToken);
                }
                res.ContentType="application/json; charset=utf-8";
                res.ContentLength64=listing.Length;
                responseStarted=true;
                await listing.SendAsync(res,cancellationToken);
                return;
            }
            using var output=scope.CreateTemporaryFile();
            using(var writer=new Utf8JsonWriter(output)){
                if(path=="/api/session"){
                    if(sessions is null){throw new HttpSessionException(404,"HTTP sessions are not enabled.");}
                    if(req.HttpMethod=="POST"){if(lease is not null){throw new HttpSessionException(400,"Creating a session does not accept an existing session token.");}token=sessions.Create(selected,subject);res.StatusCode=201;WriteSession(writer,token,session);}
                    else if(lease is null){throw new HttpSessionException(400,"An X-Seamless-Session header is required.");}
                    else if(req.HttpMethod=="GET"){WriteSession(writer,token!,session);}
                    else if(req.HttpMethod=="DELETE"){sessions.Delete(token!,lease);JsonSerializer.Serialize(writer,new{closed=true});}
                    else{throw new HttpSessionException(405,"Unsupported session operation.");}
                }else if(path.StartsWith("/api/transactions/",StringComparison.Ordinal)&&req.HttpMethod=="GET"){
                    if(sqlEngine.Databases.GatewayTransactions is not{} manager||!Guid.TryParse(path["/api/transactions/".Length..],out var transaction)){throw new HttpSessionException(400,"A Gateway transaction ID is required.");}var outcome=manager.GetOutcome(transaction);JsonSerializer.Serialize(writer,new{transactionId=outcome.Id,outcome=outcome.Kind.ToString(),lsn=outcome.Lsn});
                }else if(path=="/api/databases"){JsonSerializer.Serialize(writer,sqlEngine.Databases.Databases.Where(d=>protection is null||protection.CanDiscover(operation!,d.Name)).Select(d=>new{d.Name,d.IsReadOnly}));}
                else if(path=="/api/tables"&&req.HttpMethod=="GET"){
                    var tables=session.Run(()=>{
                        using var binding=DatabaseRegistry.EnterSqlSession(session);using var read=DatabaseRegistry.ReadSqlDatabase(adapter,session);
                        return adapter.GetTables().Where(t=>protection is null||protection.CanAccessTable(operation!,selected,adapter,t,SecurityPermission.Discover))
                            .Select(t=>new{name=t.TableName,columns=t.Columns.Select(c=>new{c.Name,c.SqlTypeName,c.IsPrimaryKey,c.IsNullable}).ToArray()}).ToArray();
                    });
                    JsonSerializer.Serialize(writer,tables);
                }else if(path.StartsWith("/api/tables/",StringComparison.Ordinal)&&req.HttpMethod=="GET"){
                    var table=path["/api/tables/".Length..];using var binding=DatabaseRegistry.EnterSqlSession(session);
                    using var read=protection is null?null:DatabaseRegistry.ReadSqlDatabase(adapter,session);
                    if(protection is not null){
                        var metadata=adapter.FindTable(table);
                        if(metadata is null||!protection.CanAccessTable(operation!,selected,adapter,metadata,SecurityPermission.Read)){
                            throw new HttpSessionException(403,"The identity is not permitted to read this table.");
                        }
                    }
                    var result=adapter.ExecuteSelect(table,null,null,session);await WriteResultAsync(writer,result,cancellationToken,table);
                }else if(path=="/api/sql"&&req.HttpMethod=="POST"){
                    var body=req.InputStream;
                    if(budgetedBody&&req.ContentLength is null){
                        body=new BudgetedRequestBody(body,bodyReservation,sharedBodyReservation,bodyLimit);
                    }
                    using var input=new StreamReader(body,req.ContentEncoding);using var json=JsonDocument.Parse(await input.ReadToEndAsync(cancellationToken));
                    if(!json.RootElement.TryGetProperty("query",out var query)||query.ValueKind!=JsonValueKind.String){throw new HttpSessionException(400,"A string 'query' property is required.");}
                    IReadOnlyList<QueryResult> results;
                    if(protection is null){results=sqlEngine.ExecuteBatch(query.GetString()!,session);}
                    else{
                        if(query.GetString() is not{} sql||string.IsNullOrWhiteSpace(sql)){throw new HttpSessionException(400,"A non-empty SQL query is required.");}
                        var catalog=new HttpAuthorizedSqlCatalog(sqlEngine.Databases,protection,operation!);
                        try{
                            var statement=SqlExecutionEngine.ParseAuthorizedStatement(sql);
                            if(statement is BeginTransactionStatement{Distributed:false,MarkDefined:false,Name:null}){
                                if(lease is null||beginTransaction is null||session.IsInTransaction||sqlEngine.Databases.GatewayTransactions is null){
                                    throw new HttpSessionException(409,"Starting a protected transaction requires an idle HTTP session and an enabled Gateway transaction owner.");
                                }
                                var authorized=await beginTransaction()??throw new HttpSessionException(503,"Transaction authorization is unavailable.");
                                try{
                                    if(!protection.CanDiscover(authorized.Lease,selected)){throw new HttpSessionException(403,"The identity is not permitted to discover this database.");}
                                    using var request=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,authorized.Lease.CancellationToken);
                                    session.CancellationToken=request.Token;
                                    request.Token.ThrowIfCancellationRequested();
                                    results=[sqlEngine.Execute(sql,session)];
                                    lease.Entry.AttachAuthorization(authorized.Lease,authorized.Proof);
                                }catch{try{if(session.IsInTransaction){session.Dispose();}}finally{authorized.Lease.Dispose();}throw;}
                                finally{session.CancellationToken=cancellationToken;}
                            }else if(continuedSession is not null&&(statement is CommitTransactionStatement{ Name:null } or RollbackTransactionStatement{ Name:null })){
                                cancellationToken.ThrowIfCancellationRequested();results=[sqlEngine.Execute(sql,session)];
                            }else{results=[sqlEngine.ExecuteAuthorizedStatement(sql,statement,session,catalog,catalog.DemandMutation)];}
                        }
                        catch(SqlQueryException error)when(error.Number is 208 or 40515){throw new HttpSessionException(403,"This SQL operation or target is unavailable in protected HTTP.");}
                        catch(SqlQueryException error)when(error.Number is 102 or 103){throw new HttpSessionException(400,error.Message);}
                    }
                    using var binding=DatabaseRegistry.EnterSqlSession(session);
                    if(results.Count==1){await WriteResultAsync(writer,results[0],cancellationToken,null,lease is null?null:token,session);}
                    else{writer.WriteStartObject();writer.WritePropertyName("results");writer.WriteStartArray();foreach(var result in results){await WriteResultAsync(writer,result,cancellationToken,null);}writer.WriteEndArray();if(lease is not null){writer.WritePropertyName("session");WriteSession(writer,token!,session);}writer.WriteEndObject();}
                }else{throw new HttpSessionException(404,"Not Found");}
                await writer.FlushAsync(cancellationToken);
            }
            output.Position=0;res.ContentType="application/json; charset=utf-8";res.ContentLength64=output.Length;responseStarted=true;await res.CopyResponseAsync(output,cancellationToken);
        }catch(Exception error){
            var bodyTooLarge=error is BadHttpRequestException{StatusCode:StatusCodes.Status413PayloadTooLarge}
                or HttpSessionException{Status:StatusCodes.Status413PayloadTooLarge};
            if(!bodyTooLarge&&lease is not null&&error is IOException or OperationCanceledException or global::System.Net.HttpListenerException){sessions!.Delete(token!,lease);}
            if(responseStarted){res.Abort();throw;}
            if(bodyTooLarge){res.CloseHttp1AfterResponse();}
            res.StatusCode=error is HttpSessionException http?http.Status:bodyTooLarge?StatusCodes.Status413PayloadTooLarge:500;
            var completion=error as global::Seamless.Transactions.TransactionCommitException;
            await WriteJsonAsync(res,new{error=error.Message,number=(error as SqlQueryException)?.Number,transactionId=completion?.TransactionId,outcome=completion?.Outcome.ToString(),session=lease is null?null:State(token!,lease.Session)},cancellationToken);
        }finally{try{transient?.Dispose();}finally{try{if(lease is not null&&!lease.Session.IsInTransaction){lease.Entry.ReleaseAuthorization();}lease?.Dispose();}finally{res.Close();}}}
    }
    private static void ReserveBody(ResourceReservation? reservation,FrameBufferQuota.GrowingReservation? shared,long bytes,long limit){
        if(bytes>limit){
            throw new HttpSessionException(StatusCodes.Status413PayloadTooLarge,"The HTTP request body exceeds its size limit.");
        }
        try{reservation?.Resize(bytes);}
        catch(ResourceLimitException){
            throw new HttpSessionException(StatusCodes.Status503ServiceUnavailable,"The HTTP request body budget is in use.");
        }
        if(shared is not null&&!shared.TryGrowTo(bytes)){
            throw new HttpSessionException(StatusCodes.Status503ServiceUnavailable,"The Gateway request budget is in use.");
        }
    }

    // Keep a small completed response in the execution budget. Larger responses use the same
    // temporary storage as other endpoints, so serialization errors can still precede headers.
    private sealed class DatabaseListBuffer(ExecutionScope scope):Stream{
        private const int MemoryCapacity=4096;
        private OwnedBuffer? memory=TryRent(scope);
        private TemporaryFile? file;
        private int length;

        private static OwnedBuffer? TryRent(ExecutionScope scope){
            try{return scope.Rent(MemoryCapacity);}
            catch(ResourceLimitException){return null;}
        }
        private TemporaryFile? AppendInMemory(ReadOnlySpan<byte> bytes){
            scope.ThrowIfUnavailable();
            if(this.file is not null){return this.file;}
            if(this.memory is{} buffer&&bytes.Length<=MemoryCapacity-this.length){
                bytes.CopyTo(buffer.Memory.Span[this.length..]);
                this.length+=bytes.Length;
                return null;
            }
            var spill=this.file=scope.CreateTemporaryFile();
            if(this.memory is{} prefix){
                spill.Write(prefix.Memory.Span[..this.length]);
                prefix.Dispose();
                this.memory=null;
            }
            return spill;
        }
        public Task SendAsync(Exchange response,CancellationToken token){
            token.ThrowIfCancellationRequested();
            if(this.file is{} spill){
                spill.Position=0;
                return response.CopyResponseAsync(spill,token);
            }
            return response.WriteResponseAsync(this.memory!.Memory[..this.length],token);
        }
        public override bool CanRead=>false;
        public override bool CanSeek=>false;
        public override bool CanWrite=>true;
        public override long Length=>this.file?.Length??this.length;
        public override long Position{
            get=>this.Length;
            set=>throw new NotSupportedException();
        }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Flush()=>this.file?.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken){
            cancellationToken.ThrowIfCancellationRequested();
            return this.file?.FlushAsync(cancellationToken)??Task.CompletedTask;
        }
        public override void Write(byte[] buffer,int offset,int count)=>this.Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer)=>this.AppendInMemory(buffer)?.Write(buffer);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default){
            cancellationToken.ThrowIfCancellationRequested();
            return this.AppendInMemory(buffer.Span)?.WriteAsync(buffer,cancellationToken)??ValueTask.CompletedTask;
        }
        protected override void Dispose(bool disposing){
            try{
                if(disposing){
                    try{this.file?.Dispose();}
                    finally{this.memory?.Dispose();}
                }
            }finally{base.Dispose(disposing);}
        }
    }

    private sealed class BudgetedRequestBody(Stream source,ResourceReservation? reservation,FrameBufferQuota.GrowingReservation? shared,long limit):Stream{
        public override bool CanRead=>source.CanRead;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default){
            var count=await source.ReadAsync(buffer,cancellationToken);
            if(count!=0){ReserveBody(reservation,shared,checked((reservation?.Bytes??shared!.Bytes)+count),limit);}
            return count;
        }
        public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken cancellationToken)=>this.ReadAsync(buffer.AsMemory(offset,count),cancellationToken).AsTask();
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
    // 旧公開HttpListenerオーバーロードとKestrel入口で同じ要求処理を使う。
    private sealed class Exchange{
        private readonly HttpContext? current;
        private readonly global::System.Net.HttpListenerContext? legacy;
        private readonly FrameBufferQuota? pendingWrites;
        private readonly TimeSpan? responseWriteTimeout;
        public Exchange(HttpContext context,FrameBufferQuota? pendingWrites,TimeSpan? responseWriteTimeout){this.current=context;this.pendingWrites=pendingWrites;this.responseWriteTimeout=responseWriteTimeout;}
        public Exchange(global::System.Net.HttpListenerContext context){this.legacy=context;}
        public string HttpMethod{get{if(this.current is not null){return this.current.Request.Method;}return this.legacy!.Request.HttpMethod;}}
        public string Path{get{if(this.current is not null){return this.current.Request.Path.Value??"";}return this.legacy!.Request.Url?.AbsolutePath??"";}}
        public string? Header(string name){if(this.current is not null){if(this.current.Request.Headers.TryGetValue(name,out var value)){return value.ToString();}return null;}return this.legacy!.Request.Headers[name];}
        public string? Query(string name){if(this.current is not null){if(this.current.Request.Query.TryGetValue(name,out var value)){return value.ToString();}return null;}return this.legacy!.Request.QueryString[name];}
        public Stream InputStream{get{if(this.current is not null){return this.current.Request.Body;}return this.legacy!.Request.InputStream;}}
        public long? ContentLength=>this.current?.Request.ContentLength;
        public Encoding ContentEncoding{get{
            if(this.current is null){return this.legacy!.Request.ContentEncoding;}
            if(global::System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(this.current.Request.ContentType,out var media)&&!string.IsNullOrEmpty(media.CharSet)){return Encoding.GetEncoding(media.CharSet.Trim('"'));}
            return Encoding.UTF8;
        }}
        private Stream OutputStream{get{if(this.current is not null){return this.current.Response.Body;}return this.legacy!.Response.OutputStream;}}
        private int ChunkSize=>(int)Math.Min(16*1024,this.pendingWrites?.MaximumBytes??long.MaxValue);
        public async Task CopyResponseAsync(Stream source,CancellationToken token){
            if(this.pendingWrites is null&&this.responseWriteTimeout is null){await source.CopyToAsync(this.OutputStream,token);return;}
            var size=(int)Math.Min(source.Length-source.Position,this.ChunkSize);
            if(size==0){return;}
            using var reservation=this.ReserveWrite(size);
            var buffer=GC.AllocateUninitializedArray<byte>(size);
            while(true){
                var read=await source.ReadAsync(buffer,token);
                if(read==0){return;}
                await this.WriteChunkAsync(buffer.AsMemory(0,read),token);
            }
        }
        public async Task WriteResponseAsync(ReadOnlyMemory<byte> bytes,CancellationToken token){
            if(this.pendingWrites is null&&this.responseWriteTimeout is null){await this.OutputStream.WriteAsync(bytes,token);return;}
            while(!bytes.IsEmpty){
                var size=Math.Min(bytes.Length,this.ChunkSize);
                using var reservation=this.ReserveWrite(size);
                await this.WriteChunkAsync(bytes[..size],token);
                bytes=bytes[size..];
            }
        }
        private IDisposable? ReserveWrite(int bytes){
            if(this.pendingWrites is null){return null;}
            var reservation=this.pendingWrites.TryReserve(bytes);
            if(reservation is not null){return reservation;}
            this.Abort();
            throw new IOException("The pending HTTP response write limit has been reached.");
        }
        private async ValueTask WriteChunkAsync(ReadOnlyMemory<byte> bytes,CancellationToken token){
            using var deadline=this.responseWriteTimeout.HasValue?CancellationTokenSource.CreateLinkedTokenSource(token):null;
            if(this.responseWriteTimeout is{} timeout){deadline!.CancelAfter(timeout);}
            try{await this.OutputStream.WriteAsync(bytes,deadline?.Token??token);}
            catch(OperationCanceledException error)when(deadline?.IsCancellationRequested==true&&!token.IsCancellationRequested){
                this.Abort();
                throw new IOException("The HTTP response write has timed out.",error);
            }
        }
        public int StatusCode{set{if(this.current is not null){this.current.Response.StatusCode=value;}else{this.legacy!.Response.StatusCode=value;}}}
        public string ContentType{set{if(this.current is not null){this.current.Response.ContentType=value;}else{this.legacy!.Response.ContentType=value;}}}
        public long ContentLength64{set{if(this.current is not null){this.current.Response.ContentLength=value;}else{this.legacy!.Response.ContentLength64=value;}}}
        public void CloseHttp1AfterResponse(){
            if(this.current is{} context&&context.Request.Protocol is "HTTP/1.0" or "HTTP/1.1"){context.Response.Headers.Connection="close";}
        }
        public void SetLegacyCorsHeaders(){
            if(this.legacy is null){return;}
            var headers=this.legacy.Response.Headers;
            headers.Add("Access-Control-Allow-Origin",CorsOrigin);
            headers.Add("Access-Control-Allow-Methods",CorsMethods);
            headers.Add("Access-Control-Allow-Headers",CorsHeaders);
        }
        public void Abort(){if(this.current is not null){this.current.Abort();}else{this.legacy!.Response.Abort();}}
        public void Close(){this.legacy?.Response.Close();}
    }
    private static void ValidateTransaction(string? expected,SessionContext? session){
        var actual=session?.Transaction?.Id;
        if(actual is not null){if(!Guid.TryParse(expected,out var id)||id!=actual){throw new HttpSessionException(409,"The request must identify the session's active transaction in X-Seamless-Transaction.");}}
        else if(!string.IsNullOrEmpty(expected)){throw new HttpSessionException(409,"The request's transaction is no longer active.");}
    }
    private static object State(string token,SessionContext session)=>new{id=token,database=session.DatabaseName,transactionId=session.Transaction?.Id,transactionCount=session.TransactionCount,transactionState=session.TransactionState};
    private static void WriteSession(Utf8JsonWriter writer,string token,SessionContext session)=>JsonSerializer.Serialize(writer,State(token,session));
    private static async Task WriteResultAsync(Utf8JsonWriter writer,QueryResult result,CancellationToken ct,string? table,string? token=null,SessionContext? session=null){
        writer.WriteStartObject();if(table is not null){writer.WriteString("table",table);}writer.WritePropertyName("columns");JsonSerializer.Serialize(writer,result.Columns.Select(c=>c.Name));writer.WritePropertyName("rows");writer.WriteStartArray();long count=0;
        foreach(var row in result.Rows){ct.ThrowIfCancellationRequested();writer.WriteStartObject();for(var index=0;index<row.Length;index++){writer.WritePropertyName(result.Columns[index].Name);JsonSerializer.Serialize(writer,row[index]);}writer.WriteEndObject();await writer.FlushAsync(ct);count++;}
        writer.WriteEndArray();writer.WriteNumber("total",count);writer.WriteNumber("rowsAffected",result.RowsAffected);if(token is not null){writer.WritePropertyName("session");WriteSession(writer,token,session!);}writer.WriteEndObject();await writer.FlushAsync(ct);
    }
    private static async Task WriteJsonAsync(Exchange res,object data,CancellationToken ct){res.ContentType="application/json; charset=utf-8";var bytes=JsonSerializer.SerializeToUtf8Bytes(data);res.ContentLength64=bytes.Length;await res.WriteResponseAsync(bytes,ct);}

    private static async Task ServeHtmlAsync(Exchange res,string dbName,CancellationToken ct){
        res.ContentType="text/html; charset=utf-8";
        var html=$@"<!DOCTYPE html>
<html lang=""ja"">
<head>
    <meta charset=""UTF-8"">
    <title>SeamlessDB Web UI - {dbName}</title>
    <style>
        body {{ font-family: 'Segoe UI', Meiryo, sans-serif; margin: 20px; background: #f8f9fa; color: #333; }}
        h1 {{ color: #0066cc; margin-bottom: 5px; }}
        .header {{ margin-bottom: 20px; border-bottom: 2px solid #0066cc; padding-bottom: 10px; }}
        .tables-bar {{ margin-bottom: 15px; display: flex; gap: 8px; flex-wrap: wrap; }}
        .btn {{ padding: 6px 14px; border: 1px solid #ccc; background: #fff; cursor: pointer; border-radius: 4px; font-size: 14px; }}
        .btn:hover {{ background: #e9ecef; }}
        .btn-primary {{ background: #0066cc; color: #fff; border-color: #0066cc; }}
        .btn-primary:hover {{ background: #0052a3; }}
        .sql-box {{ width: 100%; height: 80px; font-family: Consolas, monospace; font-size: 14px; padding: 8px; box-sizing: border-box; border: 1px solid #ccc; border-radius: 4px; }}
        .controls {{ margin: 10px 0 20px 0; }}
        table {{ border-collapse: collapse; width: 100%; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
        th, td {{ border: 1px solid #dee2e6; padding: 8px 12px; text-align: left; font-size: 14px; }}
        th {{ background: #f1f3f5; font-weight: bold; }}
        tr:nth-child(even) {{ background: #fdfdfd; }}
        .status {{ margin-top: 8px; color: #666; font-size: 13px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>SeamlessDB Web UI</h1>
        <div>Database: <strong>{dbName}</strong> | TDS Port: <strong>14333</strong> | HTTP Port: <strong>8088</strong></div>
    </div>
    <div class=""tables-bar"" id=""tableButtons""></div>
    <textarea class=""sql-box"" id=""sqlInput"">SELECT * FROM 商品;</textarea>
    <div class=""controls"">
        <button class=""btn btn-primary"" onclick=""runSql()"">SQL実行</button>
        <span class=""status"" id=""statusText""></span>
    </div>
    <div id=""tableContainer""></div>
    <script>
        async function loadTables() {{
            const res = await fetch('/api/tables');
            const tables = await res.json();
            const bar = document.getElementById('tableButtons');
            bar.innerHTML = '<strong>テーブル:</strong> ';
            tables.forEach(t => {{
                const btn = document.createElement('button');
                btn.className = 'btn';
                btn.textContent = t.name;
                btn.onclick = () => {{
                    document.getElementById('sqlInput').value = 'SELECT * FROM ' + t.name + ';';
                    runSql();
                }};
                bar.appendChild(btn);
            }});
        }}
        async function runSql() {{
            const sql = document.getElementById('sqlInput').value;
            const status = document.getElementById('statusText');
            status.textContent = '実行中...';
            try {{
                const res = await fetch('/api/sql', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify({{ query: sql }})
                }});
                const data = await res.json();
                if(data.error) {{
                    status.textContent = 'エラー: ' + data.error;
                    document.getElementById('tableContainer').innerHTML = '';
                    return;
                }}
                status.textContent = '完了: ' + data.rows.length + ' 件取得 (影響行数: ' + data.rowsAffected + ')';
                renderTable(data.columns, data.rows);
            }} catch(err) {{
                status.textContent = '通信エラー: ' + err.message;
            }}
        }}
        function renderTable(cols, rows) {{
            if(!cols || cols.length === 0) {{
                document.getElementById('tableContainer').innerHTML = '<p>結果セットはありません</p>';
                return;
            }}
            let html = '<table><thead><tr>';
            cols.forEach(c => html += '<th>' + c + '</th>');
            html += '</tr></thead><tbody>';
            rows.forEach(r => {{
                html += '<tr>';
                cols.forEach(c => html += '<td>' + (r[c] !== null && r[c] !== undefined ? r[c] : '<em>NULL</em>') + '</td>');
                html += '</tr>';
            }});
            html += '</tbody></table>';
            document.getElementById('tableContainer').innerHTML = html;
        }}
        loadTables();
        runSql();
    </script>
</body>
</html>";

        var bytes=Encoding.UTF8.GetBytes(html);
        res.ContentLength64=bytes.Length;
        await res.WriteResponseAsync(bytes,ct);
    }
}
