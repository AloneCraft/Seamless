using Seamless.Collections;
namespace Seamless.Stores;
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class StorageDictionaryStore<TKey,TValue,TKeySerializer,TValueSerializer>:ManagedDictionaryStore<TKey,TValue,TKeySerializer,TValueSerializer> 
	where TKey:IEquatable<TKey>
	where TValue:IEquatable<TValue>
	where TKeySerializer:ISerializer<TKey>
	where TValueSerializer:ISerializer<TValue>{
	internal StorageDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
		base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){}
	public override ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> Create()=>new StorageManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>(this);

}
