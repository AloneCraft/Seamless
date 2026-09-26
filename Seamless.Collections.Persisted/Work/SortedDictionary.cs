using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommonStore=Seamless.Execution.Store;
namespace Seamless.Collections.Persisted.Work;
/// <summary>追加後に明示確定する辞書。同値キーは最後の Add が勝つ。単一スレッド専用。</summary>
public sealed class SortedDictionary<TKey,TValue>:IReadOnlyDictionary<TKey,TValue>,IDisposable where TKey:notnull{
    private readonly SortedWorkData<KeyValuePair<TKey,TValue>,TKey> data;
    internal SortedDictionary(CommonStore store,string name,IComparer<TKey>? comparer,string? comparisonId,long? sortMemoryBytes,StorageTuning? tuning){
        this.data=new(store,name,"dictionary",static pair=>pair.Key,comparer,comparisonId,true,sortMemoryBytes,tuning);
    }
    public int Count=>checked((int)this.data.Count);
    public long LongCount=>this.data.Count;
    public long AddedCount=>this.data.AddedCount;
    public bool IsReady=>this.data.IsReady;
    public TValue this[TKey key]{get{if(this.TryGetValue(key,out var value)){return value;}throw new KeyNotFoundException();}}
    public IEnumerable<TKey> Keys{get{foreach(var pair in this.data.Enumerate()){yield return pair.Key;}}}
    public IEnumerable<TValue> Values{get{foreach(var pair in this.data.Enumerate()){yield return pair.Value;}}}
    public void Add(TKey key,TValue value){ArgumentNullException.ThrowIfNull(key);this.data.Add(new(key,value));}
    public void CompleteAdding()=>this.data.CompleteAdding();
    public bool ContainsKey(TKey key){ArgumentNullException.ThrowIfNull(key);return this.data.Find(key)>=0;}
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)]out TValue value){
        ArgumentNullException.ThrowIfNull(key);
        var position=this.data.Find(key);
        if(position>=0){value=this.data.At(position).Value;return true;}
        value=default;return false;
    }
    public IEnumerable<KeyValuePair<TKey,TValue>> Range(TKey lower,TKey upper)=>this.data.Range(lower,upper);
    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator()=>this.data.Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public void Dispose()=>this.data.Dispose();
}
