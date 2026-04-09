namespace Seamless.Store;
using global::Common;

using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class RelationStore<TKey,TValue>:CollectionStore<TValue,Empty> where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>,ITuple<TKey>{
	public RelationStore(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0):base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){
	}
	public RelationStore(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0):base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice){
	}
    internal Relation<TKey,TValue> Create() =>
		new Relation<TKey,TValue>(this.Store);
}
