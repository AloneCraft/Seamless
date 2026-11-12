using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MemoryPack;
using Seamless.Authentication;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Native;
using Seamless.Security;

namespace Management.Tests;

public sealed partial class NativeTls{
    internal static async Task<int> ServePolicyProbeAsync(int maximumConnections,int identityCount,bool mixed,bool protectTds){
        if(!Console.IsInputRedirected||!Console.IsOutputRedirected){return 2;}
        try{
            await ManagementTransport.WithServerCertificate(async certificate=>{
                await using var issuer=await OidcDevelopmentIssuer.StartAsync();
                using var database=new Fixture(null,start:false);
                var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var largeId=Guid.NewGuid();
                var resources=new List<SecurityResource>{new(serverId,null,SecurityResourceKind.Server,"probe"),new(databaseId,serverId,SecurityResourceKind.Database,"fixture"),
                    new(largeId,databaseId,SecurityResourceKind.Query,"large")};
                var roles=new List<SecurityRole>();
                var bindings=new List<RoleBinding>();
                var credentials=new List<PolicyProbeCredential>();
                var queryIds=new Dictionary<string,Guid>(StringComparer.Ordinal){{"large",largeId}};
                var queries=new List<NativeQueryDefinition>{Definition<byte[]>("large")};
                for(var index=0;index<identityCount;index++){
                    var subject="dev-user";var query="small";
                    if(identityCount>1){subject+="-"+index;query+="-"+index;}
                    var queryId=Guid.NewGuid();var roleId=Guid.NewGuid();
                    resources.Add(new(queryId,databaseId,SecurityResourceKind.Query,query));
                    roles.Add(new(roleId,subject,[new(databaseId,SecurityPermission.Discover),new(queryId,SecurityPermission.Execute),new(largeId,SecurityPermission.Execute)]));
                    bindings.Add(new(new(issuer.Id,subject),SecurityPrincipalKind.User,roleId));
                    credentials.Add(new(subject,query,issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject,scope:"seamless.database")));
                    queryIds.Add(query,queryId);
                    queries.Add(Definition<int>(query));
                }
                using var manager=new AuthorizationManager(new Seamless.Security.AuthorizationPolicySnapshot(1,resources,roles,bindings));
                using var verifier=OidcTokenVerifier.ForServer(issuer.Settings,32);
                var module=new PolicyProbeModule();
                var sessions=0;
                var activeSessions=0;
                var membershipRefreshes=0;
                var sessionsRefreshedTwice=0;
                var deniedQueries=0;
                var authorization=new NativeGatewayAuthorization(verifier,(identity,_)=>{
                    var session=manager.CreateSession(identity);
                    Interlocked.Increment(ref sessions);
                    Interlocked.Increment(ref activeSessions);
                    return ValueTask.FromResult<IAuthorizationSession>(new TrackedPolicyProbeSession(session,
                        ()=>Interlocked.Decrement(ref activeSessions),count=>{
                            Interlocked.Increment(ref membershipRefreshes);
                            if(count==2){Interlocked.Increment(ref sessionsRefreshedTwice);}
                        }));
                },(lease,request)=>{
                    if(request.Database!="fixture"){throw new AuthorizationException("UnknownResource","Unknown probe database.");}
                    lease.Demand(databaseId,SecurityPermission.Discover);
                },(lease,request,implementation,_)=>{
                    if(!ReferenceEquals(module,implementation)||!queryIds.TryGetValue(request.QueryId,out var queryId)){throw new AuthorizationException("UnapprovedModule","Unknown probe module or query.");}
                    try{lease.Demand(queryId,SecurityPermission.Execute);}
                    catch(AuthorizationException error)when(error.Code=="NotGranted"){
                        Interlocked.Increment(ref deniedQueries);
                        throw;
                    }
                },static(_,bytes)=>NativeWireValidation.Validate<int>(bytes.Span));
                var sqlAuthorization=new HttpGatewayAuthorization(verifier,manager,name=>name=="fixture"?databaseId:null);
                var mixedLimit=maximumConnections+(protectTds?2:1);
                using var gateway=mixed?new GatewayServer(database.Registry,new(){
                    TdsEnabled=protectTds,TdsPort=0,NativePort=0,HttpPort=0,MaximumConnections=mixedLimit,MaximumConcurrentTlsHandshakes=32,
                    TdsAuthorization=protectTds?new(certificate,sqlAuthorization,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity))):null,
                    NativeCertificate=certificate,NativeAuthorization=authorization,NativeAuthenticatedLimits=new(){MaximumConnections=maximumConnections+1},
                    Http=new(){Certificate=certificate,ListenAddress=IPAddress.Loopback}
                },sqlAuthorization):null;
                using var standalone=gateway is null?NativeQueryServer.CreateAuthenticated(database.Registry,certificate,authorization,new(){MaximumConnections=maximumConnections},0):null;
                var server=gateway?.Native??standalone!;
                var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
                server.Register(new("probe","","","",version,queries),module,version);
                if(gateway is null){server.Start();}
                else{gateway.Start();}
                // 子プロセス専用のリダイレクトへ一度だけ渡す。呼び出し元は記録しない。
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new{
                    server.Port,MaximumConnections=maximumConnections,HttpsPort=gateway?.HttpPort??0,TdsPort=gateway?.TdsPort??0,
                    Certificate=certificate.Export(X509ContentType.Cert),AuthorityId=issuer.Id,Credentials=credentials,Fingerprint
                }));
                while(await Console.In.ReadLineAsync() is{} command){
                    if(command=="stop"){break;}
                    if(command!="snapshot"){continue;}
                    using var process=Process.GetCurrentProcess();
                    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new{
                        PrivateBytes=process.PrivateMemorySize64,WorkingSet=process.WorkingSet64,Allocated=GC.GetTotalAllocatedBytes(),HeapBytes=GC.GetTotalMemory(false),
                        Gen0=GC.CollectionCount(0),Gen1=GC.CollectionCount(1),Gen2=GC.CollectionCount(2),CpuMs=process.TotalProcessorTime.TotalMilliseconds,
                        Threads=process.Threads.Count,AuthenticatedSessions=Volatile.Read(ref sessions),DeniedPrivateQueries=Volatile.Read(ref deniedQueries),
                        ActiveAuthenticatedSessions=Volatile.Read(ref activeSessions),MembershipRefreshes=Volatile.Read(ref membershipRefreshes),
                        SessionsRefreshedTwice=Volatile.Read(ref sessionsRefreshedTwice),IssuerMembershipChecks=issuer.MembershipChecks,
                        SharedConnections=gateway?.SharedConnectionCount
                    }));
                }
                if(gateway is null){await server.StopAsync();}
                else{await gateway.StopAsync();}
            },includeLoopbackAddress:true);
            return 0;
        }catch(Exception error){
            await Console.Error.WriteLineAsync("Authenticated policy probe server failed: "+error.GetType().Name);
            return 1;
        }
    }

    private static NativeQueryDefinition Definition<TResult>(string id)=>
        new(id,"fixture",Fingerprint,typeof(int).FullName!,typeof(TResult).FullName!){
            ArgumentsContract=NativeWireContract.For<int>(),ResultContract=NativeWireContract.For<TResult>()
        };

    private sealed record PolicyProbeCredential(string Subject,string Query,string Token);

    private sealed class TrackedPolicyProbeSession(IAuthorizationSession session,Action release,Action<int> refresh):IAuthorizationSession{
        private int disposed;
        private int refreshes;
        public SecuritySubjectId Subject=>session.Subject;
        public AuthorizationLease BeginOperation(CancellationToken cancellationToken=default)=>session.BeginOperation(cancellationToken);
        public AuthorizationLease BeginTransaction(CancellationToken cancellationToken=default)=>session.BeginTransaction(cancellationToken);
        public void UpdateValidatedIdentity(AuthenticatedIdentity identity){
            session.UpdateValidatedIdentity(identity);
            refresh(Interlocked.Increment(ref this.refreshes));
        }
        public void MarkMembershipUnavailable()=>session.MarkMembershipUnavailable();
        public void Dispose(){
            if(Interlocked.Exchange(ref this.disposed,1)!=0){return;}
            try{session.Dispose();}
            finally{release();}
        }
    }

    private sealed class PolicyProbeModule:INativeQueryModule{
        private readonly byte[] large=MemoryPackSerializer.Serialize(new byte[32768]);
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            var value=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            if(queryId=="small"||queryId.StartsWith("small-",StringComparison.Ordinal)){yield return MemoryPackSerializer.Serialize(value+1);yield break;}
            if(queryId!="large"||value is <0 or >128){throw new InvalidOperationException("Invalid probe request.");}
            for(var row=0;row<value;row++){context.CancellationToken.ThrowIfCancellationRequested();yield return this.large;}
        }
    }
}
