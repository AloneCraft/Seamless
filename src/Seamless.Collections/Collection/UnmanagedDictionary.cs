using Garnet.common;
using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;

namespace Seamless.Collections;

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(DictionaryDebugView<,>))]
public abstract class UnmanagedDictionary<TKey, TValue> : UnmanagedCollection<TKey, TValue>, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

#if TSAVORITE2_0BETA3
    private ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Remove_TKey_TValue, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Remove_KeyValuePair, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> Remove_KeyValuePair_ClientSession { get; set; }

    internal UnmanagedDictionary(CollectionStore<TKey, TValue> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Remove_TKey_TValue>(new());
        this.Remove_KeyValuePair_ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Remove_KeyValuePair>(new());
    }
#elif TSAVORITE1_1_1
    private ClientSession<SpanByte, SpanByte, SpanByte, SpanByteAndMemory, Empty, Remove_TKey_TValue, StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>>> Remove_TKey_TValue_ClientSession { get; set; }
    private ClientSession<SpanByte, SpanByte, SpanByte, SpanByteAndMemory, Empty, Remove_KeyValuePair, StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>>> Remove_KeyValuePair_ClientSession { get; set; }

    internal UnmanagedDictionary(CollectionStore<TKey, TValue> CollectionStore) : base(CollectionStore) {
        var Store = CollectionStore.Store;
        this.Remove_TKey_TValue_ClientSession = Store.NewSession<SpanByte, SpanByteAndMemory, Empty, Remove_TKey_TValue>(new());
        this.Remove_KeyValuePair_ClientSession = Store.NewSession<SpanByte, SpanByteAndMemory, Empty, Remove_KeyValuePair>(new());
    }
#endif

    public bool IsReadOnly => false;

    public bool TryGetValue(TKey Key, [MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key, out var Output);
        if(Status.NotFound) {
            Value = default;
            return false;
        }
        Value = Deserialize<TValue>(GetReadOnlySpan(Output));
        return true;
    }

    public TryAddResult TryAdd(TKey Key, TValue Value) =>
        this.PrivateTryAdd(Key, Value);

    public void Add(TKey Key, TValue Value) {
        var Result = this.TryAdd(Key, Value);
        switch(Result) {
            case TryAddResult.LogFull:
                throw this.ThrowLogFull();
            case TryAddResult.KeyAlreadyExists:
                throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
        }
    }

    public void Add(KeyValuePair<TKey, TValue> item) => this.Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) {
        if(!this.TryGetValue(item.Key, out var value)) {
            return false;
        }
        return value.Equals(item.Value);
    }

    public bool ContainsKey(TKey Key) => this.Contains(Key);

#if TSAVORITE2_0BETA3
    public bool Remove(KeyValuePair<TKey, TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var KeyBytes = Serialize(item.Key);
        var ValueBytes = Serialize(item.Value);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        var Output = default(SpanByteAndMemory);
        var Status = Session.BasicContext.RMW(FixedKey, ref Input, ref Output);
        Status = Pending(Session, Status, ref Output);
        return Output.Length > 0;
    }

    public bool Remove(TKey key, out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = Serialize(key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = default(PinnedSpanByte);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(FixedKey, ref Input, ref Output);
        Status = Pending(Session, Status, ref Output);
        if(Status.Found) {
            value = Deserialize<TValue>(Output.ReadOnlySpan);
            return true;
        }
        value = default;
        return false;
    }

    public Enumerator<TKey, TValue, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, UnmanagedKeyValueIterator<TKey, TValue>, KeyValuePair<TKey, TValue>> GetEnumerator() => new(this.ClientSession, new());

    private IEnumerable<T> Enumerate<TIterator, T>(TIterator Iterator)
        where TIterator : ScanIteratorFunctions<T> {
        using var e = new Enumerator<TKey, TValue, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, TIterator, T>(this.ClientSession, Iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
#elif TSAVORITE1_1_1
    public bool Remove(KeyValuePair<TKey, TValue> item) {
        var Session = this.Remove_KeyValuePair_ClientSession;
        var KeyBytes = Serialize(item.Key);
        var ValueBytes = Serialize(item.Value);
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = SpanByte.FromPinnedSpan(ValueBytes);
        var Status = Session.BasicContext.RMW(FixedKey, Input, out var Output);
        Status = Pending(Session, Status, ref Output);
        return Output.Length > 0;
    }

    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value) {
        var Session = this.Remove_TKey_TValue_ClientSession;
        var KeyBytes = Serialize(key);
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(SpanByte);
        var Status = Session.BasicContext.RMW(FixedKey, Input, out var Output);
        Status = Pending(Session, Status, ref Output);
        if(Status.Found) {
            value = Deserialize<TValue>(Output.AsReadOnlySpan());
            return true;
        }
        value = default;
        return false;
    }

    public Enumerator<TKey, TValue, StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>, UnmanagedKeyValueIterator<TKey, TValue>, KeyValuePair<TKey, TValue>> GetEnumerator() => new(this.ClientSession, new());

    private IEnumerable<T> Enumerate<TIterator, T>(TIterator Iterator)
        where TIterator : ScanIteratorFunctions<T> {
        using var e = new Enumerator<TKey, TValue, StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>, TIterator, T>(this.ClientSession, Iterator);
        while(e.MoveNext()) {
            yield return e.Current;
        }
    }
#endif

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length - arrayIndex < this.GetLongCount()) {
            throw new ArgumentException("The destination array is too small.");
        }
        foreach(var kvp in this) {
            array[arrayIndex++] = kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => this.GetEnumerator();

    public ICollection<TKey> Keys => new KeyCollection(this);
    public ICollection<TValue> Values => new ValueCollection(this);

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => this.Enumerate<UnmanagedKeyIterator<TKey>, TKey>(new());
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => this.Enumerate<UnmanagedValueIterator<TValue>, TValue>(new());

    public TValue this[TKey Key] {
        get {
            if(this.TryGetValue(Key, out var Value)) {
                return Value;
            }
            throw new KeyNotFoundException();
        }
        set {
            var KeyBytes = Serialize(Key);
            var ValueBytes = Serialize(value);
            if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
                throw this.ThrowLogFull();
            }

            var Session = this.ClientSession;
#if TSAVORITE2_0BETA3
            var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Input = ValueBytes;
#elif TSAVORITE1_1_1
            var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
            var Input = SpanByte.FromPinnedSpan(ValueBytes);
#endif
            var Status = Session.BasicContext.Upsert(FixedKey, Input);
            Status = Pending(Session, Status);
        }
    }

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

    private sealed class KeyCollection(UnmanagedDictionary<TKey, TValue> dictionary) : ICollection<TKey> {
        public int Count => dictionary.Count;
        public bool IsReadOnly => true;

        public void Add(TKey item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(TKey item) => dictionary.ContainsKey(item);

        public void CopyTo(TKey[] array, int arrayIndex) {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            foreach(var key in ((IReadOnlyDictionary<TKey, TValue>)dictionary).Keys) {
                if(arrayIndex >= array.Length) {
                    throw new ArgumentException("The destination array is too small.");
                }
                array[arrayIndex++] = key;
            }
        }

        public bool Remove(TKey item) => throw new NotSupportedException();
        public IEnumerator<TKey> GetEnumerator() => ((IReadOnlyDictionary<TKey, TValue>)dictionary).Keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    private sealed class ValueCollection(UnmanagedDictionary<TKey, TValue> dictionary) : ICollection<TValue> {
        public int Count => dictionary.Count;
        public bool IsReadOnly => true;

        public void Add(TValue item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();

        public bool Contains(TValue item) {
            foreach(var value in ((IReadOnlyDictionary<TKey, TValue>)dictionary).Values) {
                if(value.Equals(item)) {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(TValue[] array, int arrayIndex) {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            foreach(var value in ((IReadOnlyDictionary<TKey, TValue>)dictionary).Values) {
                if(arrayIndex >= array.Length) {
                    throw new ArgumentException("The destination array is too small.");
                }
                array[arrayIndex++] = value;
            }
        }

        public bool Remove(TValue item) => throw new NotSupportedException();
        public IEnumerator<TValue> GetEnumerator() => ((IReadOnlyDictionary<TKey, TValue>)dictionary).Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
