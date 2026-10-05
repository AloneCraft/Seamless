namespace Seamless.Collections.Tsavorite;

/// <summary>
/// Tsavorite の辞書のマルチ用の操作用オブジェクト(<see cref="IParallelDictionary{TKey,TValue}"/> の実装)。
/// 代入は挿入か上書きかを数えないので、Upsert でそのまま書く(シングル用より速い)。
/// </summary>
public sealed class ParallelDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
    :Collection<TKey,TValue,TKeySerializer,TValueSerializer,ParallelAccess>, IParallelDictionary<TKey,TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        TValue,//TOutput
        EqualityEmpty,//TContext
        Remove_TKey_TValue<TValue,TValueSerializer>,//TFunctions
        SpanByteStoreFunctions,//TStoreFunctions
        SpanByteStoreAllocator//TAllocator
    > Remove_TKey_TValue_ClientSession { get; set; } = null!;
    internal ParallelDictionary(CollectionStore<TKey,TValue> collectionStore,ParallelRegionState region) : base(collectionStore,region) {
        try {
            this.CreateAdditionalSessions();
        } catch {
            this.AbortConstruction();
            throw;
        }
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.Remove_TKey_TValue_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        this.Remove_TKey_TValue_ClientSession=this.Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Remove_TKey_TValue<TValue,TValueSerializer>//TFunctions
        >(new());
    }
    public TValue this[TKey key] {
        get {
            this.VerifyThreadAffinity("Item");
            if(this.TryGetValue(key,out var value))
                return value;
            throw new KeyNotFoundException();
        }
        set => this.PrivateUpsert(key,value);
    }
    public unsafe bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
        this.VerifyThreadAffinity();
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        TValue Output = default!;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
            Status=Pending(Session,Status,ref Output);
        }
        if(!Status.NotFound) {
            this.OnRemoved();
            value=Output;
            return true;
        }
        value=default;
        return false;
    }
    /// <summary>全件を数える(毎回全走査)。他のスレッドが書いている途中でも呼べるが、その時点の値ではない。</summary>
    public long ScanCount() => this.ScanCountCore();
    public Enumerator<KeyValueIterator,KeyValuePair<TKey,TValue>> GetEnumerator() {
        this.VerifyThreadAffinity();
        return new(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();
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
