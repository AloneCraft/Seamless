using Benchmark.Tpc.Server;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
public sealed class TpcWalFailure{
    [Fact]public async Task SyncFailureStopsUpdatesReadsRollbackAndCheckpoints(){
        var stores=new[]{Store.CreateTemporary(),Store.CreateTemporary()};var scale=TestDatabases.Mini(2,2);
        using var engine=TestDatabases.Variants[0].Open(stores,scale,Core.TpcCMode.Spec);
        engine.Database.BeginBulkLoad();new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(Core.TpcCMode.Spec),1).Generate(engine.Loader);engine.Database.EndBulkLoad();
        using var device=new FakeWalDevice();device.BlockSync();await using var writer=new WalWriter(device);
        var host=new TpcWalHost(engine.Database.PartitionBases.Cast<TpcDatabaseBase>().ToArray(),writer);
        try{
            var update=host.ExecuteAsync([0,1],()=>engine.Execute(new Core.PaymentRequest(1,123,1,2,1,1,null,10m)),static result=>result.RolledBack,static (_,_)=>[]);
            Assert.True(device.SyncEntered.Wait(TimeSpan.FromSeconds(5)));
            var read=host.ExecuteAsync([1],()=>engine.Execute(new Core.StockLevelRequest(2,124,1,20)),static result=>result.RolledBack,static (_,_)=>[]);
            var rollback=host.ExecuteAsync([0,1],()=>engine.Execute(new Core.NewOrderRequest(1,125,1,1,[new(1,2,1),new(scale.Items+1,1,1)],0)),static result=>result.RolledBack,static (_,_)=>[]);
            Assert.False(update.IsCompleted);Assert.False(read.IsCompleted);Assert.False(rollback.IsCompleted);
            device.SyncFailure=new IOException("injected");device.ReleaseSync();
            foreach(var task in new[]{update,read,rollback}){await Assert.ThrowsAsync<TransactionCommitException>(async()=>await task.WaitAsync(TimeSpan.FromSeconds(5)));}
            await Assert.ThrowsAsync<IOException>(()=>host.CheckpointAsync(0));
        }finally{
            device.ReleaseSync();try{await host.DisposeAsync();}catch(IOException){}
            engine.Dispose();
            foreach(var store in stores){store.Dispose();}
        }
    }
    [Fact]public async Task BypassingTheSutCannotPublishAPartiallyMutatedCheckpoint(){
        var opened=TestDatabases.Open(TestDatabases.Variants[0],TestDatabases.Mini(1,1),Core.TpcCMode.Spec);
        using var store=opened.Store;using var engine=opened.Engine;using var device=new FakeWalDevice();await using var writer=new WalWriter(device);
        await using var host=new TpcWalHost(engine.Database.PartitionBases.Cast<TpcDatabaseBase>().ToArray(),writer);
        Assert.Throws<InvalidOperationException>(()=>engine.Execute(new Core.PaymentRequest(1,123,1,1,1,1,null,10m)));
        Assert.NotNull(host.Failure.Error);await Assert.ThrowsAsync<InvalidOperationException>(()=>host.CheckpointAsync(0));
    }
}
