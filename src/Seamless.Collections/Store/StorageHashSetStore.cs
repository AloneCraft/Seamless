namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class StorageHashSetStore<T>:CollectionStore<T,Empty> where T:IEquatable<T>{
    internal StorageHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,LogDevice,ObjectLogDevice){
	}
	public HashSet<T> Create()=>new StorageHashSet<T>(this);
}

