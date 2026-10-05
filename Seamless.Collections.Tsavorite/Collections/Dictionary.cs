namespace Seamless.Collections.Tsavorite;

/// <summary>
/// Dictionaryの動作を検証する。
/// Dictionaryの動作を検証する。
/// Dictionaryの動作を検証する。
/// </summary>
[DebuggerTypeProxy(typeof(DictionaryDebugView<,,,>))]
public partial class Dictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    :Collection<
        TKey,
        TValue,
        TKeySerializer,
        TValueSerializer
    >,
    IDictionary<TKey,TValue> where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public bool IsReadOnly => false;
    public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = this.Read(item.Key,out var output);
        if(status.NotFound)
            return false;
        return EqualityComparer<TValue>.Default.Equals(output,item.Value);
    }
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var kvp in this) {
            array[arrayIndex++]=kvp;
        }
    }
    IEnumerator IEnumerable.GetEnumerator() {
        this.VerifyThreadAffinity();
        return this.GetEnumerator();
    }
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() {
        this.VerifyThreadAffinity();
        return this.GetEnumerator();
    }
    public List<KeyValuePair<TKey,TValue>> ToList() {
        using var Enumerator = this.GetEnumerator();
        var capacity = checked((int)Math.Min(this.EntryCount,int.MaxValue));
        var List = new List<KeyValuePair<TKey,TValue>>(capacity);
        while(Enumerator.MoveNext()) {
            List.Add(Enumerator.Current);
        }
        return List;
    }
    public ICollection<TKey> Keys => this.CreateCollectionView<KeyIterator,TKey>();
    public ICollection<TValue> Values => this.CreateCollectionView<ValueIterator,TValue>();
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Values;
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        // 追加のセッションは基底の Dispose が DisposeAdditionalSessions で破棄する(ここで先に破棄すると二重になる)。
        base.Dispose(disposing);
    }
}



public partial class Dictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        TValue,//TOutput
        EqualityEmpty,//TContext
        Remove_TKey_TValue<TValue,TValueSerializer>,//TFunctions
        SpanByteStoreFunctions,//TStoreFunctions
        SpanByteStoreAllocator//TAllocator
    > Remove_TKey_TValue_ClientSession { get; set; } = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        TValue,//TInput
        bool,//TOutput
        EqualityEmpty,//TContext
        Remove_KeyValuePair<TValue,TValueSerializer>,//TFunctions
        SpanByteStoreFunctions,//TStoreFunctions
        SpanByteStoreAllocator//TAllocator
    > Remove_KeyValuePair_ClientSession { get; set; } = null!;
    // 代入用のセッション。セッションの数には上限があるので、代入を使う辞書だけが最初の代入で作る。
    private ClientSession<
        FixedSpanByteKey,//TKey
        TValue,//TInput
        bool,//TOutput
        EqualityEmpty,//TContext
        SetValue<TValue,TValueSerializer>,//TFunctions
        SpanByteStoreFunctions,//TStoreFunctions
        SpanByteStoreAllocator//TAllocator
    >? SetValue_ClientSession;
    internal Dictionary(CollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
        try {
            this.CreateAdditionalSessions();
        } catch {
            this.AbortConstruction();
            throw;
        }
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.Remove_TKey_TValue_ClientSession);
        CompletePendingAndDispose(this.Remove_KeyValuePair_ClientSession);
        var setValue = this.SetValue_ClientSession;
        this.SetValue_ClientSession=null;
        if(setValue is not null) {
            CompletePendingAndDispose(setValue);
        }
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Remove_TKey_TValue<TValue,TValueSerializer>//TFunctions
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            TValue,//TInput
            bool,//TOutput
            EqualityEmpty,//TContext
            Remove_KeyValuePair<TValue,TValueSerializer>//TFunctions
        >(new());
    }
    public unsafe bool Remove(KeyValuePair<TKey,TValue> item) {
        this.VerifyThreadAffinity();
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes = TKeySerializer.Serialize(ref Key0);
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=Session.BasicContext.RMW(FixedKey,ref Value0,ref Output);
            Pending(Session,Status,ref Output);
        }
        if(Output) {
            this.OnRemoved();
        }
        return Output;
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
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<TKey,TValue>
    > GetEnumerator() {
        this.VerifyThreadAffinity();
        return new(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() {
        this.VerifyThreadAffinity();
        return new Enumerator<MemoryKeyValueIterator,KeyValuePair<TKey,TValue>>(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    private ReadOnlyCollectionView<TIterator,T> CreateCollectionView<TIterator, T>()
        where TIterator : IterateLookupIteratorFunctions_List<T>, ICreate<TIterator> =>
        new(this);

    private sealed class ReadOnlyCollectionView<TIterator, T>(
        Dictionary<TKey,TValue,TKeySerializer,TValueSerializer> dictionary
    ):ICollection<T>
        where TIterator : IterateLookupIteratorFunctions_List<T>, ICreate<TIterator> {
        public int Count => checked((int)dictionary.GetLongCount());
        public bool IsReadOnly => true;
        public void Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
        public bool Contains(T item) {
            var comparer = EqualityComparer<T>.Default;
            using var e = this.GetEnumerator();
            while(e.MoveNext()) {
                if(comparer.Equals(e.Current,item))
                    return true;
            }
            return false;
        }
        public void CopyTo(T[] array,int arrayIndex) {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if(array.Length-arrayIndex<this.Count)
                throw new ArgumentException("The destination array is too small.");
            using var e = this.GetEnumerator();
            while(e.MoveNext()) {
                array[arrayIndex++]=e.Current;
            }
        }
        public Enumerator<TIterator,T> GetEnumerator() {
            dictionary.VerifyThreadAffinity();
            return new(dictionary.ReadSession,TIterator.Create(),dictionary.Store.Log.TailAddress,dictionary);
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
    public TValue this[TKey key] {
        get {
            this.VerifyThreadAffinity("Item");
            if(this.TryGetValue(key,out var value))
                return value;
            throw new KeyNotFoundException();
        }
        set {
            this.VerifyThreadAffinity("Item");
            var KeyBytes = TKeySerializer.Serialize(ref key);
            var ValueLength = TValueSerializer.Serialize(ref value).Length;
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueLength))
                throw this.ThrowLogFull();
            // Upsert は挿入か上書きかを返さないので、RMW で書いて新規かどうかを受け取り、件数を保つ。
            var ClientSession = this.SetValue_ClientSession??=this.Store.NewSession<
                FixedSpanByteKey,//TKey
                TValue,//TInput
                bool,//TOutput
                EqualityEmpty,//TContext
                SetValue<TValue,TValueSerializer>//TFunctions
            >(new());
            var Output = false;
            Status Status;
            unsafe {
                fixed(byte* _ = KeyBytes) {
                    var FixedKey = (FixedSpanByteKey)KeyBytes;
                    Status=ClientSession.BasicContext.RMW(FixedKey,ref value,ref Output);
                    Status=Pending(ClientSession,Status,ref Output);
                }
            }
            Debug.Assert(Status.IsCompleted);
            if(Output) {
                this.OnInserted();
            }
        }
    }
}
