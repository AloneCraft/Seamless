using Seamless.Collections;
using Seamless.Devices;

namespace Seamless.Store;
/// <summary>
/// UnmanagedHashSet 用の Tsavorite ストア。
/// </summary>
public abstract class UnmanagedHashSetStore<TKey>:UnmanagedCollectionStore<TKey,EqualityEmpty>
    where TKey:unmanaged,IEquatable<TKey> {
    private protected UnmanagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                      (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
    public abstract UnmanagedHashSet<TKey> Create();
}
/// <summary>
/// メモリのみの UnmanagedHashSet ストア。
/// </summary>
public sealed class MemoryUnmanagedHashSetStore<TKey>:UnmanagedHashSetStore<TKey>
    where TKey:unmanaged,IEquatable<TKey> {
    internal MemoryUnmanagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){}
    public override UnmanagedHashSet<TKey> Create()=>new MemoryUnmanagedHashSet<TKey>(this);
}
