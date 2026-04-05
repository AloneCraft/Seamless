namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理し、Dictionary インスタンスを作成するためのストアクラス。
/// 複数の Dictionary インスタンスで同じストアを共有できる。
/// </summary>
public sealed class MemoryDictionaryStore<TKey,TValue>:CollectionStore<TKey,TValue> 
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
{
	//public DictionaryStore(double MutableFraction):base(MutableFraction){}
    internal MemoryDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){}
	public Dictionary<TKey,TValue> Create()=>new MemoryDictionary<TKey,TValue>(this);
}
