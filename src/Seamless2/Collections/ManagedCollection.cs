using System.Collections;
using System.Diagnostics;
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
    private protected sealed class MemoryKeyIterator:MemoryScanIteratorFunctions<TKey> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    private protected sealed class ValueIterator:ScanIteratorFunctions<TValue> {
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
    private protected sealed class MemoryKeyValueIterator:MemoryScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
            var Key = TKeySerializer.Deserialize(logRecord.Key);
            var Value = TValueSerializer.Deserialize(logRecord.ValueSpan);
            return new(Key,Value);
        }
    }
    public struct PublicEnumerator<TIterator, T>(
        ClientSession<
            FixedSpanByteKey,
            TValue,
            bool,
            EqualityEmpty,
            ManagedAdd<TValue,TValueSerializer>,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator
    ):IEnumerator<T>
        where TIterator : ScanIteratorFunctions<
            T
        > {
        private T[]? Items;
        private int Index = -1;
        private T CurrentValue = default!;
        public readonly T Current => this.CurrentValue;
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            this.Items??=this.Materialize();
            var NextIndex = this.Index+1;
            if(NextIndex>=this.Items.Length)
                return false;
            this.Index=NextIndex;
            this.CurrentValue=this.Items[this.Index];
            return true;
        }
        private T[] Materialize() {
            var ScanFunctions = new MaterializedScanIteratorFunctions<TIterator,T>(Iterator);
            _=Session.Iterate(ref ScanFunctions);
            _=Session.BasicContext.CompletePending(wait: true);
            return [.. ScanFunctions.Items];
        }
        public void Reset() {
            this.Items=null;
            this.Index=-1;
            this.CurrentValue=default!;
        }
        public readonly void Dispose() {
        }
    }
    public struct Enumerator<TIterator, T>(
        ClientSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            ManagedRead<TValue,TValueSerializer>,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session,
        TIterator Iterator
    ):IEnumerator<T>
        where TIterator : ScanIteratorFunctions<
            T


        > {
        private long Cursor, Count;
        private int Index = -1;
        private bool 最後のデータ配列か;
        public readonly T Current => Iterator.Chunk[this.Index];
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(++this.Index<this.Count)
                return true;
            if(this.最後のデータ配列か) {
                this.Index=-1;
                return false;
            }
            while(true) {
                if(Iterator.ストレージにデータがあるか) {
                    this.Cursor=Iterator.LastAcceptedAddress;
                    Iterator.SkipLastAcceptedAddressOnce=true;
                    Iterator.ストレージにデータがあるか=false;
                }
                Iterator.Count=0;
                var まだデータがある = Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor: false);
                Session.BasicContext.CompletePending(wait: true);
                this.Count=Iterator.Count;
                if(this.Count>0) {
                    this.Index=0;
                    return true;
                }
                if(!まだデータがある&&!Iterator.ストレージにデータがあるか) {
                    this.最後のデータ配列か=true;
                    this.Index=-1;
                    return false;
                }
            }
            // Iterator.Count=0;
            // var まだデータがある = Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor: false);
            // Session.BasicContext.CompletePending(wait: true);
            // var Count = Iterator.Count;
            // this.Count=Count;
            // if(!まだデータがある&&Count==0) {
            //     this.最後のデータ配列か=true;
            //     this.Index=-1;
            //     return false;
            // }
            // if(Count>0) {
            //     this.Index=0;
            //     return true;
            // }
            // return false;
        }
        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.最後のデータ配列か=false;
            Iterator.Count=0;
            Iterator.LastAcceptedAddress=0;
            Iterator.SkipLastAcceptedAddressOnce=false;
            Iterator.ストレージにデータがあるか=false;
        }
        public readonly void Dispose() {
        }
    }
    private protected ManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
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
}
