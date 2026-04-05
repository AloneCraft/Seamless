namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class HashSetMemoryStore<T>:CollectionStore<T,Empty> where T:IEquatable<T>{
    internal HashSetMemoryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){
	}
	public HashSet<T> Create()=>new HashSetMemoryStore(this.Store,this.LogMemorySize,this.MutableFraction);
}
