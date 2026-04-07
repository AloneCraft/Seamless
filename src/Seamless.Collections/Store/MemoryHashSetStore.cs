
using Seamless.Devices;

using Tsavorite.core;

namespace Seamless.Collections;
public abstract class HashSetStore<T>:CollectionStore<T,Empty>where T:IEquatable<T>{
    private protected HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                      (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
    public abstract ManagedHashSet<T> Create();
}
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryHashSetStore<T>:HashSetStore<T>
    where T:IEquatable<T>{
    internal MemoryHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){}
    public override ManagedHashSet<T> Create()=>new MemoryManagedHashSet<T>(this);
}
