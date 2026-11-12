using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Authentication;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Security;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class GatewayHttpTransactionAuthorization{
    [Fact]
    public Task ActiveTransactionRetainsRevokedRightsAndDisablingRollsBack()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Where(static path=>{
            var name=Path.GetFileName(path);
            return name.StartsWith("MemoryPack",StringComparison.Ordinal)||name.StartsWith("Seamless.",StringComparison.Ordinal)
                &&!name.Contains("Generator",StringComparison.Ordinal)&&!name.StartsWith("Seamless.Management",StringComparison.Ordinal);
        }).ToArray();
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var compiler=new GeneratedAssemblyBuilder(new(Path.Combine(root,"w"),Path.Combine(root,"c"),dotnet,
            typeof(global::Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,references));
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        var definition=new DatabaseCatalog("sales",[new("dbo",[new TableCatalog("Rows",[new(0,"Id",number,false)],new("PK_Rows",[new("Id",false)],1))])],"Japanese_CI_AS");
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(root,"db"),compiler,
            new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[definition]);
        var adapter=(InProcessDatabaseAdapter)host.Databases.Resolve("sales").Adapter;
        var table=Assert.Single(adapter.GetTables());
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var schemaId=Guid.NewGuid();var tableId=Guid.NewGuid();var roleId=Guid.NewGuid();
        var subject=new SecuritySubjectId(issuer.Id,"writer");
        using var policy=new AuthorizationManager(Policy(1,true));
        var holdTransaction=false;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdOperation=false;var discoveries=0;
        var operationEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationAborted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AuthorizationLease? delayedAuthorization=null;
        var protection=new HttpGatewayAuthorization(verifier,
            BeginOperationAsync,
            name=>name=="sales"?databaseId:null,
            (target,id)=>{Interlocked.Increment(ref discoveries);return ReferenceEquals(target,adapter)&&id==table.StableObjectId?tableId:null;},
            static (_,_,_,_) => (HttpSqlObjectBinding?)null,
            BeginAuthorizationAsync);
        using var server=new HttpServer(0,adapter,new SqlExecutionEngine(host.Databases),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate},protection);
        server.Start();
        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};trust.CustomTrustStore.Add(certificate);
        using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false,SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=trust}}){
            BaseAddress=new($"https://localhost:{server.Port}"),Timeout=TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.Authorization=new("Bearer",issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject.Value,scope:"seamless.database"));
        await OpenAsync();await BeginAsync();
        using(var first=await SqlAsync("INSERT INTO dbo.Rows VALUES (1)")){Assert.Equal(HttpStatusCode.OK,first.StatusCode);}
        policy.PublishPolicy(Policy(2,false));
        using(var continued=await SqlAsync("INSERT INTO dbo.Rows VALUES (2)")){Assert.Equal(HttpStatusCode.OK,continued.StatusCode);}
        using(var commit=await SqlAsync("COMMIT TRANSACTION")){Assert.Equal(HttpStatusCode.OK,commit.StatusCode);}
        client.DefaultRequestHeaders.Remove("X-Seamless-Transaction");
        using(var refused=await SqlAsync("INSERT INTO dbo.Rows VALUES (3)")){Assert.Equal(HttpStatusCode.Forbidden,refused.StatusCode);}
        Assert.Equal(2,await CountAsync());
        using(var closed=await client.DeleteAsync("/api/session")){Assert.Equal(HttpStatusCode.OK,closed.StatusCode);}
        client.DefaultRequestHeaders.Remove("X-Seamless-Session");

        policy.PublishPolicy(Policy(3,true));
        await OpenAsync();await BeginAsync();
        using(var pending=await SqlAsync("INSERT INTO dbo.Rows VALUES (3)")){Assert.Equal(HttpStatusCode.OK,pending.StatusCode);}
        policy.SetDisabled(subject,true);
        using(var denied=await SqlAsync("COMMIT TRANSACTION")){Assert.Contains(denied.StatusCode,new[]{HttpStatusCode.Conflict,HttpStatusCode.Forbidden});}
        client.DefaultRequestHeaders.Remove("X-Seamless-Transaction");client.DefaultRequestHeaders.Remove("X-Seamless-Session");
        using(var denied=await SqlAsync("SELECT Id FROM dbo.Rows")){Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);}
        policy.SetDisabled(subject,false);
        Assert.Equal(2,await CountAsync());
        await OpenAsync();holdTransaction=true;
        using var cancel=new CancellationTokenSource();
        var waiting=client.PostAsync("/api/sql",new StringContent("{\"query\":\"BEGIN TRANSACTION\"}"),cancel.Token);
        try{
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{using var response=await waiting;});
            await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }finally{release.TrySetResult();}
        using(var closed=await client.DeleteAsync("/api/session")){Assert.Contains(closed.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.NotFound});}
        client.DefaultRequestHeaders.Remove("X-Seamless-Session");
        Assert.Equal(2,await CountAsync());
        using(var normal=await client.GetAsync("/api/tables")){Assert.Equal(HttpStatusCode.OK,normal.StatusCode);}
        var before=Volatile.Read(ref discoveries);Assert.True(before>0);
        holdOperation=true;
        using var cancelLookup=new CancellationTokenSource();
        var cancelledRequest=client.GetAsync("/api/tables",cancelLookup.Token);
        try{
            await operationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));cancelLookup.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{using var response=await cancelledRequest;});
            await operationAborted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }finally{operationRelease.TrySetResult();}
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(before,Volatile.Read(ref discoveries));
        Assert.NotNull(delayedAuthorization);
        Assert.Throws<ObjectDisposedException>(()=>delayedAuthorization.ThrowIfCancellationRequested());

        async ValueTask<AuthorizationLease?> BeginOperationAsync(AuthenticatedIdentity identity,CancellationToken cancellation){
            if(holdOperation){
                using var registration=cancellation.Register(()=>operationAborted.TrySetResult());
                operationEntered.TrySetResult();
                await operationRelease.Task;
                delayedAuthorization=policy.CreateSession(identity).BeginOperation();return delayedAuthorization;
            }
            return policy.CreateSession(identity).BeginOperation(cancellation);
        }

        async ValueTask<AuthorizationLease?> BeginAuthorizationAsync(AuthenticatedIdentity identity,CancellationToken cancellation){
            if(holdTransaction){
                entered.TrySetResult();
                try{await release.Task.WaitAsync(cancellation);}
                finally{exited.TrySetResult();}
            }
            return policy.CreateSession(identity).BeginTransaction();
        }

        Seamless.Security.AuthorizationPolicySnapshot Policy(long version,bool insert)=>new(version,
            [new(serverId,null,SecurityResourceKind.Server,"gateway"),new(databaseId,serverId,SecurityResourceKind.Database,"sales"),
                new(schemaId,databaseId,SecurityResourceKind.Schema,"dbo"),new(tableId,schemaId,SecurityResourceKind.Table,"Rows")],
            [new(roleId,"writer",[new(databaseId,SecurityPermission.Discover),new(tableId,insert?SecurityPermission.Read|SecurityPermission.Insert:SecurityPermission.Read)])],
            [new(subject,SecurityPrincipalKind.User,roleId)]);
        Task<HttpResponseMessage> SqlAsync(string sql)=>client.PostAsync("/api/sql",new StringContent(JsonSerializer.Serialize(new{query=sql})));
        async Task OpenAsync(){
            using var response=await client.PostAsync("/api/session",null);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
            using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Add("X-Seamless-Session",result.RootElement.GetProperty("id").GetString());
        }
        async Task BeginAsync(){
            using var response=await SqlAsync("BEGIN TRANSACTION");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Add("X-Seamless-Transaction",result.RootElement.GetProperty("session").GetProperty("transactionId").GetGuid().ToString());
        }
        async Task<int> CountAsync(){
            using var response=await SqlAsync("SELECT COUNT(*) AS Total FROM dbo.Rows");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return Assert.Single(result.RootElement.GetProperty("rows").EnumerateArray()).GetProperty("Total").GetInt32();
        }
    });
}
