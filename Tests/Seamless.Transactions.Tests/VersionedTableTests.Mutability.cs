using Seamless.Collections.Iteration;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions.Collections;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class VersionedTableTests{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutablePayloadsCannotChangeStagedCommittedOrOtherTablesRows(bool disk){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"mutable-rows-"+Guid.NewGuid().ToString("N"));
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=48L<<20,TemporaryStorageLimitBytes=32L<<20,TemporaryDirectory=root});
        var limits=new StoreLimits{MemoryBytes=32L<<20,DiskBytes=32L<<20};Store store;
        if(disk){store=Store.Open(root,limits,parentBudget:resources.Budget);}else{store=Store.CreateMemory(limits,parentBudget:resources.Budget);}
        using(store){
            await using var owner=new TransactionCoordinator(null,null,null,new(){Concurrency=TransactionConcurrencyMode.Optimistic,ParentBudget=resources.Budget,
                History=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=8<<20,MaxMetadataBytes=256<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=64<<10,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
            var leftStore=store.CreateTsavoriteHistory("left",TuningForMutableRows(),VersionedTransactionalTable<int,MutableRow>.StorageIdentity("mutable-left",VersionedRecordEncoding.Canonical));
            var rightStore=store.CreateTsavoriteHistory("right",TuningForMutableRows(),VersionedTransactionalTable<int,MutableRow>.StorageIdentity("mutable-right",VersionedRecordEncoding.Canonical));
            using var left=new VersionedTransactionalTable<int,MutableRow>(0,1,"mutable-left",leftStore,resources,1024,static row=>row.Id);
            using var right=new VersionedTransactionalTable<int,MutableRow>(0,2,"mutable-right",rightStore,resources,1024,static row=>row.Id);owner.Register(left);owner.Register(right);
            await using(var seed=await owner.BeginAsync()){
                var payload=new byte[]{10,20};left.For(seed).Add(new(1,payload));right.For(seed).Add(new(1,payload));payload[0]=99;
                var copy=left.For(seed)[1];copy.Payload[0]=77;Assert.Equal(10,left.For(seed)[1].Payload[0]);Assert.Equal(10,right.For(seed)[1].Payload[0]);await seed.CommitAsync();
            }
            await using(var old=await owner.BeginReadOnlyAsync()){
                // 退避後のnative Pending完了から返る可変データにも同じ所有権を要求する。
                if(disk){leftStore.Store.Log.FlushAndEvict(true);rightStore.Store.Log.FlushAndEvict(true);}
                var copy=left.For(old)[1];copy.Payload[0]=88;var mutate=new MutatePayload();left.For(old).Rows.Iterate(ref mutate);
                Assert.Equal(1,mutate.Count);Assert.Equal(10,left.For(old)[1].Payload[0]);Assert.Equal(10,right.For(old)[1].Payload[0]);
                await OutsideCurrent(async()=>{
                    await using var write=await owner.BeginAsync();var payload=new byte[]{30,40};left.For(write)[1]=new(1,payload);right.For(write)[1]=new(1,payload);payload[0]=99;
                    write.CreateSavepoint("before");left.For(write)[1]=new(1,[50,60]);write.RollbackToSavepoint("before");
                    var staged=left.For(write)[1];staged.Payload[0]=66;Assert.Equal(30,left.For(write)[1].Payload[0]);Assert.Equal(30,right.For(write)[1].Payload[0]);await write.CommitAsync();
                });
                Assert.Equal(10,left.For(old)[1].Payload[0]);Assert.Equal(10,right.For(old)[1].Payload[0]);
            }
            await using(var current=await owner.BeginReadOnlyAsync()){Assert.Equal(30,left.For(current)[1].Payload[0]);Assert.Equal(30,right.For(current)[1].Payload[0]);}
        }
        Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }
    private static TsavoriteTuning TuningForMutableRows()=>new(){IndexSize=64,PageSize=4096,SegmentSize=1<<16,LogMemorySize=1<<16};
    private readonly record struct MutableRow(int Id,byte[] Payload);
    private struct MutatePayload:IStrategy<MutatePayload,MutableRow>{
        internal int Count;
        public static bool Invoke(ref MutatePayload self,MutableRow row){row.Payload[0]=55;self.Count++;return true;}
    }
}
