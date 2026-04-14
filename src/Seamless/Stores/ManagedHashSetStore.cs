using Seamless.Collections;
using Seamless.Devices;


namespace Seamless.Stores;

public class ManagedHashSetStore<TKey>:ManagedCollectionStore<TKey,EqualityEmpty> where TKey:IEquatable<TKey>{
    internal ManagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage){}

    internal ManagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory){}

    public ManagedHashSet<TKey> Create()=>new(this);
}
