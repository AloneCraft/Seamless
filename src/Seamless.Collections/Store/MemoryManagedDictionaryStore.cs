using Seamless.Devices;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryManagedDictionaryStore<TKey,TValue,TKeySerializer,TValueSerializer>:ManagedDictionaryStore<TKey,TValue,TKeySerializer,TValueSerializer>
	where TKey:IEquatable<TKey>
	where TValue:IEquatable<TValue>
	where TKeySerializer:ISerializer<TKey>
	where TValueSerializer:ISerializer<TValue>
{
	//public DictionaryStore(double MutableFraction):base(MutableFraction){}
	internal MemoryManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
		base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){}
	public override ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> Create()=>new MemoryManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>(this);
}

