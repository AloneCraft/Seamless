using Seamless.Collections;
using Seamless.Devices;


namespace Seamless.Stores;

public class ManagedHashSetStore<TKey>:ManagedCollectionStore<TKey,EqualityEmpty> where TKey : IEquatable<TKey> {
    internal ManagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage) { }

    internal ManagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) { }

    private protected override long GetRecoveryEntryCount() {
        using var set = this.Create();
        return set.GetLongCount();
    }

    public ManagedHashSet<TKey> Create() => new(this);
    internal ManagedHashSet<TKey> CreateOwned() => new(this,this);
}

//public class ManagedUnmanagedHashSetStore<TKey>:ManagedHashSetStore<TKey>
//    where TKey : IEquatable<TKey> {
//    internal ManagedUnmanagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    internal ManagedUnmanagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    public new ManagedUnmanagedHashSet<TKey> Create() => new(this);
//}
