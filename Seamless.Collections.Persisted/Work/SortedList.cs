using System.Collections;
using CommonStore=Seamless.Execution.Store;
namespace Seamless.Collections.Persisted.Work;
/// <summary>重複行を保持する作業リスト。確定後は比較順、同値行は追加順。単一スレッド専用。</summary>
public sealed class SortedList<T>:IReadOnlyList<T>,IDisposable{
    private readonly SortedWorkData<T,T> data;
    internal SortedList(CommonStore store,string name,IComparer<T>? comparer,string? comparisonId,long? sortMemoryBytes,StorageTuning? tuning){
        this.data=new(store,name,"list",static value=>value,comparer,comparisonId,false,sortMemoryBytes,tuning);
    }
    public int Count=>checked((int)this.data.Count);
    public long LongCount=>this.data.Count;
    public long AddedCount=>this.data.AddedCount;
    public bool IsReady=>this.data.IsReady;
    public T this[int index]=>this.data.At(index);
    public T this[long index]=>this.data.At(index);
    public void Add(T value)=>this.data.Add(value);
    public void CompleteAdding()=>this.data.CompleteAdding();
    public bool Contains(T value)=>this.data.Find(value)>=0;
    /// <summary>比較で同値となる最初の行の位置。未確定時は例外。</summary>
    public long IndexOf(T value)=>this.data.Find(value);
    public IEnumerable<T> Range(T lower,T upper)=>this.data.Range(lower,upper);
    public IEnumerator<T> GetEnumerator()=>this.data.Enumerate().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public void Dispose()=>this.data.Dispose();
}
