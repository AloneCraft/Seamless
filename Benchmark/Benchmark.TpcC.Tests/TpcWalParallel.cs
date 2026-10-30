using Benchmark.Tpc.Server;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
public sealed class TpcWalParallel{
    [Fact]public async Task FourVariantsExecuteIndependentPaymentsBeforeEitherSyncCompletes(){
        foreach(var variant in TestDatabases.Variants){
            var stores=new[]{Store.CreateTemporary(),Store.CreateTemporary()};var scale=TestDatabases.Mini(2,2);
            using var engine=variant.Open(stores,scale,Core.TpcCMode.Spec);
            engine.Database.BeginBulkLoad();new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(Core.TpcCMode.Spec),1).Generate(engine.Loader);engine.Database.EndBulkLoad();
            var before0=engine.Database.PartitionBases[0].Customer.Get(new Core.CustomerKey(1,1,1)).Balance;
            var before1=engine.Database.PartitionBases[1].Customer.Get(new Core.CustomerKey(2,1,1)).Balance;
            using var device=new FakeWalDevice();device.BlockSync();await using var writer=new WalWriter(device);
            var host=new TpcWalHost(engine.Database.PartitionBases.Cast<TpcDatabaseBase>().ToArray(),writer);
            using var rendezvous=new Barrier(2);using var executed=new CountdownEvent(2);
            try{
                Task<TpcDurableResult<Core.TpcCResult>> Submit(int part,int warehouse)=>host.ExecuteAsync([part],()=>{
                    if(!rendezvous.SignalAndWait(TimeSpan.FromSeconds(5))){throw new TimeoutException("Independent partition did not execute.");}
                    var result=engine.Execute(new Core.PaymentRequest(warehouse,123,1,warehouse,1,1,null,10m));executed.Signal();return result;
                },static result=>result.RolledBack,static (_,_)=>[]);
                var first=Submit(0,1);var second=Submit(1,2);Assert.True(executed.Wait(TimeSpan.FromSeconds(5)));Assert.True(device.SyncEntered.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(first.IsCompleted);Assert.False(second.IsCompleted);device.ReleaseSync();await Task.WhenAll(first,second).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(before0-10m,engine.Database.PartitionBases[0].Customer.Get(new Core.CustomerKey(1,1,1)).Balance);
                Assert.Equal(before1-10m,engine.Database.PartitionBases[1].Customer.Get(new Core.CustomerKey(2,1,1)).Balance);Assert.Empty(Core.TpcCIndexConsistency.Check(engine.Database));
            }finally{
                device.ReleaseSync();try{await host.DisposeAsync();}finally{engine.Dispose();foreach(var store in stores){store.Dispose();}}
            }
        }
    }
}
