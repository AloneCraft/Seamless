using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;
namespace Seamless.Collections;

public partial class ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    private ClientSession<
        FixedSpanByteKey,//TKey

        EqualityEmpty,//TInput
        TValue,//TOutput
        EqualityEmpty,//TContext
        ManagedRemove_TKey_TValue<TValue,TValueSerializer>,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator//TAllocator
    > Remove_TKey_TValue_ClientSession { get; }
    private ClientSession<
        FixedSpanByteKey,//TKey

        TValue,//TInput
        bool,//TOutput
        EqualityEmpty,//TContext
        Remove_KeyValuePair<TValue,TValueSerializer>,//TFunctions
        ManagedStoreFunctions,//TStoreFunctions
        ManagedAllocator//TAllocator
    > Remove_KeyValuePair_ClientSession { get; }
    internal ManagedDictionary(ManagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            ManagedRemove_TKey_TValue<TValue,TValueSerializer>//TFunctions
        >(new());
        this.Remove_KeyValuePair_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            TValue,//TInput
            bool,//TOutput
            EqualityEmpty,//TContext
            Remove_KeyValuePair<TValue,TValueSerializer>//TFunctions
        >(new());
    }
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes = TKeySerializer.Serialize(ref Key0);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = false;
        var Status = Session.BasicContext.RMW(FixedKey,ref Value0,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output;
    }
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(EqualityEmpty);
        TValue Output = default!;
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
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
    > GetEnumerator() => new(this.ReadClientSession,new());
    IEnumerator<KeyValuePair<TKey,TValue>> IMemoryCollection<KeyValuePair<TKey,TValue>>.GetEnumerator() => new Enumerator<MemoryKeyValueIterator,KeyValuePair<TKey,TValue>>(this.ReadMemoryClientSession,new(),this.HeadAddress);
    private List<T> CreateCollection<TIterator, T>(TIterator Iterator) where TIterator : ScanIteratorFunctions<T> {
        var Collection = new List<T>();
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,Iterator);
        while(e.MoveNext()) {
            Collection.Add(e.Current);
        }
        return Collection;
    }
    private IEnumerable<T> Enumerate<TIterator, T>(TIterator Iterator)
        where TIterator : ScanIteratorFunctions<T> {
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,Iterator);
        while(e.MoveNext())
            yield return e.Current;
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
            var ClientSession = this.AddClientSession;
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            // インメモリストアでは Upsert は Pending にならないのでピン止め不要
            var Status = ClientSession.BasicContext.Upsert(FixedKey,ValueBytes);
            Status=Pending(ClientSession,Status);
        }
    }
}
