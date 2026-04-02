using Common;
namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、HashSet インスタンスを作成するためのストアクラス。
/// 複数の HashSet インスタンスで同じストアを共有できる。
/// </summary>
public sealed class HashSetStore<T>:CollectionStore<T,Empty> where T:IEquatable<T>{
    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice){
	}
    internal HashSetStore(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice,LogDevice,ObjectLogDevice){
	}
	public HashSet<T> Create()=>new(this.Store,this.LogMemorySize,this.MutableFraction);
}
