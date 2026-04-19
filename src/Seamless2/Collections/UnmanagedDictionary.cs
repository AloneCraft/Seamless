using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using Seamless.Stores;
namespace Seamless.Collections;

public partial class UnmanagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
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









    > Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<
        FixedSpanByteKey,

        TValue,//TInput
        bool,
        EqualityEmpty,
        Remove_KeyValuePair<TValue,TValueSerializer>,
        UnmanagedStoreFunctions,





        UnmanagedAllocator









    > Remove_KeyValuePair_ClientSession { get; set; }
    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
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
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<
            TKey,
            TValue
        >
    > GetEnumerator() => new(this.ReadClientSession,new());
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() => new Enumerator<MemoryKeyValueIterator,KeyValuePair<TKey,TValue>>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator iterator)
    where TIterator : ScanIteratorFunctions<T> {
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
    private List<T> CreateCollection<TIterator, T>(TIterator iterator)
        where TIterator : ScanIteratorFunctions<T> {
        var collection = new List<T>();
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()) {
            collection.Add(e.Current);
        }
        return collection;
    }
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key = item.Key;
        var Value = item.Value;
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = false;
        var Status = Session.BasicContext.RMW(FixedKey,ref Value,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output;
    }
    public bool Remove(TKey key,out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(EqualityEmpty);
        var Output = default(TValue);
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
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
        var ClientSession = this.AddClientSession;
        var FixedKey = (FixedSpanByteKey)keyBytes;
        var Input = valueBytes;
        var Status = ClientSession.BasicContext.Upsert(FixedKey,Input);
        Status=Pending(ClientSession,Status);
    }
}
