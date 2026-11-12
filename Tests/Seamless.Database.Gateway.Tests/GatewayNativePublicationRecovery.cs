using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Transactions;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayNativePublicationRecovery{
    [Theory,Trait("Category","Integration")]
    [InlineData("aborted")]
    [InlineData("committed")]
    [InlineData("unknown")]
    public async Task NativeOnlyPublicationFencesItsDependenciesUntilTheOutcomeIsRecovered(string failure){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"native-publication-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};var armed=false;
        DatabaseCatalog Definition(string name)=>new(name,[new SchemaCatalog("dbo",[new TableCatalog("Rows",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("PK_Rows",[new("Id",false)]))])],"Japanese_CI_AS");
        NativeQuerySourceProject Source(int value)=>new("publication","Query",[new("Query.cs","using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){yield return MemoryPackSerializer.Serialize("+value+");}}")],[new("value","a","","System.Int32","System.Int32")],["a"]);
        void Inject(GatewayCommitPhase phase,int id){
            if(!armed){return;}
            if(failure=="committed"&&phase==GatewayCommitPhase.WalDurable){throw new IOException("Stopped before Native publication.");}
            if(failure!="committed"&&phase==GatewayCommitPhase.BeforeWal){if(failure=="unknown"){throw new TransactionCommitException(Guid.NewGuid(),TransactionOutcomeKind.Unknown,new IOException("Uncertain commit result."));}throw new IOException("Aborted Native preparation.");}
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[Definition("a"),Definition("b")],failureInjection:Inject)){
            await host.Transactions.InstallNativeQueryAsync(Source(1));armed=true;
            var error=await Assert.ThrowsAsync<TransactionCommitException>(()=>host.Transactions.InstallNativeQueryAsync(Source(2)));Assert.Equal(failure switch{"aborted"=>TransactionOutcomeKind.Aborted,"committed"=>TransactionOutcomeKind.Committed,_=>TransactionOutcomeKind.Unknown},error.Outcome);armed=false;
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(960,"a");
            if(failure=="aborted"){Assert.Empty(engine.Execute("SELECT Id FROM a.dbo.Rows",session).Rows);}else{Assert.Throws<DatabaseFaultedException>(()=>engine.Execute("SELECT Id FROM a.dbo.Rows",session).Rows.ToArray());}
            Assert.Empty(engine.Execute("SELECT Id FROM b.dbo.Rows",session).Rows);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var rows=new List<int>();
            await foreach(var row in client.QueryAsync<int,int>("publication","value","a",host.Databases.Resolve("a").SchemaFingerprint!,0,timeout.Token)){rows.Add(row);}Assert.Equal(failure=="committed"?2:1,Assert.Single(rows));
        }
    }
}
