using System.Collections.Concurrent;
using Seamless.Authentication;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Security;
using Seamless.Transactions;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class NativeQueryExecutionSnapshots{
    [Fact]public Task OldTransactionKeepsItsApprovedPublicationWhileNewCodeRequiresNewApproval()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);void Mark(string stage)=>File.AppendAllText(Path.Combine(root,"stages.log"),DateTimeOffset.UtcNow.ToString("o")+" "+stage+Environment.NewLine);
        var references=global::Seamless.Database.Gateway.Tests.GeneratedAssemblyBuilderTests.RuntimeReferences();
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var compiler=new GeneratedAssemblyBuilder(new(Path.Combine(root,"w"),Path.Combine(root,"c"),dotnet,typeof(Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,references));
        var options=new DatabaseContainerOptions{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
            TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=8<<20,MaxMetadataBytes=2<<20,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)},
            IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,ManagedMemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=32<<20,TransactionMemoryLimit=1024};
        var number=new DatabaseType(DatabaseTypeKind.Int32,false,SqlTypeName:"int");var catalog=new DatabaseCatalog("lab",[new("dbo",[new TableCatalog("Items",[new(0,"Id",number,false),new(1,"N",number,false)],new("PK_Items",[new("Id",false)],1))])],"Japanese_CI_AS");
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(root,"db"),compiler,options,[catalog,new("anchor",[new SchemaCatalog("dbo",[])])]);var engine=new SqlExecutionEngine(host.Databases);using var administration=new SessionContext(997,"lab");
        foreach(var row in engine.Execute("INSERT INTO dbo.Items VALUES(1,200)",administration).Rows){}
        NativeQuerySourceProject Source(int offset,string resultType="System.Int32",string resultExpression="sum"){
            var type=((InProcessDatabaseAdapter)host.Databases.Resolve("lab").Adapter).Database.GetType().FullName!.Replace('+','.');
            var source=$$"""
                using MemoryPack;
                using Seamless.Database.Client;
                using Seamless.Database.Gateway.Native;
                public sealed class Query : INativeQueryModule {
                    public IEnumerable<byte[]> Execute(string id, ReadOnlyMemory<byte> args, INativeQueryContext context) {
                        var db=(global::{{type}})context.Database;
                        var sum={{offset}};
                        foreach (var row in global::Seamless.Database.StoredTableExtensions.GetGatewayTransactionRows(db.dbo.Items)) { sum=checked(sum+row.N); }
                        yield return MemoryPackSerializer.Serialize({{resultExpression}});
                    }
                }
                """;
            return new("snapshot","Query",[new("Query.cs",source)],[new("value","lab","","System.Int32",resultType)],["lab"]);
        }
        await host.Transactions.InstallNativeQueryAsync(Source(0));var oldHash=host.Transactions.NativeQueries.Single(item=>item.ModuleId=="snapshot").Manifest.AssemblySha256;Mark("original publication ready");
        await host.Transactions.InstallNativeQueryAsync(new("anchor","Query",[new("Query.cs","using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){yield return MemoryPackSerializer.Serialize(0);}}")],[new("touch","anchor","","System.Int32","System.Int32")],["anchor"]));
        var anchorHash=host.Transactions.NativeQueries.Single(item=>item.ModuleId=="anchor").Manifest.AssemblySha256;
        Mark("anchor publication ready");
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);var serverId=Guid.NewGuid();var databaseId=Guid.NewGuid();var anchorDatabaseId=Guid.NewGuid();var queryId=Guid.NewGuid();var anchorQueryId=Guid.NewGuid();var roleId=Guid.NewGuid();var tableId=Guid.NewGuid();
        using var policy=new AuthorizationManager(new(1,[new(serverId,null,SecurityResourceKind.Server,"server"),new(databaseId,serverId,SecurityResourceKind.Database,"lab"),new(anchorDatabaseId,serverId,SecurityResourceKind.Database,"anchor"),new(queryId,databaseId,SecurityResourceKind.Query,"value"),new(anchorQueryId,anchorDatabaseId,SecurityResourceKind.Query,"touch"),new(tableId,databaseId,SecurityResourceKind.Table,"Items")],[new(roleId,"executor",[new(databaseId,SecurityPermission.Discover),new(anchorDatabaseId,SecurityPermission.Discover),new(queryId,SecurityPermission.Execute),new(anchorQueryId,SecurityPermission.Execute),new(tableId,SecurityPermission.Read)])],[new(new(issuer.Id,"dev-user"),SecurityPrincipalKind.User,roleId)]));
        var approved=new ConcurrentDictionary<string,byte>(StringComparer.Ordinal);approved.TryAdd(oldHash,0);approved.TryAdd(anchorHash,0);var observed=new ConcurrentQueue<NativeQueryExecution>();
        var protection=NativeGatewayAuthorization.ForPublishedQueries(verifier,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(policy.CreateSession(identity)),(lease,request)=>lease.Demand(request.Database=="anchor"?anchorDatabaseId:databaseId,SecurityPermission.Discover),
            (lease,_,execution)=>{lease.Demand(execution.ModuleId=="anchor"?anchorQueryId:queryId,SecurityPermission.Execute);observed.Enqueue(execution);if(!approved.ContainsKey(execution.AssemblySha256)){throw new AuthorizationException("UnapprovedExecution","The new executable has not been approved.");}});
        // 旧世代・DDLに加え公開契約2版のビルドを行う試験全体の期限。通信／公開の製品側期限は変更しない。
        using var server=NativeQueryServer.CreateAuthenticated(host.Databases,certificate,protection,new(),0);
        server.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        administration.CancellationToken=timeout.Token;
        NativeQueryClient Client()=>NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")),port:server.Port,trustedRoot:certificate);
        var fingerprint=host.Databases.Resolve("lab").SchemaFingerprint!;await using var oldClient=Client();await using var current=Client();
        async Task<int> ReadOld(NativeTransaction transaction){var count=0;var result=0;await foreach(var row in transaction.QueryAsync<int,int>("snapshot","value","lab",fingerprint,0,timeout.Token)){result=row;count++;}Assert.Equal(1,count);return result;}
        NativeQueryExecution oldExecution;
        // 同じDBのSchema leaseを保持する読取と公開は待機する。試験側の取消を必ず渡す。
        await using(var reader=await oldClient.BeginTransactionAsync("lab",timeout.Token)){
            Mark("same database transaction opened");Assert.Equal(200,await ReadOld(reader));Mark("same database read complete");oldExecution=observed.Last();using var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>host.Transactions.InstallNativeQueryAsync(Source(10),cancellation.Token));Mark("blocked publication cancelled");await reader.RollbackAsync(timeout.Token);
        }
        // 無関係なDBでsnapshotを固定し、公開対象DBのSchema leaseはまだ取らない。
        await using(var old=await oldClient.BeginTransactionAsync("anchor",timeout.Token)){
            await foreach(var value in old.QueryAsync<int,int>("anchor","touch",host.Databases.Resolve("anchor").SchemaFingerprint!,0,timeout.Token)){Assert.Equal(0,value);}
            Mark("anchor snapshot fixed");
            await host.Transactions.InstallNativeQueryAsync(Source(10),timeout.Token);var newHash=host.Transactions.NativeQueries.Single(item=>item.ModuleId=="snapshot").Manifest.AssemblySha256;Assert.NotEqual(oldHash,newHash);
            Mark("replacement publication ready");
            await Assert.ThrowsAsync<NativeQueryException>(()=>current.ExecuteScalarAsync<int,int>("snapshot","value","lab",fingerprint,0,timeout.Token));
            Assert.Equal(200,await ReadOld(old));Assert.Equal(oldExecution.QueryGenerationId,observed.Last().QueryGenerationId);Assert.Same(oldExecution.Module,observed.Last().Module);
            var inspected=host.Transactions.InspectNativePublication("lab","snapshot","value",timeout.Token);
            Assert.Equal(newHash,inspected.AssemblySha256);
            Assert.NotEqual(oldExecution.QueryGenerationId,inspected.QueryGenerationId);
            approved.TryAdd(inspected.AssemblySha256,0);
            Assert.Equal(210,await current.ExecuteScalarAsync<int,int>("snapshot","value","lab",fingerprint,0,timeout.Token));var next=observed.Last();Assert.NotEqual(oldExecution.QueryGenerationId,next.QueryGenerationId);Assert.NotSame(oldExecution.Module,next.Module);Assert.Equal(oldExecution.Databases,next.Databases);
            await old.RollbackAsync(timeout.Token);
            Mark("old snapshot rolled back");
        }
        var previous=observed.Last();foreach(var row in engine.Execute("ALTER TABLE dbo.Items ADD Extra int NULL",administration).Rows){}
        Mark("schema and native code republished");
        var updatedFingerprint=host.Databases.Resolve("lab").SchemaFingerprint!;Assert.NotEqual(fingerprint,updatedFingerprint);
        await Assert.ThrowsAsync<NativeQueryException>(()=>current.ExecuteScalarAsync<int,int>("snapshot","value","lab",updatedFingerprint,0,timeout.Token));
        var after=observed.Last();Assert.NotEqual(previous.QueryGenerationId,after.QueryGenerationId);Assert.NotEqual(Assert.Single(previous.Databases).GenerationId,Assert.Single(after.Databases).GenerationId);
        approved.TryAdd(after.AssemblySha256,0);Assert.Equal(210,await current.ExecuteScalarAsync<int,int>("snapshot","value","lab",fingerprint,0,timeout.Token));
        Mark("new generation approved and executed with unchanged client and old fingerprint");
        await host.Transactions.InstallNativeQueryAsync(Source(10,"System.Int32[]","new[]{sum}"),timeout.Token);
        approved.TryAdd(host.Transactions.NativeQueries.Single(item=>item.ModuleId=="snapshot").Manifest.AssemblySha256,0);
        await Assert.ThrowsAsync<NativeQueryException>(()=>current.ExecuteScalarAsync<int,int[]>("snapshot","value","lab",fingerprint,0,timeout.Token));
        Assert.Equal(new[]{210},await current.ExecuteScalarAsync<int,int[]>("snapshot","value","lab",updatedFingerprint,0,timeout.Token));
        Mark("unqualified array contract retains schema check");
        await host.Transactions.InstallNativeQueryAsync(Source(10,"System.Int64","(long)sum"),timeout.Token);
        approved.TryAdd(host.Transactions.NativeQueries.Single(item=>item.ModuleId=="snapshot").Manifest.AssemblySha256,0);
        var mismatch=await Assert.ThrowsAsync<NativeQueryException>(()=>current.ExecuteScalarAsync<int,int>("snapshot","value","lab",fingerprint,0,timeout.Token));
        Assert.True(mismatch.IsRecoverable);Assert.Contains("result contract",mismatch.Message,StringComparison.Ordinal);
        var argumentsMismatch=await Assert.ThrowsAsync<NativeQueryException>(()=>current.ExecuteScalarAsync<long,long>("snapshot","value","lab",fingerprint,0L,timeout.Token));
        Assert.True(argumentsMismatch.IsRecoverable);Assert.Contains("argument contract",argumentsMismatch.Message,StringComparison.Ordinal);
        Assert.Equal(210L,await current.ExecuteScalarAsync<int,long>("snapshot","value","lab",fingerprint,0,timeout.Token));
        Mark("changed public result rejects old type and accepts matching type");
    });
}
