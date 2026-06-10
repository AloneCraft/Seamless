using Tsavorite.SessionFunctions;
using Tsavorite.Stores;
namespace Tsavorite.Collections;

public abstract class Collection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue
    TValue,//TReadOutput
    TValue,//TAddInput
    Read<TValue,TValueSerializer>,//TReadFunctions
    Upsert<TValue,TValueSerializer>,//TAddFunctions
    ContainsDelete,//TDeleteFunctions
    SpanByteStoreFunctions,//TStoreFunctions
    SpanByteStoreAllocator//TAllocator
>    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions_List<TKey,ListBase<TKey>>,ICreate<KeyIterator> {
        public static KeyIterator Create() => new();
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class MemoryKeyIterator:MemoryScanIteratorFunctions<TKey,ListBase<TKey>> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class ValueIterator:ScanIteratorFunctions_List<TValue,ListBase<TValue>>,ICreate<ValueIterator> {
        public static ValueIterator Create() => new();
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    private protected sealed class MemoryValueIterator:MemoryScanIteratorFunctions<TValue,ListBase<TValue>> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions_List<KeyValuePair<TKey,TValue>,ListBase<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    private protected sealed class MemoryKeyValueIterator:MemoryScanIteratorFunctions<KeyValuePair<TKey,TValue>,ListBase<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public struct Enumerator<TIterator, T>:IEnumerator<T>
        where TIterator : ScanIteratorFunctions_List<T,ListBase<T>> {
        private ListBase<T>? Items;
        private long Index = -1;
        private T CurrentValue = default!;
        private readonly ClientSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Read<TValue,TValueSerializer>,
            SpanByteStoreFunctions,
            SpanByteStoreAllocator
        > _session;
        private readonly TIterator _iterator;
        public Enumerator(ClientSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            Read<TValue,TValueSerializer>,
            SpanByteStoreFunctions,
            SpanByteStoreAllocator
        > session,
        TIterator iterator) {
            this._session=session;
            this._iterator=iterator;
            try {
                var Items = new InMemoryList<T>(Const.Chunk_Length);
                this._iterator.SetList(Items);
                var ScanFunctions = this._iterator;
                var cursor = 0L;
                this._session.IterateLookup(ref ScanFunctions,ref cursor,resetCursor: false);
                this._session.BasicContext.CompletePending(wait: true);
                this.Items=Items;
            }
            catch {
                this.DisposeItems();
                throw;
            }
        }
        public readonly T Current => this.CurrentValue;
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(this.Items is null)
                return false;
            var NextIndex = this.Index+1;
            if(NextIndex>=this.Items.Count) {
                this.Index=-1;
                return false;
            }
            this.Index=NextIndex;
            this.CurrentValue=this.Items[this.Index];
            return true;
        }
        private void DisposeItems() {
            this.Items?.Dispose();
            this.Items=null;
        }
        public void Reset() {
            this.Index=-1;
            this.CurrentValue=default!;
        }
        public void Dispose() {
            this.DisposeItems();
        }
    }
    private protected Collection(CollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool ContainsKey(TKey Key) {
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool Contains(TKey Key) {
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    private protected override unsafe Status Read(TKey Key,out TValue Value) {
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Session = this.ReadSession;
        TValue Output = default!;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=Session.BasicContext.Read(FixedKey,ref Output);
            Status=Pending(Session,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        if(Status.Found)
            Value=Output;
        else
            Value=default!;
        return Status;
    }
    public unsafe bool Remove(TKey key) {
        var ClientSession = this.ContainsDeleteSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var Input = default(EqualityEmpty);
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            Status=ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        return Output;
    }
    private protected override unsafe TryAddResult PrivateTryAdd(TKey key,TValue value) {
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var serializedValueSize = TValueSerializer.Serialize(ref value).Length;
        if(this.WouldOverflowMutable(KeyBytes.Length,serializedValueSize))
            return TryAddResult.LogFull;
        var ClientSession = this.Add_ClientSession;
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=ClientSession.BasicContext.RMW(FixedKey,ref value,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }

    private protected unsafe void PrivateUpsert(TKey key,TValue value) {
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var ValueBytes = TValueSerializer.Serialize(ref value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length))
            throw this.ThrowLogFull();
        var ClientSession = this.Add_ClientSession;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            Status=ClientSession.BasicContext.Upsert(FixedKey,ValueBytes);
            Pending(ClientSession,Status);
        }
    }
}
