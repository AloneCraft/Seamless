namespace Seamless.Collections.Persisted.Work;

/// <summary>追加と検索を交互に行う、共通 Store 所有のディスク上の集合。等しい要素は一度だけ保存する。</summary>
public sealed class HashSet<T>:IReadOnlyCollection<T>,IDisposable{
    private readonly HashStore<T,AutoSerializer<T>> store;
    internal HashSet(global::Seamless.Execution.Store owner,string name,IEqualityComparer<T> comparer,string comparerId,StorageTuning? tuning){
        this.store=new(owner,name,"set",comparerId,(path,quota)=>new Records(path,tuning,new HashKeyPolicy<T,AutoSerializer<T>>(comparer,owner.Budget),quota));
    }
    public int Count=>checked((int)this.GetLongCount());
    public long GetLongCount()=>this.store.Records.DistinctCount;
    public bool IsDisposed=>this.store.IsDisposed;
    public bool Add(T item){
        var records=this.store.Records;
        var key=AutoSerializer<T>.Serialize(ref item);
        using var memory=this.store.Budget.ReserveMemory(checked(128+3L*key.Length));
        if(records.Lookup(key,out _)){
            return false;
        }
        records.AddRawBytes(key);
        return true;
    }
    public bool Contains(T item){
        var records=this.store.Records;
        var key=AutoSerializer<T>.Serialize(ref item);
        using var memory=this.store.Budget.ReserveMemory(checked(128+2L*key.Length));
        return records.Lookup(key,out _);
    }
    public IEnumerator<T> GetEnumerator()=>this.store.Enumerate(latestOnly:false).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public void Dispose()=>this.store.Dispose();

    private sealed class Records(string path,StorageTuning? tuning,IPersistedKeyPolicy policy,IPersistedStorageQuota quota)
        :HashRecords<T,AutoSerializer<T>>(path,tuning,policy,quota);
}
