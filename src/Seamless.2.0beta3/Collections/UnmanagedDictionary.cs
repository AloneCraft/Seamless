using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using System.Diagnostics;
using Seamless.Stores;
namespace Seamless.Collections;

[DebuggerDisplay("Count = {Count}")]
public abstract partial class UnmanagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>:UnmanagedCollection<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>, ISeamlessDictionary<TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue> {

    private ClientSession<
        FixedSpanByteKey,
        EqualityEmpty,
        SpanByteAndMemory,
        EqualityEmpty,
        Remove_TKey_TValue,
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<
        FixedSpanByteKey,
        PinnedSpanByte,
        bool,
        EqualityEmpty,
        Remove_KeyValuePair,
        UnmanagedStoreFunctions,
        UnmanagedAllocator
    > Remove_KeyValuePair_ClientSession { get; set; }

    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
        var store = collectionStore.Store;
        this.Remove_TKey_TValue_ClientSession = store.NewSession<FixedSpanByteKey,EqualityEmpty,SpanByteAndMemory,EqualityEmpty,Remove_TKey_TValue>(new());
        this.Remove_KeyValuePair_ClientSession = store.NewSession<FixedSpanByteKey,PinnedSpanByte,bool,EqualityEmpty,Remove_KeyValuePair>(new());
    }
    public Enumerator<
        KeyValueIterator,
        KeyValuePair<
            TKey,
            TValue
        >
    > GetEnumerator()=>new(this.ReadClientSession,new());
    private IEnumerable<T> Enumerate<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T> {
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
    private List<T> CreateCollection<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T> {
        var collection = new List<T>();
        using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
        while(e.MoveNext()) {
            collection.Add(e.Current);
        }
        return collection;
    }
    public partial bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes =TKeySerializer.Serialize(ref Key0);
        var ValueBytes =TValueSerializer.Serialize(ref Value0);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = false;
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output;
    }

    public partial bool Remove(TKey key,out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes =TKeySerializer.Serialize(ref key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(EqualityEmpty);
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        // output.Length>0 は InPlaceUpdater/CopyUpdater で値がコピーされた＝成功
        if(Output.Length>0) {
            value=TValueSerializer.Deserialize(Output.ReadOnlySpan);
            return true;
        }
        //Unsafe.SkipInit(out value);
        value =default;
        return false;
    }

    //public Enumerator<
    //    KeyValueIterator<
    //        TKey,
    //        TValue,
    //        TKeySerializer,
    //        TValueSerializer
    //    >,
    //    KeyValuePair<TKey,TValue>
    //> GetEnumerator()=>new(this.ReadClientSession,new());

    //private List<T> CreateCollection<TIterator,T>(TIterator iterator)
    //    where TIterator:ScanIteratorFunctions<T> {
    //    var collection = new List<T>();
    //    using var e = new Enumerator<TIterator,T>(this.ReadClientSession,iterator);
    //    while(e.MoveNext()) {
    //        collection.Add(e.Current);
    //    }
    //    return collection;
    //}

    //private IEnumerable<T> Enumerate<TIterator,T>(TIterator iterator)
    //    where TIterator:ScanIteratorFunctions<T> {
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
    //        this.Remove_KeyValuePair_ClientSession.Dispose();
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

    //public List<KeyValuePair<TKey,TValue>> toList() {
    //    using var e = this.GetEnumerator();
    //    var list = new List<KeyValuePair<TKey,TValue>>();
    //    while(e.MoveNext()) {
    //        list.Add(e.Current);
    //    }
    //    return list;
    //}

    //public ICollection<TKey> Keys => this.CreateCollection<KeyIterator<TKey,TKeySerializer>,TKey>(new());
    //public ICollection<TValue> Values => this.CreateCollection<ValueIterator<TValue,TValueSerializer>,TValue>(new());
    //IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Enumerate<KeyIterator<TKey,TKeySerializer>,TKey>(new());
    //IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Enumerate<ValueIterator<TValue,TValueSerializer>,TValue>(new());
    private partial void SetValue(TKey key,TValue value){
        var keyBytes =TKeySerializer.Serialize(ref key);
        var valueBytes =TValueSerializer.Serialize(ref value);
        if(this.WouldOverflowMutable(keyBytes.Length,valueBytes.Length)) {
            throw this.ThrowLogFull();
        }
        var session = this.AddClientSession;
        var fixedKey = (FixedSpanByteKey)keyBytes;
        var input = valueBytes;
        var status = session.BasicContext.Upsert(fixedKey,input);
        status = Pending(session,status);
    }
    //public TValue this[TKey key] {
    //    get {
    //        if(this.TryGetValue(key,out var value)) {
    //            return value;
    //        }
    //        throw new KeyNotFoundException();
    //    }
    //    set {
    //        var KeyBytes =this.SerializeKey(ref key);
    //        var ValueBytes =this.SerializeValue(ref value);
    //        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
    //            throw this.ThrowLogFull();
    //        }
    //        var Session = this.AddClientSession;
    //        var FixedKey = (FixedSpanByteKey)KeyBytes;
    //        var Input = ValueBytes;
    //        var Status = Session.BasicContext.Upsert(FixedKey,Input);
    //        Status = Pending(Session,Status);
    //    }
    //}
}
