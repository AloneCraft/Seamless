namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class DictionaryStorageStore<TKey,TValue>:CollectionStore<TKey,TValue> 
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
	internal DictionaryStorageStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string CheckpointDir):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,LogDevice,ObjectLogDevice){}
	public Dictionary<TKey,TValue> Create()=>new Sto(this.Store,this.LogMemorySize,this.MutableFraction);
}
