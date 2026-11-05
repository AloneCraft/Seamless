using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Execution;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Fact,Trait("Category","Integration")]
    public async Task HistoryPressureAbortsAnUnconsumedReadAndAllowsLaterWritersToProceed(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var options=Options();options=options with{TransactionHistory=options.TransactionHistory! with{MaxRetainedStorageBytes=8<<10}};var budget=new ResourceBudget(64<<20,128<<20);
        using(var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"mvcc-pressure-"+Guid.NewGuid().ToString("N")),GeneratedAssemblyBuilderTests.Compiler(data,cache),options,[Definition("a")],budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var writer=new SessionContext(75,"a");using var reader=new SessionContext(76,"a");Execute(engine,writer,"INSERT dbo.Items VALUES(1,0)");host.Transactions.Checkpoint();
            // 結果の列挙はまだ始めない。走査handleを保持せず、古いReadVersionだけを保持する。
            var pending=engine.Execute("SELECT N FROM dbo.Items",reader);Assert.NotNull(reader.StatementTransaction);var committed=0;
            for(var attempt=0;attempt<256&&reader.StatementTransaction is not null;attempt++){
                Execute(engine,writer,"BEGIN TRANSACTION; UPDATE dbo.Items SET N=N+1 WHERE Id=1");
                try{Execute(engine,writer,"COMMIT TRANSACTION");committed++;}
                catch(TransactionCommitException error){
                    Assert.Equal(TransactionOutcomeKind.Aborted,error.Outcome);var reason=error.InnerException as TransactionAbortedException;
                    if(reason is null&&error.InnerException is AggregateException aggregate){reason=aggregate.Flatten().InnerExceptions.OfType<TransactionAbortedException>().Single();}
                    Assert.NotNull(reason);Assert.Equal(TransactionAbortReason.HistoryPressure,reason.Reason);
                }
            }
            Assert.Null(reader.StatementTransaction);var aborted=Assert.Throws<TransactionAbortedException>(()=>pending.Rows.ToArray());Assert.Equal(TransactionAbortReason.HistoryPressure,aborted.Reason);
            Execute(engine,writer,"UPDATE dbo.Items SET N=N+1 WHERE Id=1");committed++;Assert.Equal(committed,Assert.Single(engine.Execute("SELECT N FROM dbo.Items",reader).Rows)[0]);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
}
