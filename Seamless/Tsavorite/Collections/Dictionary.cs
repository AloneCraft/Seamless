using Seamless.Persisted.Collections;
using Seamless.Tsavorite.SessionFunctions;
using Seamless.Tsavorite.Stores;
namespace Seamless.Tsavorite.Collections;

/// <summary>
/// Tsavorite ???o?b?N?G???h??g???I???????? Dictionary?B
/// <see cref="IDictionary{TKey,TValue}"/> ?????????A???p??? Tsavorite ?????????????B
/// ?OI?|?????[?t?B?Y???????z?f?B?X?p?b?`?????????B
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
    IDictionary<TKey,TValue>    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public bool IsReadOnly => false;
    public void Add(KeyValuePair<TKey,TValue> item) => Add(item.Key,item.Value);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = Read(item.Key,out var output);
        if(status.NotFound)
            return false;
        return EqualityComparer<TValue>.Default.Equals(output,item.Value);
    }
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var kvp in this) {
            array[arrayIndex++]=kvp;
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => GetEnumerator();
    public List<KeyValuePair<TKey,TValue>> ToList() {
        using var Enumerator = GetEnumerator();
        var capacity=checked((int)Math.Min(EntryCount,int.MaxValue));
        var List=new List<KeyValuePair<TKey,TValue>>(capacity);
        while(Enumerator.MoveNext()) {
            List.Add(Enumerator.Current);
        }
        return List;
    }
    public ICollection<TKey> Keys => CreateCollectionView<KeyIterator,TKey>();
    public ICollection<TValue> Values => CreateCollectionView<ValueIterator,TValue>();
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => Values;
    public override bool IsDisposed => disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(disposed)
            return;
        disposed=true;
        if(disposing) {
            Remove_TKey_TValue_ClientSession.Dispose();
            Remove_KeyValuePair_ClientSession.Dispose();
        }
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
    internal Dictionary(CollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
        CreateAdditionalSessions();
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(Remove_TKey_TValue_ClientSession);
        CompletePendingAndDispose(Remove_KeyValuePair_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
        Remove_TKey_TValue_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Remove_TKey_TValue<TValue,TValueSerializer>//TFunctions
        >(new());
        Remove_KeyValuePair_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            TValue,//TInput
            bool,//TOutput
            EqualityEmpty,//TContext
            Remove_KeyValuePair<TValue,TValueSerializer>//TFunctions
        >(new());
    }
    public unsafe bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = Remove_KeyValuePair_ClientSession;
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
        return Output;
    }
    public unsafe bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var Session = Remove_TKey_TValue_ClientSession;
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
            value=Output;
            return true;
        }
        value=default;
        return false;
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<TKey,TValue>
    > GetEnumerator() => new(ReadSession,new(),Store.Log.TailAddress);
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() => new Enumerator<MemoryKeyValueIterator,KeyValuePair<TKey,TValue>>(ReadSession,new(),Store.Log.TailAddress);
    private ReadOnlyCollectionView<TIterator,T> CreateCollectionView<TIterator,T>()
        where TIterator : ScanIteratorFunctions_List<T,PersistedList<T>>,ICreate<TIterator> =>
        new(this);

    private sealed class ReadOnlyCollectionView<TIterator,T>(
        Dictionary<TKey,TValue,TKeySerializer,TValueSerializer> dictionary
    ):ICollection<T>
        where TIterator : ScanIteratorFunctions_List<T,PersistedList<T>>,ICreate<TIterator> {
        public int Count => checked((int)dictionary.GetLongCount());
        public bool IsReadOnly => true;
        public void Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
        public bool Contains(T item) {
            var comparer = EqualityComparer<T>.Default;
            using var e = GetEnumerator();
            while(e.MoveNext()) {
                if(comparer.Equals(e.Current,item))
                    return true;
            }
            return false;
        }
        public void CopyTo(T[] array,int arrayIndex) {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if(array.Length-arrayIndex<Count)
                throw new ArgumentException("The destination array is too small.");
            using var e = GetEnumerator();
            while(e.MoveNext()) {
                array[arrayIndex++]=e.Current;
            }
        }
        public Enumerator<TIterator,T> GetEnumerator() => new(dictionary.ReadSession,TIterator.Create(),dictionary.Store.Log.TailAddress);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public TValue this[TKey key] {
        get {
            if(TryGetValue(key,out var value))
                return value;
            throw new KeyNotFoundException();
        }
        set {
            var KeyBytes = TKeySerializer.Serialize(ref key);
            var ValueBytes = TValueSerializer.Serialize(ref value);
            if(WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
                throw ThrowLogFull();
            var ClientSession = Add_ClientSession;
            Status Status;
            unsafe {
                fixed(byte* _ = KeyBytes) {
                    var FixedKey = (FixedSpanByteKey)KeyBytes;
                    var Value = ValueBytes;
                    Status=ClientSession.BasicContext.Upsert(FixedKey,Value);
                }
            }
            Pending(ClientSession,Status);
        }
    }
}
