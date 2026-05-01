using Seamless.SessionFunctions;

using Seamless.Stores;
namespace Seamless.Collections;

public partial class UnmanagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    where TKey : unmanaged
    where TValue : unmanaged
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {

    private ClientSession<
        FixedSpanByteKey,

        EqualityEmpty,
        TValue,
        EqualityEmpty,
        UnmanagedRemove_TKey_TValue<TKey,TValue>,
        UnmanagedStoreFunctions,





        UnmanagedAllocator









    > Remove_TKey_TValue_ClientSession { get; set; } = null!;
    private ClientSession<
        FixedSpanByteKey,

        TValue,//TInput
        bool,
        EqualityEmpty,
        Remove_KeyValuePair<TValue,TValueSerializer>,
        UnmanagedStoreFunctions,





        UnmanagedAllocator









    > Remove_KeyValuePair_ClientSession { get; set; } = null!;
    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
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
            FixedSpanByteKey,
            EqualityEmpty,
            TValue,
            EqualityEmpty,
            UnmanagedRemove_TKey_TValue<TKey,TValue>



        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            FixedSpanByteKey,
            TValue,//TInput
            bool,
            EqualityEmpty,
            Remove_KeyValuePair<TValue,TValueSerializer>



        >(new());
    }
    public UnmanagedList<KeyValuePair<TKey,TValue>>.Enumerator GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-dictionary-enumerator");
        try {
            var items = ListStore.Create<KeyValuePair<TKey,TValue>>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new KeyValueIterator();
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            return new(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-dictionary-memory-enumerator");
        try {
            var items = ListStore.Create<KeyValuePair<TKey,TValue>>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new MemoryKeyValueIterator();
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            return new UnmanagedList<KeyValuePair<TKey,TValue>>.Enumerator(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator iterator)
    where TIterator : ScanIteratorFunctions_List<T,UnmanagedList<T>>
    where T : unmanaged =>
        this.CreateCollection<TIterator,T>(iterator);
    private global::System.Collections.Generic.List<T> CreateCollection<TIterator, T>(TIterator iterator)
        where TIterator : ScanIteratorFunctions_List<T,UnmanagedList<T>>
        where T : unmanaged {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-dictionary-collection");
        try {
            using var items = ListStore.Create<T>(workListPath,initialCapacity: Const.Chunk_Length);
            iterator.SetList(items);
            var cursor = 0L;
            this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
            this.ReadSession.BasicContext.CompletePending(wait: true);
            var collection = new global::System.Collections.Generic.List<T>(checked((int)items.Count));
            foreach(var item in items)
                collection.Add(item);
            return collection;
        }
        finally {
            DeleteTemporaryWorkListPath(workListPath);
        }
    }
    public unsafe bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key = item.Key;
        var Value = item.Value;
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=Session.BasicContext.RMW(FixedKey,ref Value,ref Output);
            Pending(Session,Status,ref Output);
        }
        return Output;
    }
    public unsafe bool Remove(TKey key,out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        var Output = default(TValue);
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
        //Unsafe.SkipInit(out value);
        value=default;
        return false;
    }
    private partial void SetValue(TKey key,TValue value) {
        var keyBytes = TKeySerializer.Serialize(ref key);
        var valueBytes = TValueSerializer.Serialize(ref value);
        if(this.WouldOverflowMutable(keyBytes.Length,valueBytes.Length))
            throw this.ThrowLogFull();
        var ClientSession = this.Add_ClientSession;
        var Input = valueBytes;
        Status Status;
        unsafe {
            fixed(byte* _ = keyBytes) {
                var FixedKey = (FixedSpanByteKey)keyBytes;
                Status=ClientSession.BasicContext.Upsert(FixedKey,Input);
            }
        }
        Pending(ClientSession,Status);
    }
}
