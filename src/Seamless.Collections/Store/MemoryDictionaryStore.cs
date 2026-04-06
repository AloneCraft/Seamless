using Common;

using Seamless.Devices;

using Tsavorite.core;

namespace Seamless.Collections;
public abstract class DictionaryStore<TKey,TValue>:CollectionStore<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    private protected DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                         (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
    //private otected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction): 

    public abstract Dictionary<TKey,TValue> Create();
}
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryDictionaryStore<TKey,TValue>:DictionaryStore<TKey,TValue> 
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
{
	//public DictionaryStore(double MutableFraction):base(MutableFraction){}
    internal MemoryDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){}
	public override Dictionary<TKey,TValue> Create()=>new MemoryDictionary<TKey,TValue>(this);
}
