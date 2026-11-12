using Seamless.Authentication;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Rdbms;
using Seamless.Security;
using Seamless.Transactions;
using System.Security.Cryptography;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class NativeGatewayAuthentication{
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public Task PublishedGatewayModuleUsesTheTransactionLeaseAcrossSavepointsAndRevocation(bool scopedOutcomes,bool versioned)=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Where(static path=>{
            var name=Path.GetFileName(path);return name.StartsWith("MemoryPack",StringComparison.Ordinal)||name.StartsWith("Seamless.",StringComparison.Ordinal)&&!name.Contains("Generator",StringComparison.Ordinal)&&!name.StartsWith("Seamless.Management",StringComparison.Ordinal);
        }).ToArray();
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var compiler=new GeneratedAssemblyBuilder(new(Path.Combine(root,"w"),Path.Combine(root,"c"),dotnet,typeof(Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,references));
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");
        var catalog=new DatabaseCatalog("sales",[new("dbo",[new TableCatalog("Rows",[new(0,"Id",number,false)],new("PK_Rows",[new("Id",false)],1))])],"Japanese_CI_AS");
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        if(versioned){
            options=options with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=8<<20,MaxMetadataBytes=2<<20,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};
        }
        var storage=Path.Combine(root,"db");
        using var host=await GatewayDatabaseHost.OpenAsync(storage,compiler,options,[catalog,new("outside",[new SchemaCatalog("dbo",[])])]);
        var database=((InProcessDatabaseAdapter)host.Databases.Resolve("sales").Adapter).Database;
        var row=database.GetType().GetProperty("dbo")!.GetValue(database)!.GetType().GetProperty("Rows")!.PropertyType.GetGenericArguments()[1].FullName!.Replace('+','.');
        var code="using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+database.GetType().FullName!.Replace('+','.')+")context.Database;if(id==\"insert\"){db.dbo.Rows.Add(new global::"+row+"(MemoryPackSerializer.Deserialize<int>(args.Span)));yield return MemoryPackSerializer.Serialize(1);}else{yield return MemoryPackSerializer.Serialize((int)db.dbo.Rows.Count);}}}";
        await host.Transactions.InstallNativeQueryAsync(new("secured","Query",[new("Query.cs",code)],[new("insert","sales","","System.Int32","System.Int32",false),new("count","sales","","System.Int32","System.Int32",true)],["sales"]));
        var approved=Assert.Single(host.Transactions.NativeQueries).Manifest;INativeQueryModule? executable=null;
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);
        var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var outsideId=Guid.NewGuid();var queryId=Guid.NewGuid();var roleId=Guid.NewGuid();
        Seamless.Security.AuthorizationPolicySnapshot Policy(long version,bool allowed){var permission=SecurityPermission.None;if(allowed){permission=SecurityPermission.Execute;}return new(version,[new(serverId,null,SecurityResourceKind.Server,"server"),new(databaseId,serverId,SecurityResourceKind.Database,"sales"),new(outsideId,serverId,SecurityResourceKind.Database,"outside"),new(queryId,databaseId,SecurityResourceKind.Query,"secured")],[new(roleId,"executor",[new(databaseId,SecurityPermission.Discover),new(outsideId,SecurityPermission.Discover),new(queryId,permission)])],[new(new(issuer.Id,"dev-user"),SecurityPrincipalKind.User,roleId),new(new(issuer.Id,"other-user"),SecurityPrincipalKind.User,roleId)]);}
        using var manager=new AuthorizationManager(Policy(1,true));var executions=0;
        byte[] key=scopedOutcomes?RandomNumberGenerator.GetBytes(32):[];
        ValueTask<IAuthorizationSession> OpenSession(AuthenticatedIdentity identity,CancellationToken _)=>ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity));
        void DemandDatabase(AuthorizationLease lease,NativeQueryRequest request)=>lease.Demand(request.Database switch{"sales"=>databaseId,"outside"=>outsideId,_=>throw new InvalidOperationException("Unknown fixture database.")},SecurityPermission.Discover);
        var authorization=NativeGatewayAuthorization.ForPublishedQueries(verifier,OpenSession,DemandDatabase,
            (lease,request,execution)=>{
                var module=execution.Module;var definition=execution.Definition;
                Assert.Equal(host.Transactions.WalId,execution.WalId);Assert.NotEqual(Guid.Empty,execution.QueryGenerationId);Assert.Equal(approved.AssemblySha256,execution.AssemblySha256);Assert.Equal(64,execution.ManifestSha256.Length);
                var binding=Assert.Single(execution.Databases);Assert.Equal("sales",binding.Name);Assert.Equal(database.Options.GatewayProfile!.DatabaseId,binding.DatabaseId);Assert.Equal(database.Options.GatewayProfile.GenerationId,binding.GenerationId);
                if(executable is null){using var file=File.OpenRead(module.GetType().Assembly.Location);Assert.Equal(approved.AssemblySha256,Convert.ToHexString(SHA256.HashData(file)));Assert.Equal(approved.EntryType,module.GetType().FullName);executable=module;}
                Assert.Same(executable,module);Assert.Contains(definition,approved.Queries);Assert.Equal("secured",request.ModuleId);lease.Demand(queryId,SecurityPermission.Execute);Interlocked.Increment(ref executions);
            },32,null,key);
        using var server=NativeQueryServer.CreateAuthenticated(host.Databases,certificate,authorization,new(),0);server.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        NativeQueryClient Client(int port,string subject="dev-user")=>NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:subject,scope:"seamless.database")),port:port,trustedRoot:certificate);
        await using var client=Client(server.Port);var fingerprint=host.Databases.Resolve("sales").SchemaFingerprint!;
        async Task<int> Execute(NativeTransaction transaction,string query,int value){var count=0;var result=0;await foreach(var item in transaction.QueryAsync<int,int>("secured",query,fingerprint,value,timeout.Token)){count++;result=item;}Assert.Equal(1,count);return result;}
        Guid committedId;
        await using(var transaction=await client.BeginTransactionAsync("sales",timeout.Token)){
            committedId=transaction.Id;
            Assert.Equal(1,await Execute(transaction,"insert",1));await transaction.SavepointAsync("point",timeout.Token);Assert.Equal(1,await Execute(transaction,"insert",2));await transaction.RollbackToSavepointAsync("point",timeout.Token);
            if(scopedOutcomes){Assert.Equal(NativeTransactionOutcomeKind.Unknown,(await transaction.GetOutcomeAsync(timeout.Token)).Kind);}
            manager.PublishPolicy(Policy(2,false));await transaction.BeginNestedAsync(timeout.Token);Assert.Equal(1,await Execute(transaction,"insert",3));await transaction.CommitAsync(timeout.Token);Assert.False(transaction.IsCompleted);await transaction.CommitAsync(timeout.Token);
        }
        var before=executions;await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("secured","count","sales",fingerprint,0,timeout.Token));Assert.Equal(before,executions);
        manager.PublishPolicy(Policy(3,true));Assert.Equal(2,await client.ExecuteScalarAsync<int,int>("secured","count","sales",fingerprint,0,timeout.Token));
        if(!scopedOutcomes){await Assert.ThrowsAsync<NativeQueryException>(()=>client.GetTransactionOutcomeAsync("sales",committedId,timeout.Token));}
        await using(var transaction=await client.BeginTransactionAsync("sales",timeout.Token)){
            Assert.Equal(1,await Execute(transaction,"insert",4));manager.SetDisabled(new(issuer.Id,"dev-user"),true);
            await Assert.ThrowsAnyAsync<Exception>(()=>transaction.CommitAsync(timeout.Token));Assert.True(transaction.IsCompleted);
        }
        manager.SetDisabled(new(issuer.Id,"dev-user"),false);await using var observer=Client(server.Port);Assert.Equal(2,await observer.ExecuteScalarAsync<int,int>("secured","count","sales",fingerprint,0,timeout.Token));Assert.True(executions>=12);
        if(scopedOutcomes){
            var outcome=await observer.GetTransactionOutcomeAsync("sales",committedId,timeout.Token);
            Assert.Equal(NativeTransactionOutcomeKind.Committed,outcome.Kind);
            Assert.Equal(committedId,outcome.TransactionId);
            Assert.True(outcome.Lsn>0);
            Assert.Equal(new(committedId,NativeTransactionOutcomeKind.Unknown,0),await observer.GetTransactionOutcomeAsync("outside",committedId,timeout.Token));
            await using(var other=Client(server.Port,"other-user")){
                Assert.Equal(new(committedId,NativeTransactionOutcomeKind.Unknown,0),await other.GetTransactionOutcomeAsync("sales",committedId,timeout.Token));
            }
            await using(var aborted=await observer.BeginTransactionAsync("sales",timeout.Token)){
                await aborted.RollbackAsync(timeout.Token);
                Assert.Equal(NativeTransactionOutcomeKind.Aborted,(await aborted.GetOutcomeAsync(timeout.Token)).Kind);
            }
            var walId=host.Transactions.WalId;
            await server.StopAsync();
            host.Transactions.Checkpoint();
            host.Dispose();
            using var reopened=await GatewayDatabaseHost.OpenAsync(storage,compiler,options,cancellationToken:timeout.Token);
            Assert.Equal(walId,reopened.Transactions.WalId);
            var freshAuthorization=NativeGatewayAuthorization.ForPublishedQueries(verifier,OpenSession,DemandDatabase,(lease,_,execution)=>{
                Assert.Equal(approved.AssemblySha256,execution.AssemblySha256);
                lease.Demand(queryId,SecurityPermission.Execute);
            },32,null,key);
            using var restarted=NativeQueryServer.CreateAuthenticated(reopened.Databases,certificate,freshAuthorization,new(),0);
            restarted.Start();
            await using var recovered=Client(restarted.Port);
            Assert.Equal(outcome,await recovered.GetTransactionOutcomeAsync("sales",committedId,timeout.Token));
            Assert.Equal(2,await recovered.ExecuteScalarAsync<int,int>("secured","count","sales",fingerprint,0,timeout.Token));
            await using var stranger=Client(restarted.Port,"other-user");
            Assert.Equal(new(committedId,NativeTransactionOutcomeKind.Unknown,0),await stranger.GetTransactionOutcomeAsync("sales",committedId,timeout.Token));
        }
    });
}
