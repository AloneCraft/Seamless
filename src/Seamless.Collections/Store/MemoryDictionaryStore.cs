
using Seamless.Devices;

using Tsavorite.core;

namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryDictionaryStore<TKey,TValue>:VariableDictionaryStore<TKey,TValue> 
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
{
	//public DictionaryStore(double MutableFraction):base(MutableFraction){}
    internal MemoryDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){}
	public override VariableDictionary<TKey,TValue> Create()=>new MemoryVariableDictionary<TKey,TValue>(this);
}
public static class Extension{
    public static VariableDictionary<TKey,TValue> Create<TKey,TValue>(this MemoryDictionaryStore<TKey,TValue> store) 
        where TKey:IEquatable<TKey> 
        where TValue:IEquatable<TValue> 
        =>new MemoryVariableDictionary<TKey,TValue>(store);
}
public static class Extension1{
    //public static UnmanagedDictionary<TKey,TValue> Create<TKey,TValue>(this MemoryDictionaryStore<TKey,TValue> store) 
    //    where TKey:unmanaged,IEquatable<TKey> 
    //    where TValue:unmanaged,IEquatable<TValue> 
    //    =>new MemoryUnmanagedDictionary<TKey,TValue>(store);
}
