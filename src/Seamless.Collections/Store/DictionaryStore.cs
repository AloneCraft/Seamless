namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class DictionaryStore<TKey,TValue>:CollectionStore<TKey,TValue> 
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
{
	//public DictionaryStore(double MutableFraction):base(MutableFraction){}
    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice){}
	internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice,IDevice LogDevice,IDevice ObjectLogDevice):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice,LogDevice,ObjectLogDevice){}
	public Dictionary<TKey,TValue> Create()=>new(this.Store,this.LogMemorySize,this.MutableFraction);
}
