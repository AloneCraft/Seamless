namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryHashSetStore<T>:CollectionStore<T,Empty> 
    where T:IEquatable<T>{
    internal MemoryHashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){
	}
	public HashSet<T> Create()=>new MemoryHashSet<T>(this);
}
