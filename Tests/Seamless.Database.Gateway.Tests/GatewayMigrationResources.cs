using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayMigrationResources{
    [Theory,Trait("Category","Integration")]
    [InlineData("cancel")]
    [InlineData("memory")]
    [InlineData("disk")]
    public async Task FailedMigrationPreservesTheTransactionAndReleasesEveryOwnedResource(string failure){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");
        var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");
        var root=Path.Combine(data,"migration-resource-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        var budget=new ResourceBudget(128L<<20,256L<<20);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};
        DatabaseCatalog Definition(string name)=>new(name,[new SchemaCatalog("dbo",[new TableCatalog("Rows",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"Value",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("PK_Rows",[new("Id",false)]))])],"Japanese_CI_AS");
        var armed=false;ResourceReservation? pressure=null;using var cancellation=new CancellationTokenSource();
        void Inject(GatewayCommitPhase phase,int id){
            if(!armed||id!=1){return;}
            if(failure=="cancel"&&phase==GatewayCommitPhase.CandidateBuilt){cancellation.Cancel();}
            if(phase!=GatewayCommitPhase.CandidateCopied){return;}
            if(failure=="memory"){pressure=budget.ReserveMemory(budget.MemoryLimitBytes-budget.MemoryBytes);}
            if(failure=="disk"){throw new IOException("Injected full candidate volume.",unchecked((int)0x80070070));}
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[Definition("a"),Definition("b")],budget:budget,failureInjection:Inject)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(930,"a");using var unrelated=new SessionContext(931,"b");
            void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("INSERT a.dbo.Rows(Id,Value) VALUES(1,10); INSERT b.dbo.Rows(Id,Value) VALUES(1,20); BEGIN TRANSACTION; UPDATE a.dbo.Rows SET Value=11; SAVE TRANSACTION before_ddl");
            session.CancellationToken=cancellation.Token;armed=true;Exception error;
            try{error=Assert.ThrowsAny<Exception>(()=>Execute("ALTER TABLE a.dbo.Rows ALTER COLUMN Value bigint NOT NULL"));}
            finally{armed=false;pressure?.Dispose();session.CancellationToken=CancellationToken.None;}
            if(failure=="cancel"){Assert.IsAssignableFrom<OperationCanceledException>(error);}else if(failure=="memory"){Assert.IsType<ResourceLimitException>(error);}else{Assert.IsType<IOException>(error);}
            Assert.Equal(1,session.TransactionCount);Assert.Equal(1,session.TransactionState);
            Assert.Equal(11,Assert.Single(engine.Execute("SELECT Value FROM a.dbo.Rows",session).Rows)[0]);
            Assert.Equal(20,Assert.Single(engine.Execute("SELECT Value FROM b.dbo.Rows",unrelated).Rows)[0]);
            Execute("ROLLBACK TRANSACTION before_ddl; UPDATE a.dbo.Rows SET Value=12; COMMIT TRANSACTION");
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(932,"a");
            Assert.Equal(12,Assert.Single(engine.Execute("SELECT Value FROM a.dbo.Rows",session).Rows)[0]);
            Assert.Equal(20,Assert.Single(engine.Execute("SELECT Value FROM b.dbo.Rows",session).Rows)[0]);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
}
