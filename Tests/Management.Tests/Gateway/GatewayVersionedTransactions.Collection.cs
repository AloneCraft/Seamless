using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Execution;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Fact,Trait("Category","Integration")]
    public async Task FailedCompletionRegistrationReleasesTheRootAndAllowsHostShutdown(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var budget=new ResourceBudget(64<<20,128<<20);
        ResourceReservation? pressure=null;
        // 開始中の別取引などが残予算を消費した場合を、既存の障害注入口で再現する。物理メモリは確保しない。
        void Inject(GatewayCommitPhase phase,int database){if(phase==GatewayCommitPhase.TransactionStarted&&pressure is null){pressure=budget.ReserveMemory(budget.AvailableMemoryBytes);}}
        var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"mvcc-admission-"+Guid.NewGuid().ToString("N")),GeneratedAssemblyBuilderTests.Compiler(data,cache),Options(),[Definition("a")],budget:budget,failureInjection:Inject);
        try{
            using var session=new SessionContext(64,"a");var before=budget.MemoryBytes;
            var error=Assert.Throws<global::Seamless.Transactions.TransactionAbortedException>(()=>session.Run(()=>{host.Transactions.Begin(session);return 0;}));
            Assert.Equal(global::Seamless.Transactions.TransactionAbortReason.ResourceLimit,error.Reason);Assert.False(session.IsInTransaction);pressure!.Dispose();
            Assert.Equal(before,budget.MemoryBytes);var engine=new SqlExecutionEngine(host.Databases);Assert.Equal(1,Assert.Single(engine.Execute("SELECT 1",session).Rows)[0]);
        }finally{pressure?.Dispose();await Task.Run(host.Dispose).WaitAsync(TimeSpan.FromSeconds(5));}
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckpointReleasesRetiredStoresAndRecoversWithoutTheirCodecs(bool automatic){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var root=Path.Combine(data,"mvcc-collection-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var budget=new ResourceBudget(64<<20,128<<20);
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),[Definition("a")],budget:budget)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(61,"a");using var hold=new SessionContext(63,"a");Execute(engine,hold,"BEGIN TRANSACTION");Execute(engine,session,"INSERT dbo.Items VALUES(1,100); ALTER TABLE dbo.Items ADD A int NULL; ALTER TABLE dbo.Items ADD B int NULL; BEGIN TRANSACTION; ALTER TABLE dbo.Items ADD C int NULL; ROLLBACK TRANSACTION");
            var before=budget.MemoryBytes;Execute(engine,hold,"ROLLBACK TRANSACTION");
            if(automatic){Assert.True(SpinWait.SpinUntil(()=>host.Transactions.LastGenerationCollection is not null,TimeSpan.FromSeconds(5)));}else{host.Transactions.Checkpoint();}Assert.True(budget.MemoryBytes<before);
            // ALCのUnloadは非同期。DLLがOSに保持されている間はDeferredを返して次の回収で再試行する。
            for(var attempt=0;attempt<10;attempt++){await Task.Delay(10);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
            host.Transactions.Checkpoint();Assert.Empty(host.Transactions.LastGenerationCollection!.Deferred);Assert.Equal(2,Directory.GetDirectories(Path.Combine(root,"generations","1")).Length);
            Assert.Equal(new object?[]{100,null,null},Assert.Single(engine.Execute("SELECT N,A,B FROM dbo.Items",session).Rows));
            Execute(engine,session,"UPDATE dbo.Items SET N=120 WHERE Id=1");
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),budget:budget)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(62,"a");Assert.Equal(new object?[]{120,null,null},Assert.Single(engine.Execute("SELECT N,A,B FROM dbo.Items",session).Rows));host.Transactions.Checkpoint();Assert.Empty(host.Transactions.LastGenerationCollection!.Deferred);}
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
}
