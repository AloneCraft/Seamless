using System.Collections;
using CommonStore=Seamless.Execution.Store;
namespace Seamless.Collections.Persisted.Work;
/// <summary>追加後に明示確定する集合。確定前は追加順、確定後は比較順・重複除去済みで列挙する。単一スレッド専用。</summary>
public sealed class SortedSet<T>:IReadOnlyCollection<T>,IDisposable{
    private readonly SortedWorkData<T,T> data;
    internal SortedSet(CommonStore store,string name,IComparer<T>? comparer,string? comparisonId,long? sortMemoryBytes,StorageTuning? tuning){
        this.data=new(store,name,"set",static value=>value,comparer,comparisonId,true,sortMemoryBytes,tuning);
    }
    public int Count=>checked((int)this.data.Count);
    public long LongCount=>this.data.Count;
    public long AddedCount=>this.data.AddedCount;
    public bool IsReady=>this.data.IsReady;
    /// <summary>追加すると未確定へ戻る。同値要素の代表値は最後に追加した値。</summary>
    public void Add(T value)=>this.data.Add(value);
    public void CompleteAdding()=>this.data.CompleteAdding();
    public bool Contains(T value)=>this.data.Find(value)>=0;
    /// <summary>確定後の半開区間 [lower,upper) を列挙する。</summary>
    public IEnumerable<T> Range(T lower,T upper)=>this.data.Range(lower,upper);
    public IEnumerator<T> GetEnumerator()=>this.data.Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public void Dispose()=>this.data.Dispose();
}
