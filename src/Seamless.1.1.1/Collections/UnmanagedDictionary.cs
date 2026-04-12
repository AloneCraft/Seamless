using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.Collections;

[DebuggerDisplay("Count = {Count}")]
public abstract partial class UnmanagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {

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
            UnmanagedRecordDisposer<TKey,TValue>
        >,
        BlittableAllocator<
            TKey,
            TValue,
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<TKey>,
                UnmanagedRecordDisposer<TKey,TValue>
            >
        >
    > Remove_TKey_TValue_ClientSession { get; set; }
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
            UnmanagedRecordDisposer<TKey,TValue>
        >,
        BlittableAllocator<
            TKey,
            TValue,
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<TKey>,
                UnmanagedRecordDisposer<TKey,TValue>
            >
        >
    > Remove_KeyValuePair_ClientSession { get; set; }
    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
        var store = collectionStore.Store;
        this.Remove_TKey_TValue_ClientSession = store.NewSession<EqualityEmpty,TValue,EqualityEmpty,UnmanagedRemove_TKey_TValue<TKey,TValue>>(new());
        this.Remove_KeyValuePair_ClientSession = store.NewSession<TValue,bool,EqualityEmpty,UnmanagedRemove_KeyValuePair<TKey,TValue>>(new());
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<
            TKey,
            TValue
        >
    > GetEnumerator()=>new(this.ReadClientSession,new());
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
    private List<T> CreateCollection<TIterator,T>(TIterator iterator) where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
        var collection = new List<T>();
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()) {
            collection.Add(e.Current);
        }
        return collection;
    }
    public partial bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var key = item.Key;
        var input = item.Value;
        var output = false;
        var status = Session.BasicContext.RMW(ref key,ref input,ref output);
        Pending(Session,status,ref output);
        return output;
    }

    public partial bool Remove(TKey key,out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var input = default(EqualityEmpty);
        var output = default(TValue);
        var status = Session.BasicContext.RMW(ref key,ref input,ref output);
        status = Pending(Session,status,ref output);
        if(status.Found) {
            value = output;
            return true;
        }
        value = default;
        return false;
    }

    //public bool ContainsKey(TKey key) => this.Contains(key);

    //public bool Remove(KeyValuePair<TKey,TValue> item) {
    //    if(!this.TryGetValue(item.Key, out var existing) || !existing.Equals(item.Value))
    //        return false;
    //    return this.Remove(item.Key);
    //}

    //public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
    //    var session = this.Remove_TKey_TValue_ClientSession;
    //    var input = default(EqualityEmpty);
    //    var output = default(TValue);
    //    var status = session.BasicContext.RMW(ref key,ref input,ref output);
    //    status = Pending(session,status,ref output);
    //    if(status.Found) {
    //        value = output;
    //        return true;
    //    }
    //    value = default;
    //    return false;
    //}
    //public Enumerator<
    //    UnmanagedKeyValueIterator<TKey,TValue>,
    //    KeyValuePair<TKey,TValue>
    //> GetEnumerator()=>new(this.ReadClientSession,new());

    //private List<T> CreateCollection<TIterator,T>(TIterator iterator)
    //    where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
    //    var collection = new List<T>();
    //    using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
    //    while(e.MoveNext()) {
    //        collection.Add(e.Current);
    //    }
    //    return collection;
    //}

    //private IEnumerable<T> Enumerate<TIterator,T>(TIterator iterator)
    //    where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
    //    using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
    //    while(e.MoveNext()) {
    //        yield return e.Current;
    //    }
    //}

    //public override bool IsDisposed => this.disposed;
    //private bool disposed;

    //protected override void Dispose(bool disposing) {
    //    if(this.disposed) {
    //        return;
    //    }
    //    this.disposed = true;
    //    if(disposing) {
    //        this.Remove_TKey_TValue_ClientSession.Dispose();
    //    }
    //    base.Dispose(disposing);
    //}

    //public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
    //    ArgumentNullException.ThrowIfNull(array);
    //    ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
    //    if(array.Length-arrayIndex<this.GetLongCount()) {
    //        throw new ArgumentException("The destination array is too small.");
    //    }
    //    foreach(var kvp in this) {
    //        array[arrayIndex++] = kvp;
    //    }
    //}

    //IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    //IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();

    //public List<KeyValuePair<TKey,TValue>> ToList() {
    //    using var e = this.GetEnumerator();
    //    var list = new List<KeyValuePair<TKey,TValue>>();
    //    while(e.MoveNext()) {
    //        list.Add(e.Current);
    //    }
    //    return list;
    //}

    //public ICollection<TKey> Keys => this.CreateCollection<UnmanagedKeyIterator<TKey,TValue>,TKey>(new());
    //public ICollection<TValue> Values => this.CreateCollection<UnmanagedValueIterator<TKey,TValue>,TValue>(new());
    //IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Enumerate<UnmanagedKeyIterator<TKey,TValue>,TKey>(new());
    //IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Enumerate<UnmanagedValueIterator<TKey,TValue>,TValue>(new());
    private partial void SetValue(TKey key,TValue value){
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
            throw this.ThrowLogFull();
        }
        var session = this.AddClientSession;
        var status = session.BasicContext.Upsert(ref key,ref value);
        status=Pending(session,status);
    }

    //public TValue this[TKey key] {
    //    get {
    //        if(this.TryGetValue(key,out var value)) {
    //            return value;
    //        }
    //        throw new KeyNotFoundException();
    //    }
    //    set {
    //        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
    //            throw this.ThrowLogFull();
    //        }
    //        var session = this.AddClientSession;
    //        var status = session.BasicContext.Upsert(ref key,ref value);
    //        _ = Pending(session,status);
    //    }
    //}
}
