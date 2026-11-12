using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Execution;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Theory,Trait("Category","Integration")]
    [InlineData(false,1)]
    [InlineData(false,2)]
    [InlineData(true,1)]
    [InlineData(true,2)]
    public async Task InterruptedCommonCheckpointRetainsWalAndRecoversEveryDatabase(bool previousCheckpoint,int stopDatabase){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var root=Path.Combine(data,"mvcc-checkpoint-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var budget=new ResourceBudget(64<<20,128<<20);var armed=false;Guid id;
        void Inject(GatewayCommitPhase phase,int database){if(armed&&phase==GatewayCommitPhase.DatabaseCheckpointed&&database==stopDatabase){throw new IOException("Injected partial common checkpoint.");}}
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),[Definition("a"),Definition("b")],budget:budget,failureInjection:Inject)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(72,"a");Execute(engine,session,"INSERT a.dbo.Items VALUES(1,10); INSERT b.dbo.Items VALUES(1,20)");if(previousCheckpoint){host.Transactions.Checkpoint();}
            Execute(engine,session,"BEGIN TRANSACTION; UPDATE a.dbo.Items SET N=110 WHERE Id=1; INSERT a.dbo.Items VALUES(2,111); UPDATE b.dbo.Items SET N=220 WHERE Id=1; INSERT b.dbo.Items VALUES(2,222)");id=session.Transaction!.Id;Execute(engine,session,"COMMIT TRANSACTION");Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(id).Kind);
            var checkpointPath=Path.Combine(root,"transactions","common-checkpoint.json");byte[]? checkpoint=null;if(File.Exists(checkpointPath)){checkpoint=File.ReadAllBytes(checkpointPath);}
            armed=true;Assert.Contains("partial common checkpoint",Assert.Throws<IOException>(host.Transactions.Checkpoint).Message);armed=false;
            if(checkpoint is null){Assert.False(File.Exists(checkpointPath));}else{Assert.Equal(checkpoint,File.ReadAllBytes(checkpointPath));}
            Assert.Throws<IOException>(()=>engine.Execute("SELECT N FROM a.dbo.Items",session).Rows.ToArray());
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(73,"a");Assert.Equal(new object?[]{110,111},engine.Execute("SELECT N FROM a.dbo.Items ORDER BY Id",session).Rows.Select(static row=>row[0]).ToArray());Assert.Equal(new object?[]{220,222},engine.Execute("SELECT N FROM b.dbo.Items ORDER BY Id",session).Rows.Select(static row=>row[0]).ToArray());Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(id).Kind);host.Transactions.Checkpoint();
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
    [Theory,Trait("Category","Integration")]
    [InlineData(GatewayCommitPhase.BeforeWal,false)]
    [InlineData(GatewayCommitPhase.WalDurable,true)]
    [InlineData(GatewayCommitPhase.BeforePublication,true)]
    [InlineData(GatewayCommitPhase.AfterPublication,true)]
    public async Task CommonDecisionRecoversDdlAndRowsAfterPublicationFailure(GatewayCommitPhase stop,bool committed){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var root=Path.Combine(data,"mvcc-failure-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var budget=new ResourceBudget(64<<20,128<<20);var armed=false;Guid id;
        void Inject(GatewayCommitPhase phase,int database){if(armed&&phase==stop){throw new IOException("Injected common publication failure.");}}
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),[Definition("a"),Definition("b")],budget:budget,failureInjection:Inject)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(70,"a");Execute(engine,session,"INSERT a.dbo.Items VALUES(1,100); INSERT b.dbo.Items VALUES(1,200); BEGIN TRANSACTION; UPDATE a.dbo.Items SET N=110 WHERE Id=1; ALTER TABLE b.dbo.Items ADD Extra int NOT NULL DEFAULT 7; UPDATE b.dbo.Items SET N=220 WHERE Id=1");id=session.Transaction!.Id;armed=true;
            var error=Assert.Throws<TransactionCommitException>(()=>Execute(engine,session,"COMMIT TRANSACTION"));var expected=TransactionOutcomeKind.Aborted;if(committed){expected=TransactionOutcomeKind.Committed;}Assert.Equal(expected,error.Outcome);Assert.Equal(id,error.TransactionId);Assert.False(session.IsInTransaction);armed=false;
            if(committed){Assert.ThrowsAny<Exception>(()=>engine.Execute("SELECT N FROM a.dbo.Items",session).Rows.ToArray());}else{Assert.Equal(100,Assert.Single(engine.Execute("SELECT N FROM a.dbo.Items",session).Rows)[0]);}
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(71,"a");var a=100;var b=200;if(committed){a=110;b=220;Assert.Equal(7,Assert.Single(engine.Execute("SELECT Extra FROM b.dbo.Items",session).Rows)[0]);Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(id).Kind);}else{Assert.Throws<global::Seamless.Database.Query.SqlQueryException>(()=>engine.Execute("SELECT Extra FROM b.dbo.Items",session).Rows.ToArray());}
            Assert.Equal(a,Assert.Single(engine.Execute("SELECT N FROM a.dbo.Items",session).Rows)[0]);Assert.Equal(b,Assert.Single(engine.Execute("SELECT N FROM b.dbo.Items",session).Rows)[0]);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
}
