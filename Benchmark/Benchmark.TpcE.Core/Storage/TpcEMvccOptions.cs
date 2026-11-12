using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;

public static class TpcEMvccOptions{
    public static VersionedTpcEDatabase Open(Store store,TpcEScale scale){
        var memory=store.Limits.MemoryBytes;var disk=store.Limits.DiskBytes;var reserve=Math.Min(4L<<20,memory/16);var page=4096L;
        while(page<checked(2L*(scale.NewsItemBytes+4096L))){page=checked(page*2);}
        var transactions=new TransactionCoordinatorOptions{Concurrency=TransactionConcurrencyMode.Optimistic,History=new(){MaxRetainedMemoryBytes=Math.Max(1<<20,memory/4),MaxRetainedStorageBytes=Math.Min(512L<<20,disk/4),MaxMetadataBytes=Math.Max(1<<20,memory/4),MaintenanceReserveMemoryBytes=reserve,MaintenanceReserveStorageBytes=Math.Min(1L<<20,disk/16),MaintenanceWaitTimeout=TimeSpan.FromSeconds(30)}};
        return new(store,scale,transactions,new(){MemoryLimitBytes=memory,TemporaryStorageLimitBytes=disk,TemporaryDirectory=Path.Combine(store.DirectoryPath??Path.GetTempPath(),"tpc-mvcc-work"),MaximumQueryWorkers=4},new TsavoriteTuning{IndexSize=64,PageSize=page,SegmentSize=Math.Max(1<<20,page*4),LogMemorySize=page*4});
    }
}
