using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,
    TValue,


    TValue,//TReadOutput
    TValue,//TAddInput
    UnmanagedRead<
        TKey,
        TValue
    >,
    UnmanagedAdd<
        TKey,
        TValue
    >,
    ContainsDelete,



    UnmanagedStoreFunctions,










    UnmanagedAllocator














>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed partial class KeyIterator:ScanIteratorFunctions_List<TKey,UnmanagedList<TKey>> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    public sealed class MemoryKeyIterator:MemoryScanIteratorFunctions<TKey,UnmanagedList<TKey>> {
        private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TKeySerializer.Deserialize(logRecord.Key);
    }
    internal sealed partial class ValueIterator:ScanIteratorFunctions_List<TValue,UnmanagedList<TValue>> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    internal sealed class MemoryValueIterator:MemoryScanIteratorFunctions<TValue,UnmanagedList<TValue>> {
        private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            TValueSerializer.Deserialize(logRecord.ValueSpan);
    }
    public sealed partial class KeyValueIterator:ScanIteratorFunctions_List<KeyValuePair<TKey,TValue>,UnmanagedList<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            new(
                TKeySerializer.Deserialize(logRecord.Key),
                TValueSerializer.Deserialize(logRecord.ValueSpan)
            );
    }
    public sealed class MemoryKeyValueIterator:MemoryScanIteratorFunctions<KeyValuePair<TKey,TValue>,UnmanagedList<KeyValuePair<TKey,TValue>>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            new(
                TKeySerializer.Deserialize(logRecord.Key),
                TValueSerializer.Deserialize(logRecord.ValueSpan)
            );
    }
    //public struct Enumerator<TIterator, T>:IEnumerator<T>
    //    where TIterator : ScanIteratorFunctions_List<T> {
    //    private global::Seamless.Collections.ManagedList<T>? Items;
    //    private string? WorkListPath;
    //    private long Index = -1;
    //    private T CurrentValue = default!;
    //    private readonly ClientSession<
    //        FixedSpanByteKey,//TKey
    //        EqualityEmpty,//TInput
    //        TValue,//TOutput
    //        EqualityEmpty,//TContext
    //        UnmanagedRead<
    //            TKey,
    //            TValue
    //        >,//TReadFunctions
    //        UnmanagedStoreFunctions,//TStoreFunctions
    //        UnmanagedAllocator//TAllocator
    //    > _session;
    //    private readonly TIterator _iterator;
    //    public Enumerator(
    //        ClientSession<
    //            FixedSpanByteKey,//TKey
    //            EqualityEmpty,//TInput
    //            TValue,//TOutput
    //            EqualityEmpty,//TContext
    //            UnmanagedRead<
    //                TKey,
    //                TValue
    //            >,//TReadFunctions
    //            UnmanagedStoreFunctions,//TStoreFunctions
    //            UnmanagedAllocator//TAllocator
    //        > session,
    //        TIterator iterator
    //    ) {
    //        this._session=session;
    //        this._iterator=iterator;
    //        this.Items=null;
    //        this.WorkListPath=null;
    //        this.Index=-1;
    //        this.CurrentValue=default!;
    //        this.WorkListPath=CreateTemporaryWorkListPath("unmanaged-enumerator");
    //        try {
    //            var items = new global::Seamless.Collections.ManagedList<T>(this.WorkListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
    //            this._iterator.SetList(items);
    //            var scanFunctions = this._iterator;
    //            var cursor = 0L;
    //            this._session.IterateLookup(ref scanFunctions,ref cursor,resetCursor: false);
    //            this._session.BasicContext.CompletePending(wait: true);
    //            this.Items=items;
    //        }
    //        catch {
    //            this.DisposeItems();
    //            throw;
    //        }
    //    }
    //    public readonly T Current => this.CurrentValue;
    //    readonly object? IEnumerator.Current => this.Current;
    //    public bool MoveNext() {
    //        if(this.Items is null)
    //            return false;
    //        var nextIndex = this.Index+1;
    //        if(nextIndex>=this.Items.Count)
    //            return false;
    //        this.Index=nextIndex;
    //        this.CurrentValue=this.Items[this.Index];
    //        return true;
    //    }
    //    private void DisposeItems() {
    //        this.Items?.Dispose();
    //        this.Items=null;
    //        if(this.WorkListPath is null)
    //            return;
    //        DeleteTemporaryWorkListPath(this.WorkListPath);
    //        this.WorkListPath=null;
    //    }
    //    public void Reset() {
    //        this.Index=-1;
    //        this.CurrentValue=default!;
    //    }
    //    public void Dispose() {
    //        this.DisposeItems();
    //    }
    //}
    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool ContainsKey(TKey Key) {
        return this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    }
    public bool Contains(TKey Key) => this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    private protected override unsafe Status Read(TKey Key,out TValue Output) {
        var ClientSession = this.ReadSession;
        var KeyBytes = TKeySerializer.Serialize(ref Key);
        Output=default;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            Status=ClientSession.BasicContext.Read((FixedSpanByteKey)KeyBytes,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
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
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
    private protected override unsafe TryAddResult PrivateTryAdd(TKey key,TValue value) {
        var KeyBytes = TKeySerializer.Serialize(ref key);
        if(this.WouldOverflowMutable(KeyBytes.Length,Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ClientSession = this.Add_ClientSession;
        var Output = false;
        Status Status;
        fixed(byte* _ = KeyBytes) {
            Status=ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref value,ref Output);
            Status=Pending(ClientSession,Status,ref Output);
        }
        Debug.Assert(Status.IsCompleted);
        Debug.Assert(Status.NotFound==Output);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
