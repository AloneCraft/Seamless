using System.Collections;
using System.Diagnostics;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public abstract class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,//TKey
    TValue,//TValue


    TValue,//TReadOutput
    TValue,//TAddInput
    ManagedRead<TValue,TValueSerializer>,//TReadFunctions
    ManagedAdd<TValue,TValueSerializer>,//TAddFunctions
    ContainsDelete,//TDeleteFunctions
    ManagedStoreFunctions,//TStoreFunctions
    ManagedAllocator//TAllocator
>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed class KeyIterator:ScanIteratorFunctions<TKey> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    public sealed class MemoryKeyIterator:MemoryScanIteratorFunctions<TKey> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    internal sealed class ValueIterator:ScanIteratorFunctions<TValue> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public sealed class MemoryKeyValueIterator:MemoryScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public struct Enumerator<TIterator, T>(
        ClientSession<
            FixedSpanByteKey,//TKey
                             //
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            ManagedRead<TValue,TValueSerializer>,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator,
        long InitialCursor = 0
    ):IEnumerator<T>
        where TIterator : ScanIteratorFunctions<
            T


        > {
        private long Cursor = InitialCursor, Count;
        private int Index = -1;
        private bool Done;
        public readonly T Current => Iterator.Chunk[this.Index];
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(++this.Index<this.Count)
                return true;
            if(this.Done) {
                this.Index=-1;
                return false;
            }
            Iterator.Count=0;
            // ScanCursor can deliver one late callback after the requested count, so keep one slot free.
            var requestCount = Math.Max(1,Const.Chunk_Length-1);
            var r = Session.ScanCursor(ref this.Cursor,requestCount,Iterator,resetCursor: false);
            Session.BasicContext.CompletePending(wait: true);
            var Count = Iterator.Count;
            this.Count=Count;
            if(!r&&Count==0)
                this.Done=true;
            if(Count>0) {
                this.Index=0;
                return true;
            }
            this.Index=-1;
            return false;
        }
        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
        }
        public readonly void Dispose() {
        }
    }
    private protected ManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool Contains(TKey Key) {
        //
        //
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool ContainsKey(TKey Key) => this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    private protected override Status Read(TKey Key,out TValue Value) {
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        var Session = this.ReadClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        TValue Output = default!;
        var Status = Session.BasicContext.Read(FixedKey,ref Output);
        Status=Pending(Session,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        Value=Status.Found ? Output : default!;
        return Status;
    }
    public bool Remove(TKey key) {
        var ClientSession = this.ContainsDeleteClientSession;
        var KeyBytes = TKeySerializer.Serialize(ref key);
        //1.0はFixedKey
        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) {
        var KeyBytes = TKeySerializer.Serialize(ref key);
        var serializedValueSize = TValueSerializer.Serialize(ref value).Length;
        if(this.WouldOverflowMutable(KeyBytes.Length,serializedValueSize))
            return TryAddResult.LogFull;
        var ClientSession = this.AddClientSession;
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Output = false;
        var Status = ClientSession.BasicContext.RMW(FixedKey,ref value,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
