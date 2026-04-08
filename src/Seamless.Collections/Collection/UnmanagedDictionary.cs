using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Seamless.Collections;

[DebuggerDisplay("Count = {Count}")]
public abstract class UnmanagedDictionary<TKey,TValue>:UnmanagedCollection<TKey,TValue>, IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>, ISeamlessDictionary<TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {

#if TSAVORITE2_0BETA3
    private ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_TKey_TValue,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_KeyValuePair,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Remove_KeyValuePair_ClientSession { get; set; }

    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
        var store = collectionStore.Store;
        this.Remove_TKey_TValue_ClientSession = store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_TKey_TValue>(new());
        this.Remove_KeyValuePair_ClientSession = store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,core_Empty,Remove_KeyValuePair>(new());
    }

    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out var Output);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=UnmanagedSerializer<TValue>.Deserialize(GetReadOnlySpan(Output));
        return true;
    }
#elif TSAVORITE1_1_1
    private ClientSession<
        TKey,
        TValue,
        TValue,
        TValue,
        core_Empty,
        UnmanagedRemove_TKey_TValue<TKey,TValue>,
        StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
        BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>> Remove_TKey_TValue_ClientSession { get; set; }

    private ClientSession<
        TKey,
        TValue,
        TValue,
        bool,
        core_Empty,
        UnmanagedRemove_KeyValuePair<TKey,TValue>,
        StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>,
        BlittableAllocator<TKey,TValue,StoreFunctions<TKey,TValue,UnmanagedKeyComparer<TKey>,DefaultRecordDisposer<TKey,TValue>>>> Remove_KeyValuePair_ClientSession { get; set; }

    internal UnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore) {
        var store = collectionStore.Store;
        this.Remove_TKey_TValue_ClientSession = store.NewSession<TValue,TValue,core_Empty,UnmanagedRemove_TKey_TValue<TKey,TValue>>(new());
        this.Remove_KeyValuePair_ClientSession = store.NewSession<TValue,bool,core_Empty,UnmanagedRemove_KeyValuePair<TKey,TValue>>(new());
    }
#endif

    public bool IsReadOnly => false;

    public TryAddResult TryAdd(TKey key,TValue value) => this.PrivateTryAdd(key,value);

    public void Add(TKey key,TValue value) {
        var result = this.TryAdd(key,value);
        switch(result) {
            case TryAddResult.LogFull:
                throw this.ThrowLogFull();
            case TryAddResult.KeyAlreadyExists:
                throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
        }
    }

    public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);

#if TSAVORITE2_0BETA3
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = this.Read(item.Key,out var output);
        if(status.NotFound) return false;
        var value = UnmanagedSerializer<TValue>.Deserialize(GetReadOnlySpan(output));
        return value.Equals(item.Value);
    }
#elif TSAVORITE1_1_1
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = this.Read(item.Key,out var output);
        return status.Found && output.Equals(item.Value);
    }
#endif

    public bool ContainsKey(TKey key) => this.Contains(key);

#if TSAVORITE2_0BETA3
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var Key0 = item.Key;
        var Value0 = item.Value;
        var KeyBytes = SerializeKey(ref Key0);
        var ValueBytes = SerializeValue(ref Value0);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Output.Length>0;
    }

    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = SerializeKey(ref key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = default(PinnedSpanByte);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey,ref Input,ref Output);
        Status=Pending(Session,Status,ref Output);
        if(Status.Found) {
            value=UnmanagedSerializer<TValue>.Deserialize(Output.ReadOnlySpan);
            return true;
        }
        value=default;
        return false;
    }

    public Enumerator<TKey,TValue,Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,KeyValueIterator<TKey,TValue,UnmanagedSerializer<TKey>,UnmanagedSerializer<TValue>>,KeyValuePair<TKey,TValue>> GetEnumerator()=>new(this.ClientSession,new());

    private List<T> CreateCollection<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T> {
        var collection = new List<T>();
        using var e = new Enumerator<TKey,TValue,Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,TIterator,T>(this.ClientSession,iterator);
        while(e.MoveNext()) {
            collection.Add(e.Current);
        }
        return collection;
    }

    private IEnumerable<T> Enumerate<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T> {
        using var e = new Enumerator<TKey,TValue,Add,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>,TIterator,T>(this.ClientSession,iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
#elif TSAVORITE1_1_1
    public bool Remove(KeyValuePair<TKey,TValue> item) {
        var session = this.Remove_KeyValuePair_ClientSession;
        var key = item.Key;
        var input = item.Value;
        var output = false;
        var status = session.BasicContext.RMW(ref key,ref input,ref output,default(core_Empty));
        if(status.IsPending) {
            session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
            completedOutputs.Next();
            status = completedOutputs.Current.Status;
            output = completedOutputs.Current.Output;
            completedOutputs.Dispose();
        }
        return status.Found && output;
    }

    public bool Remove(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var session = this.Remove_TKey_TValue_ClientSession;
        var input = default(TValue);
        var output = default(TValue);
        var status = session.BasicContext.RMW(ref key,ref input,ref output,default(core_Empty));
        status = Pending(session,status,ref output);
        if(status.Found) {
            value = output;
            return true;
        }
        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => new UnmanagedEnumerator<TKey,TValue,UnmanagedKeyValueIterator<TKey,TValue>,KeyValuePair<TKey,TValue>>(this.ClientSession,new());

    private List<T> CreateCollection<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
        var collection = new List<T>();
        using var e = new UnmanagedEnumerator<TKey,TValue,TIterator,T>(this.ClientSession,iterator);
        while(e.MoveNext()) {
            collection.Add(e.Current);
        }
        return collection;
    }

    private IEnumerable<T> Enumerate<TIterator,T>(TIterator iterator)
        where TIterator:ScanIteratorFunctions<T,TKey,TValue> {
        using var e = new UnmanagedEnumerator<TKey,TValue,TIterator,T>(this.ClientSession,iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
#endif

    public override bool IsDisposed => this.disposed;
    private bool disposed;

    protected override void Dispose(bool disposing) {
        if(this.disposed) {
            return;
        }
        this.disposed = true;
        if(disposing) {
            this.Remove_TKey_TValue_ClientSession.Dispose();
            this.Remove_KeyValuePair_ClientSession.Dispose();
        }
        base.Dispose(disposing);
    }

    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount()) {
            throw new ArgumentException("The destination array is too small.");
        }
        foreach(var kvp in this) {
            array[arrayIndex++] = kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();

    public List<KeyValuePair<TKey,TValue>> ToList() {
        using var e = this.GetEnumerator();
        var list = new List<KeyValuePair<TKey,TValue>>();
        while(e.MoveNext()) {
            list.Add(e.Current);
        }
        return list;
    }

#if TSAVORITE2_0BETA3
    public ICollection<TKey> Keys => this.CreateCollection<KeyIterator<TKey,UnmanagedSerializer<TKey>>,TKey>(new());
    public ICollection<TValue> Values => this.CreateCollection<ValueIterator<TValue,UnmanagedSerializer<TValue>>,TValue>(new());
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Enumerate<KeyIterator<TKey,UnmanagedSerializer<TKey>>,TKey>(new());
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Enumerate<ValueIterator<TValue,UnmanagedSerializer<TValue>>,TValue>(new());
#elif TSAVORITE1_1_1
    public ICollection<TKey> Keys => this.CreateCollection<UnmanagedKeyIterator<TKey,TValue>,TKey>(new());
    public ICollection<TValue> Values => this.CreateCollection<UnmanagedValueIterator<TKey,TValue>,TValue>(new());
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Enumerate<UnmanagedKeyIterator<TKey,TValue>,TKey>(new());
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Enumerate<UnmanagedValueIterator<TKey,TValue>,TValue>(new());
#endif

    public TValue this[TKey key] {
        get {
            if(this.TryGetValue(key,out var value)) {
                return value;
            }
            throw new KeyNotFoundException();
        }
        set {
#if TSAVORITE2_0BETA3
            var KeyBytes = SerializeKey(ref key);
            var ValueBytes = SerializeValue(ref value);
            if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
                throw this.ThrowLogFull();
            }
            var Session = this.ClientSession;
            var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Input = ValueBytes;
            var Status = Session.BasicContext.Upsert(FixedKey,Input);
            Status = Pending(Session,Status);
#elif TSAVORITE1_1_1
            if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>())) {
                throw this.ThrowLogFull();
            }
            var session = this.ClientSession;
            var status = session.BasicContext.Upsert(ref key,ref value);
            _ = Pending(session,status);
#endif
        }
    }
}
