using Seamless.Collections;
using Seamless.Devices;

namespace Seamless.Stores;

public class ManagedSetStore<TRecord, TKey>:ManagedCollectionStore<TKey,TRecord>
    where TRecord : IRecord<TKey> {
    internal ManagedSetStore(long indexSize,long pageSize,long segmentSize,long memorySize,double mutableFraction) :
        base(indexSize,pageSize,segmentSize,memorySize,mutableFraction,new MemoryOnlyDevice(segmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    internal ManagedSetStore(long indexSize,long pageSize,long segmentSize,long memorySize,double mutableFraction,IDevice logDevice,IDevice objectLogDevice,string? checkpointDir,double storageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long storageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(indexSize,pageSize,segmentSize,memorySize,mutableFraction,logDevice,objectLogDevice,checkpointDir,OverflowMutableStrategyKind.Storage,storageLimitFraction,storageReserveBytes) {
    }

    private protected override long GetRecoveryEntryCount() {
        using var set = this.Create();
        return set.GetLongCount();
    }

    public ManagedSet<TRecord,TKey> Create() => new(this);
}
