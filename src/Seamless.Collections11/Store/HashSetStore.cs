using Common;
namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class HashSetStore<T>:CollectionStore<T,Empty> where T:IEquatable<T>{
	public HashSetStore(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0):base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){
	}
	public HashSetStore(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0):base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice){
	}
	public HashSet<T> Create()=>new(this.Store,this.LogMemorySize,this.MutableFraction);
}
