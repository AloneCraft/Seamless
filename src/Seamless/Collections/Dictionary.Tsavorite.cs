using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

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
        this.CreateAdditionalSessions();
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.Remove_TKey_TValue_ClientSession);
        CompletePendingAndDispose(this.Remove_KeyValuePair_ClientSession);
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
        return Output;
    }
    public unsafe bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
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
            value=Output;
            return true;
        }
        value=default;
        return false;
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<TKey,TValue>
    > GetEnumerator() => new(this.ReadSession,new());
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() => new Enumerator<MemoryKeyValueIterator,KeyValuePair<TKey,TValue>>(this.ReadSession,new());
    private ReadOnlyCollectionView<TIterator,T> CreateCollectionView<TIterator,T>()
        where TIterator : ScanIteratorFunctions_List<T,ListBase<T>>,ICreate<TIterator> =>
        new(this);

    private sealed class ReadOnlyCollectionView<TIterator,T>(
        Dictionary<TKey,TValue,TKeySerializer,TValueSerializer> dictionary
    ):ICollection<T>
        where TIterator : ScanIteratorFunctions_List<T,ListBase<T>>,ICreate<TIterator> {
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
        public Enumerator<TIterator,T> GetEnumerator() => new(dictionary.ReadSession,TIterator.Create());
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
    public TValue this[TKey key] {
        get {
            if(this.TryGetValue(key,out var value))
                return value;
            throw new KeyNotFoundException();
        }
        set {
            var KeyBytes = TKeySerializer.Serialize(ref key);
            var ValueBytes = TValueSerializer.Serialize(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
                throw this.ThrowLogFull();
            var ClientSession = this.Add_ClientSession;
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
