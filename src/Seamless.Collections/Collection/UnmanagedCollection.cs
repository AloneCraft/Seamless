using Garnet.common;

using MemoryPack;

using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Tsavorite.core;

namespace Seamless.Collections;

public abstract class UnmanagedCollection<TKey, TValue> : IDisposable
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    private protected static ReadOnlySpan<byte>Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;

    private protected bool PrivateProtectedWouldOverflowMutable(int KeyLength, int ValueLength) {
        Debug.Assert(this.LogMemorySize >= 8192);
        var MutableBytes = (long)(this.LogMemorySize * this.MutableFraction);
        if(MutableBytes <= 0) {
            MutableBytes = this.LogMemorySize;
        }
        var Log = this.CollectionStore.Store.Log;
        var Used = Log.TailAddress - Log.BeginAddress;
        var Required = (long)KeyLength + ValueLength + RecordOverheadBytes;
        return Used + Required > MutableBytes;
    }

    private protected const int RecordOverheadBytes = 128;
    private protected long LogMemorySize => this.CollectionStore.LogMemorySize;
    private protected double MutableFraction => this.CollectionStore.MutableFraction;

    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }

    private protected abstract bool WouldOverflowMutable(int KeyLength, int ValueLength);

    public long GetLongCount() {
#if TSAVORITE2_0BETA3
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
#elif TSAVORITE1_1_1
        var Iterator = new CounterIterator<TKey, TValue>();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
#endif
    }

    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.CollectionStore.Store.EntryCount;

    private protected readonly UnmanagedCollectionStore<TKey, TValue> CollectionStore;

#if TSAVORITE2_0BETA3
    private protected ReadOnlySpan<byte> SerializeKey(ref TKey key){
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TKey, byte>(ref key),
            Unsafe.SizeOf<TKey>()
        );
    }
    private protected ReadOnlySpan<byte> SerializeValue(ref TValue value){
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TValue, byte>(ref value),
            Unsafe.SizeOf<TValue>()
        );
    }

    public ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Add, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> ClientSession { get; private set; }
    private protected ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Delete, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> DeleteClientSession { get; private set; }

    protected UnmanagedCollection(UnmanagedCollectionStore<TKey, TValue> CollectionStore) {
        this.CollectionStore = CollectionStore;
        var Store = CollectionStore.Store;
        this.ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Add>(new());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Delete>(new());
    }

    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Add>(new());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Delete>(new());
    }

    private protected Status Read(TKey Key, out SpanByteAndMemory Output) {
        Output = default;
        var Session = this.ClientSession;
        var KeyBytes = this.SerializeKey(ref Key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        return Pending(Session, Status, ref Output);
    }

    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;

    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, TSessionFunctions0, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> Session, Status Status)
        where TSessionFunctions0 : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, Empty> {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using(CompletedOutputs) {
                if(CompletedOutputs.Next()) {
                    Status = CompletedOutputs.Current.Status;
                }
            }
        }
        return Status;
    }

    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, TSessionFunctions0, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> Session, Status Status, ref SpanByteAndMemory Output)
        where TSessionFunctions0 : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, Empty> {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using(CompletedOutputs) {
                if(CompletedOutputs.Next()) {
                    Status = CompletedOutputs.Current.Status;
                    Output = CompletedOutputs.Current.Output;
                }
            }
        }
        return Status;
    }

    private protected TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        var KeyBytes = this.SerializeKey(ref Key);
        var ValueBytes = this.SerializeValue(ref Value);
        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        var Session = this.ClientSession;
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory FixedOutput = default;
        var Status = Session.BasicContext.RMW(FixedKey, ref FixedInput, ref FixedOutput);
        Status = Pending(Session, Status);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }

#elif TSAVORITE1_1_1
    // 固定長 BlittableAllocator セッション型
    public ClientSession<TKey, TValue, TValue, TValue, Empty, UnmanagedAdd<TKey, TValue>,
        StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>,
        BlittableAllocator<TKey, TValue, StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>>> ClientSession { get; private set; }

    protected UnmanagedCollection(UnmanagedCollectionStore<TKey, TValue> CollectionStore) {
        this.CollectionStore = CollectionStore;
        this.ClientSession = CollectionStore.Store.NewSession<TValue, TValue, Empty, UnmanagedAdd<TKey, TValue>>(new());
    }

    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession = Store.NewSession<TValue, TValue, Empty, UnmanagedAdd<TKey, TValue>>(new());
    }

    private protected Status Read(TKey Key, out TValue Output) {
        Output = default;
        var Status = this.ClientSession.BasicContext.Read(ref Key, ref Output, default(Empty));
        return PendingValue(this.ClientSession, Status, ref Output);
    }

    /// <summary>TValue 出力を持つ任意セッションのペンディング完了を待機する。</summary>
    private protected static Status PendingValue<TFunctions>(
        ClientSession<TKey, TValue, TValue, TValue, Empty, TFunctions,
            StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>,
            BlittableAllocator<TKey, TValue, StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>>> session,
        Status status, ref TValue output)
        where TFunctions : ISessionFunctions<TKey, TValue, TValue, TValue, Empty>
    {
        if (!status.IsPending) return status;
        session.BasicContext.CompletePendingWithOutputs(out var completedOutputs, wait: true);
        if (completedOutputs.Next()) {
            status = completedOutputs.Current.Status;
            output = completedOutputs.Current.Output;
        }
        completedOutputs.Dispose();
        return status;
    }

    /// <summary>bool 出力を持つ任意セッションのペンディング完了を待機する。</summary>
    private protected static Status PendingBool<TFunctions>(
        ClientSession<TKey, TValue, TValue, bool, Empty, TFunctions,
            StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>,
            BlittableAllocator<TKey, TValue, StoreFunctions<TKey, TValue, UnmanagedKeyComparer<TKey>, DefaultRecordDisposer<TKey, TValue>>>> session,
        Status status, ref bool output)
        where TFunctions : ISessionFunctions<TKey, TValue, TValue, bool, Empty>
    {
        if (!status.IsPending) return status;
        session.BasicContext.CompletePendingWithOutputs(out var completedOutputs, wait: true);
        if (completedOutputs.Next()) {
            status = completedOutputs.Current.Status;
            output = completedOutputs.Current.Output;
        }
        completedOutputs.Dispose();
        return status;
    }

    private protected TryAddResult PrivateTryAdd(TKey Key, TValue Value) {
        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(), Unsafe.SizeOf<TValue>())) {
            return TryAddResult.LogFull;
        }
        var Session = this.ClientSession;
        TValue Output = default;
        var Status = Session.BasicContext.RMW(ref Key, ref Value, ref Output, default(Empty));
        Status = PendingValue(Session, Status, ref Output);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }
#endif

    public bool Remove(TKey key) {
#if TSAVORITE2_0BETA3
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = this.SerializeKey(ref key);
        var Output = default(SpanByteAndMemory);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Input = default(PinnedSpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input, ref Output);
        Status = Pending(DeleteClientSession, Status);
        Debug.Assert(Status.Found != Status.NotFound);
        return Status.Found;
#elif TSAVORITE1_1_1
        var Session = this.ClientSession;
        var Status = Session.BasicContext.Delete(ref key, default(Empty));
        if (Status.IsPending) {
            TValue dummy = default;
            Status = PendingValue(Session, Status, ref dummy);
        }
        Debug.Assert(Status.Found != Status.NotFound);
        return Status.Found;
#endif
    }

    public bool Contains(TKey Key) {
#if TSAVORITE2_0BETA3
        var Status = this.Read(Key, out _);
#elif TSAVORITE1_1_1
        var Status = this.Read(Key, out _);
#endif
        Debug.Assert(Status.Found == !Status.NotFound);
        return Status.Found;
    }

    public abstract bool IsDisposed { get; }

    private bool disposed;

    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if(this.disposed) {
            return;
        }
        if(disposing) {
            this.ClientSession.Dispose();
        }
        this.disposed = true;
    }

    ~UnmanagedCollection() => this.Dispose(false);
}
