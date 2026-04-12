using Seamless.Collections;
namespace Seamless.Stores;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class StorageHashSetStore<TKey>:ManagedHashSetStore<TKey> where TKey:IEquatable<TKey>{
    internal StorageHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){
	}
	public override ManagedHashSet<TKey> Create()=>new StorageManagedHashSet<TKey>(this);
}

