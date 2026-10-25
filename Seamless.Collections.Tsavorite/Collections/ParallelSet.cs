namespace Seamless.Collections.Tsavorite;

/// <summary>
/// キーを持つレコードの集合のマルチ用の操作用オブジェクト。並列区間(<see cref="SetStore{TKey,TRecord}.BeginParallel"/>)の中で、スレッドごとに作る。
/// 件数を管理しないので、件数の窓口は持たない。件数は <see cref="ScanCount"/> が毎回全件を数える。
/// </summary>
[DebuggerDisplay("EntryCount={EntryCount}")]
public sealed class ParallelSet<TKey, TRecord>:Collection<TKey,TRecord,global::Seamless.Collections.Serializers.Auto<TKey>,global::Seamless.Collections.Serializers.Auto<TRecord>,ParallelAccess>, global::Seamless.Collections.IUnorderedEnumerable<TRecord>
    where TRecord : IRecord<TKey> {
    internal ParallelSet(CollectionStore<TKey,TRecord> collectionStore,ParallelRegionState region) : base(collectionStore,region) {
    }
    /// <summary>追加する。同じキーがすでにあれば false。</summary>
    public bool Add(TRecord item) {
        var result = this.PrivateTryAdd(item.Key,item);
        switch(result) {
            case TryAddResult.Success:
                return true;
            case TryAddResult.KeyAlreadyExists:
                return false;
            default:
                Debug.Assert(result==TryAddResult.LogFull);
                throw this.ThrowLogFull();
        }
    }
    public bool Contains(TRecord item) => this.ContainsKey(item.Key);
    public bool Remove(TRecord item) => this.Remove(item.Key);
    /// <summary>全件を数える(毎回全走査)。他のスレッドが書いている途中でも呼べるが、その時点の値ではない。</summary>
    public long ScanCount() => this.ScanCountCore();
    /// <summary>レコードを 1 つずつ <typeparamref name="TStrategy"/> へ渡す。FLinq の生成コードが呼ぶ。</summary>
    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ISetIterateStrategy<TStrategy,TRecord> => this.IterateValuesCore(ref strategy);
    public IEnumerator<TRecord> GetEnumerator() {
        this.VerifyThreadAffinity();
        return new Enumerator<ValueIterator,TRecord>(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        base.Dispose(disposing);
    }
}
