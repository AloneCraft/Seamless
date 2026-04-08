using MemoryPack;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
namespace Seamless.Collections;
using core_Empty=Tsavorite.core.Empty;
#pragma warning disable CS8618
public abstract class Collection<TKey,TValue,TSessionKey,TSessionValue,TInput,TOutput,TAddFunctions,TDeleteFunctions,TStoreFunctions,TAllocator> : IDisposable
#if TSAVORITE2_0BETA3
    where TKey : IEquatable<TKey>
    where TAddFunctions : ISessionFunctions<TInput, TOutput, Tsavorite.core.Empty>
    where TDeleteFunctions : ISessionFunctions<TInput, TOutput, Tsavorite.core.Empty>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
#elif TSAVORITE1_1_1
    where TKey : IEquatable<TKey>
    where TAddFunctions   : struct, ISessionFunctions<TSessionKey,TSessionValue,TInput, TOutput, core_Empty>
    where TDeleteFunctions: struct, ISessionFunctions<TSessionKey,TSessionValue, TInput, TOutput, core_Empty>
    where TStoreFunctions : IStoreFunctions<TSessionKey,TSessionValue>
    where TAllocator : IAllocator<TSessionKey,TSessionValue, TStoreFunctions>
#endif

    {
    private protected static ReadOnlySpan<byte>Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;
    private protected abstract ReadOnlySpan<byte>SerializeKey(ref TKey key);
    private protected abstract ReadOnlySpan<byte>SerializeValue(ref TValue value);

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

#if TSAVORITE2_0BETA3
    public long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }
#elif TSAVORITE1_1_1
    public abstract long GetLongCount();
#endif

    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.CollectionStore.Store.EntryCount;

    private protected readonly CollectionStore<TKey, TValue,TSessionKey,TSessionValue,TStoreFunctions,TAllocator> CollectionStore;

#if TSAVORITE2_0BETA3
    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;
#elif TSAVORITE1_1_1
    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.AsReadOnlySpan();
#endif
#if TSAVORITE2_0BETA3
    public ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, Add, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> ClientSession { get; private set; }
    private protected ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, Delete, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> DeleteClientSession { get; private set; }

    protected Collection(CollectionStore<TKey, TValue, TSessionKey, TSessionValue, TStoreFunctions, TAllocator> CollectionStore) {
        this.CollectionStore = CollectionStore;
        var Store = CollectionStore.Store;
        this.ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, Add>(new());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, Delete>(new());
    }

    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, Add>(new());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, Delete>(new());
    }

    private protected Status Read(TKey Key, out SpanByteAndMemory Output) {
        Output = default;
        var Session = this.ClientSession;
        var KeyBytes = this.SerializeKey(ref Key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Status = Session.BasicContext.Read(FixedKey, ref Output);
        return Pending(Session, Status, ref Output);
    }


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
    public            ClientSession<TSessionKey,TSessionValue,TInput, TOutput, core_Empty,TAddFunctions,TStoreFunctions,TAllocator> ClientSession { get; private set; }
    private protected ClientSession<TSessionKey,TSessionValue, TInput, TOutput, core_Empty,TDeleteFunctions,TStoreFunctions,TAllocator>
    DeleteClientSession { get; private set; }

    protected Collection(
        CollectionStore<TKey,TValue,TSessionKey,TSessionValue,TStoreFunctions,TAllocator> CollectionStore) {
        this.CollectionStore = CollectionStore;
        var Store = CollectionStore.Store;
        this.ClientSession = Store.NewSession<TInput, TOutput, core_Empty, TAddFunctions>(default);
        this.DeleteClientSession = Store.NewSession<TInput, TOutput, core_Empty, TDeleteFunctions>(default);
    }

    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession = Store.NewSession<TInput, TOutput, core_Empty, TAddFunctions>(default);
        this.DeleteClientSession = Store.NewSession<TInput, TOutput, core_Empty, TDeleteFunctions>(default);
    }

    private protected abstract Status Read(TKey Key,out TOutput Output);
    //{
    //    var KeyBytes = this.SerializeKey(ref Key);
    //    var Session = this.ClientSession;
    //    var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
    //    TOutput value0 = default!;
    //    var Status = Session.BasicContext.Read(ref FixedKey, ref value0);
    //    var Result = Pending(Session, Status, ref value0);
    //    Output = Unsafe.As<TOutput, SpanByteAndMemory>(ref value0);
    //    return Result;
    //}

    private protected static Status Pending<TSessionFunctions0>(
        ClientSession<TSessionKey, TSessionValue, TInput, TOutput, core_Empty, TSessionFunctions0, TStoreFunctions, TAllocator> Session,
        Status Status)
        where TSessionFunctions0 : struct, ISessionFunctions<TSessionKey, TSessionValue, TInput, TOutput, core_Empty> {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            CompletedOutputs.Next();
            Status = CompletedOutputs.Current.Status;
            CompletedOutputs.Dispose();
        }
        return Status;
    }

    private protected static Status Pending<TSessionFunctions0>(
        ClientSession<TSessionKey, TSessionValue, TInput, TOutput, core_Empty, TSessionFunctions0, TStoreFunctions, TAllocator> Session,
        Status Status,
        ref TOutput Output)
        where TSessionFunctions0 : struct, ISessionFunctions<TSessionKey, TSessionValue, TInput, TOutput, core_Empty> {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            CompletedOutputs.Next();
            Status = CompletedOutputs.Current.Status;
            Output = CompletedOutputs.Current.Output;
            CompletedOutputs.Dispose();
        }
        return Status;
    }
    private protected abstract TValue ConvertOutput(ref TOutput output);

    public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out var Output);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=ConvertOutput(ref Output);
        return true;
    }
    private protected abstract TryAddResult PrivateTryAdd(TKey Key, TValue Value) ;
//{
//        var KeyBytes = this.SerializeKey(ref Key);
//        var ValueBytes = this.SerializeValue(ref Value);
//        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
//            return TryAddResult.LogFull;
//        }
//        var Session = this.ClientSession;
//        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
//        var FixedInput = SpanByte.FromPinnedSpan(ValueBytes);
//        TInput Input = Unsafe.As<SpanByte, TInput>(ref FixedInput);
//        TOutput Output = default!;
//        var Status = Session.BasicContext.RMW(ref FixedKey, ref Input, ref Output, default(TContext)!);
//        Status = Pending(this.ClientSession, Status);
//        if(Status.NotFound) {
//            return TryAddResult.Success;
//        }
//        return TryAddResult.KeyAlreadyExists;
//    }
#endif

    public abstract bool Remove(TKey key);
//    {
//        var DeleteClientSession = this.DeleteClientSession;
//        var KeyBytes = this.SerializeKey(ref key);
//        var Output = default(SpanByteAndMemory);
//#if TSAVORITE2_0BETA3
//        var FixedKey = (FixedSpanByteKey)KeyBytes;
//        var Input = default(PinnedSpanByte);
//        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input, ref Output);
//#elif TSAVORITE1_1_1
//        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
//        TInput Input = default!;
//        core_Empty Output0 =default;
//        var Status = DeleteClientSession.BasicContext.RMW(ref FixedKey, ref Input, ref Output0,default);
//#endif
//        Status = Pending(DeleteClientSession, Status);
//        Debug.Assert(Status.Found != Status.NotFound);
//        return Status.Found;
//    }

    public bool Contains(TKey Key) {
        var Status = this.Read(Key, out _);
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

    ~Collection() => this.Dispose(false);
}
