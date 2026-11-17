using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Seamless.Authentication;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Transport.Kestrel;
using Seamless.Security;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Http;

public sealed class HttpServer:IDisposable{
    private readonly int port;
    private IDatabaseAdapter adapter=>this.sqlEngine.Databases.Resolve(this.sqlEngine.Databases.DefaultDatabaseName).Adapter;
    private readonly SqlExecutionEngine sqlEngine;
    private readonly HttpServerOptions options;
    private readonly HttpGatewayAuthorization? authorization;
    private readonly ResourceBudget? requestBodyBudget;
    private readonly FrameBufferQuota? sharedRequestBodies;
    private readonly ConnectionQuota? sharedHandshakes;
    private readonly FrameBufferQuota? pendingWrites;
    private readonly ConnectionQuota? sharedConnections;
    private HttpSessionStore? httpSessions;
    private Exception? lastSessionCleanupError;
    private KestrelHttpHost? listener;
    private CancellationTokenSource? cts;
    public HttpServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine):this(port,adapter,sqlEngine,new()){}
    public HttpServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,HttpServerOptions options):this(port,adapter,sqlEngine,options,null){}
    public HttpServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,HttpServerOptions options,HttpGatewayAuthorization? authorization):this(port,adapter,sqlEngine,options,authorization,null){}
    internal HttpServer(int port,IDatabaseAdapter adapter,SqlExecutionEngine sqlEngine,HttpServerOptions options,HttpGatewayAuthorization? authorization,FrameBufferQuota? pendingWrites,ConnectionQuota? sharedConnections=null,FrameBufferQuota? sharedRequestBodies=null,ConnectionQuota? sharedHandshakes=null){
        ArgumentOutOfRangeException.ThrowIfNegative(port);ArgumentOutOfRangeException.ThrowIfGreaterThan(port,65535);
        if(options.MaximumRequestBodyBytes is{} maximum){ArgumentOutOfRangeException.ThrowIfNegative(maximum,nameof(options.MaximumRequestBodyBytes));}
        if(options.MaximumBufferedRequestBodyBytes is{} bufferedMaximum){
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferedMaximum,nameof(options.MaximumBufferedRequestBodyBytes));
            this.requestBodyBudget=new(bufferedMaximum,0);
        }
        if(options.MaximumConcurrentRequests is{} limit){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit,nameof(options.MaximumConcurrentRequests));}
        if(options.MaximumPendingWriteBytes is{} writeBytes){
            if(pendingWrites is not null){throw new ArgumentException("Choose the shared Gateway write limit or the HTTP-only limit.",nameof(options));}
            pendingWrites=new(writeBytes);
        }
        this.pendingWrites=pendingWrites;
        this.sharedConnections=sharedConnections;
        this.sharedRequestBodies=sharedRequestBodies;
        this.sharedHandshakes=sharedHandshakes;
        if(options.ResponseWriteTimeout is{} timeout&&(timeout<=TimeSpan.Zero||timeout.TotalMilliseconds>uint.MaxValue-1)){throw new ArgumentOutOfRangeException(nameof(options.ResponseWriteTimeout));}
        if(authorization is not null){
            if(options.Certificate is null){throw new ArgumentException("Protected HTTP requires an HTTPS certificate.",nameof(options));}
        }
        this.authorization=authorization;
        this.port=port;this.sqlEngine=sqlEngine;this.options=options;
        if(sqlEngine.Databases.GatewayTransactions is{} manager){this.options=options with{Execution=manager.LinkExecutionOptions(options.Execution)};}
    }
    public int Port=>this.listener?.Port??this.port;
    public Exception? LastSessionCleanupError=>this.httpSessions?.LastCleanupError??this.lastSessionCleanupError;
    public void Start(){
        if(this.listener is not null){return;}
        this.sqlEngine.Databases.GatewayTransactions?.Recover();this.cts=new();this.httpSessions=new(this.options,this.authorization?.SupportsTransactions==true);
        var host=new KestrelHttpHost(new(this.options.ListenAddress,this.port),this.ServeAsync,this.options.Certificate,this.options.MaximumRequestBodyBytes,this.options.MaximumConcurrentRequests,this.options.MaximumConnections){SharedConnectionQuota=this.sharedConnections,SharedTlsHandshakeQuota=this.sharedHandshakes};
        try{host.StartAsync().GetAwaiter().GetResult();this.listener=host;}
        catch{host.DisposeAsync().AsTask().GetAwaiter().GetResult();this.httpSessions.Dispose();this.httpSessions=null;this.cts.Dispose();this.cts=null;throw;}
    }
    public async Task StopAsync(){
        if(this.listener is null){return;}
        this.cts!.Cancel();
        try{await this.listener.DisposeAsync();}
        finally{this.httpSessions?.Dispose();this.lastSessionCleanupError=this.httpSessions?.LastCleanupError;this.httpSessions=null;this.listener=null;this.cts.Dispose();this.cts=null;}
    }
    public void Dispose()=>this.StopAsync().GetAwaiter().GetResult();
    private async Task ServeAsync(HttpContext context){
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(this.cts!.Token,context.RequestAborted);
        var headers=context.Response.Headers;
        headers.AccessControlAllowOrigin=HttpEndpoints.CorsOrigin;
        headers.AccessControlAllowMethods=HttpEndpoints.CorsMethods;
        headers.AccessControlAllowHeaders=HttpEndpoints.CorsHeaders;
        if(this.authorization is not null){
            headers.CacheControl="no-store";
            headers.AccessControlExposeHeaders="WWW-Authenticate";
        }
        try{
            if(this.authorization is{} protection&&!HttpMethods.IsOptions(context.Request.Method)){
                var path=context.Request.Path.Value;
                var allowedGet=HttpMethods.IsGet(context.Request.Method)&&(path=="/api/databases"||protection.SupportsTableAuthorization&&HttpEndpoints.IsTablePath(path));
                var allowedSql=protection.SupportsSqlAuthorization&&path=="/api/sql"&&HttpMethods.IsPost(context.Request.Method);
                var bearer=Bearer(context);
                var sessionToken=context.Request.Headers["X-Seamless-Session"];
                var transactionId=context.Request.Headers["X-Seamless-Transaction"];
                if(protection.SupportsTransactions&&bearer is not null&&sessionToken.Count==1&&transactionId.Count==1
                    &&Guid.TryParse(transactionId[0],out var id)&&(allowedSql||path=="/api/session"&&(HttpMethods.IsGet(context.Request.Method)||HttpMethods.IsDelete(context.Request.Method)))){
                    using var continued=await this.httpSessions!.TryAcquireTransactionAsync(sessionToken[0]!,id,BearerProof(bearer),lifetime.Token);
                    if(continued is not null){
                        var transaction=continued.Entry.TransactionAuthorization!.Authorization;
                        if(transaction.CancellationToken.IsCancellationRequested){this.httpSessions.Delete(sessionToken[0]!,continued);context.Response.StatusCode=StatusCodes.Status409Conflict;return;}
                        using var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,transaction.CancellationToken);
                        try{await Dispatch(transaction,request.Token,continued:continued);}
                        catch(OperationCanceledException)when(request.IsCancellationRequested){context.Abort();}
                        return;
                    }
                }
                var authenticated=await AuthenticateAsync(context,protection,bearer,lifetime.Token);
                if(authenticated is not{} access){return;}
                using var operation=access.Operation;
                using var authorizedRequest=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,operation.CancellationToken);
                if(path!="/api/session"&&!allowedGet&&!allowedSql){
                    context.Response.StatusCode=StatusCodes.Status403Forbidden;
                    return;
                }
                try{await Dispatch(operation,authorizedRequest.Token,protection.SupportsTransactions?BeginTransaction:null);}
                catch(OperationCanceledException)when(authorizedRequest.IsCancellationRequested){context.Abort();}
                return;

                Task Dispatch(AuthorizationLease authorized,CancellationToken token,Func<ValueTask<(AuthorizationLease Lease,byte[] Proof)?>>? begin=null,HttpSessionStore.Lease? continued=null)
                    =>HttpEndpoints.HandleRequestAsync(context,this.adapter,this.sqlEngine,this.httpSessions,token,protection,authorized,
                        requestBodyBudget:this.requestBodyBudget,maximumRequestBodyBytes:this.options.MaximumRequestBodyBytes,
                        pendingWrites:this.pendingWrites,responseWriteTimeout:this.options.ResponseWriteTimeout,beginTransaction:begin,continuedSession:continued,sharedRequestBodies:this.sharedRequestBodies);
                async ValueTask<(AuthorizationLease Lease,byte[] Proof)?> BeginTransaction(){
                    operation.ThrowIfCancellationRequested();authorizedRequest.Token.ThrowIfCancellationRequested();
                    var proof=BearerProof(bearer!);
                    var transaction=await protection.BeginTransactionAsync(access.Identity,authorizedRequest.Token);
                    return transaction is null?null:(transaction,proof);
                }
            }
            await HttpEndpoints.HandleRequestAsync(context,this.adapter,this.sqlEngine,this.httpSessions,lifetime.Token,
                requestBodyBudget:this.requestBodyBudget,maximumRequestBodyBytes:this.options.MaximumRequestBodyBytes,
                pendingWrites:this.pendingWrites,responseWriteTimeout:this.options.ResponseWriteTimeout,sharedRequestBodies:this.sharedRequestBodies);
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}catch(IOException){}catch(ObjectDisposedException)when(lifetime.IsCancellationRequested){}
    }

    private static byte[] BearerProof(string bearer)=>SHA256.HashData(Encoding.UTF8.GetBytes(bearer));

    private static string? Bearer(HttpContext context){
        var values=context.Request.Headers.Authorization;
        return values.Count==1&&AuthenticationHeaderValue.TryParse(values[0],out var value)&&value.Scheme.Equals("Bearer",StringComparison.OrdinalIgnoreCase)
            &&value.Parameter is{Length:>0 and <=65536} bearer?bearer:null;
    }
    private static async Task<(AuthenticatedIdentity Identity,AuthorizationLease Operation)?> AuthenticateAsync(HttpContext context,HttpGatewayAuthorization protection,string? bearer,CancellationToken token){
        if(bearer is not null){
            try{
                var identity=await protection.Tokens.AuthenticateAsync(bearer,token);
                var operation=await protection.BeginOperationAsync(identity,token);
                if(operation is null){context.Response.StatusCode=StatusCodes.Status503ServiceUnavailable;}
                return operation is null?null:(identity,operation);
            }catch(OidcAuthenticationException error)when(error.Code is "IdentityProviderUnavailable" or "MembershipUnavailable" or OidcAuthenticationException.BusyCode){
                context.Response.StatusCode=StatusCodes.Status503ServiceUnavailable;
                return null;
            }catch(OidcAuthenticationException){
            }catch(AuthorizationException error)when(error.Code=="AccountDisabled"){
                context.Response.StatusCode=StatusCodes.Status403Forbidden;
                return null;
            }catch(AuthorizationException){
                context.Response.StatusCode=StatusCodes.Status503ServiceUnavailable;
                return null;
            }
        }
        context.Response.StatusCode=StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate="Bearer";
        return null;
    }
}
