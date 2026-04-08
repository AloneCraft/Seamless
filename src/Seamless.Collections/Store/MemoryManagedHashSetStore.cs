using Seamless.Devices;


namespace Seamless.Collections;
public abstract class ManagedHashSetStore<T>:ManagedCollectionStore<T,Empty>where T:IEquatable<T>{
    private protected ManagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                      (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
    public abstract ManagedHashSet<T> Create();
}
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryManagedHashSetStore<T>:ManagedHashSetStore<T>
    where T:IEquatable<T>{
    internal MemoryManagedHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){}
    public override ManagedHashSet<T> Create()=>new MemoryManagedHashSet<T>(this);
}
