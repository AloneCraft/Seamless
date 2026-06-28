using Seamless.Tsavorite.Devices;


namespace Seamless.Tsavorite.Stores;

public class HashSetStore<TKey>:CollectionStore<TKey,EqualityEmpty> {
    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes) { }

    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) { }

    private protected override long GetRecoveryEntryCount() {
        using var set = this.Create();
        return set.GetLongCount();
    }

    public Collections.HashSet<TKey> Create() => new(this);
    public Collections.Set<TKey> CreateSet() => new(this);
    internal Collections.HashSet<TKey> CreateOwned() => new(this,this);
    internal Collections.Set<TKey> CreateOwnedSet() => new(this,this);
}
