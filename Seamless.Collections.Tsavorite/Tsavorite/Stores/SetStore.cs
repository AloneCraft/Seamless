using Seamless.Tsavorite.Devices;

namespace Seamless.Tsavorite.Stores;

public class SetStore<TKey, TRecord>:CollectionStore<TKey,TRecord>
    where TRecord : IRecord<TKey> {
    internal SetStore(long indexSize,long pageSize,long segmentSize,long memorySize,double mutableFraction) :
        base(indexSize,pageSize,segmentSize,memorySize,mutableFraction,new MemoryOnlyDevice(segmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    internal SetStore(long indexSize,long pageSize,long segmentSize,long memorySize,double mutableFraction,IDevice logDevice,IDevice objectLogDevice,string? checkpointDir,double storageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long storageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? objectLogSegmentSize = null) :
        base(indexSize,pageSize,segmentSize,memorySize,mutableFraction,logDevice,objectLogDevice,checkpointDir,OverflowMutableStrategyKind.Storage,storageLimitFraction,storageReserveBytes,objectLogSegmentSize) {
    }

    private protected override long GetRecoveryEntryCount() {
        using var set = this.Create();
        return set.GetLongCount();
    }

    public Set<TKey,TRecord> Create() => new(this);
}
