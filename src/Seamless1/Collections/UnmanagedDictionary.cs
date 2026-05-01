using Seamless.SessionFunctions;
using System.Runtime.CompilerServices;
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
        TKey,
        TValue,
        EqualityEmpty,
        TValue,
        EqualityEmpty,
        UnmanagedRemove_TKey_TValue<TKey,TValue>,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<TKey>,
            DefaultRecordDisposer<TKey,TValue>
        >,
        BlittableAllocator<
            TKey,
            TValue,
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<TKey>,
                DefaultRecordDisposer<TKey,TValue>
            >
        >
    > Remove_TKey_TValue_ClientSession { get; set; } = null!;
    private ClientSession<
        TKey,
        TValue,
        TValue,
        bool,
        EqualityEmpty,
        UnmanagedRemove_KeyValuePair<TKey,TValue>,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<TKey>,
            DefaultRecordDisposer<TKey,TValue>
        >,
        BlittableAllocator<
            TKey,
            TValue,
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<TKey>,
                DefaultRecordDisposer<TKey,TValue>
            >
        >
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

            EqualityEmpty,
            TValue,
            EqualityEmpty,
            UnmanagedRemove_TKey_TValue<
                TKey,
                TValue
            >
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<

            TValue,
            bool,
            EqualityEmpty,
            UnmanagedRemove_KeyValuePair<
                TKey,
                TValue
            >
        >(new());
    }
    public UnmanagedList<KeyValuePair<TKey,TValue>>.Enumerator GetEnumerator() {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-dictionary-enumerator");
        try {
            var items = ListStore.Create<KeyValuePair<TKey,TValue>>(workListPath,initialCapacity: Const.Chunk_Length);
            var iterator = new KeyValueIterator();
            iterator.SetList(items);
            this.IterateLookupAll(this.ReadSession,ref iterator);
            return new UnmanagedList<KeyValuePair<TKey,TValue>>.Enumerator(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
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
            var iterator = new KeyValueIterator();
            iterator.SetList(items);
            this.IterateLookupAll(this.ReadMemoryClientSession,ref iterator);
            return new UnmanagedList<KeyValuePair<TKey,TValue>>.Enumerator(items,ownsList: true,disposeAction: () => DeleteTemporaryWorkListPath(workListPath));
        }
        catch {
            DeleteTemporaryWorkListPath(workListPath);
            throw;
        }
    }
    private global::System.Collections.Generic.List<T> CreateCollection<TIterator, T>(TIterator iterator)
        where TIterator : ScanIteratorFunctions_List<T,UnmanagedList<T>>
        where T : unmanaged {
        var workListPath = CreateTemporaryWorkListPath("unmanaged-dictionary-collection");
        try {
            using var items = ListStore.Create<T>(workListPath,initialCapacity: Const.Chunk_Length);
            iterator.SetList(items);
            this.IterateLookupAll(this.ReadSession,ref iterator);
            var collection = new global::System.Collections.Generic.List<T>(checked((int)items.Count));
            foreach(var item in items)
                collection.Add(item);
            return collection;
        }
        finally {
            DeleteTemporaryWorkListPath(workListPath);
        }
    }
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator iterator)
        where TIterator : ScanIteratorFunctions_List<T,UnmanagedList<T>>
        where T : unmanaged =>
        this.CreateCollection<TIterator,T>(iterator);
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key = item.Key;




        var Input = item.Value;
        var Output = false;
        var Status = Session.BasicContext.RMW(ref Key,ref Input,ref Output);
        Pending(Session,Status,ref Output);
        return Output;
    }
    public bool Remove(TKey key,out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;


        var Input = default(EqualityEmpty);
        var Output = default(TValue);
        var Status = Session.BasicContext.RMW(ref key,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);

        if(Status.Found) {
            value=Output;
            return true;
        }
        //Unsafe.SkipInit(out value);
        value=default;
        return false;
    }
    private partial void SetValue(TKey key,TValue value) {


        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>()))
            throw this.ThrowLogFull();
        var session = this.Add_ClientSession;


        var status = session.BasicContext.Upsert(ref key,ref value);
        Pending(session,status);
    }
}
