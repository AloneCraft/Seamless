using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
public abstract partial class Collection<TKey,TValue,TSessionKey,TSessionValue,TInput,TOutput,TAddFunctions,TDeleteFunctions,TStoreFunctions,TAllocator>
    where TKey : IEquatable<TKey>
    where TAddFunctions : ISessionFunctions<TInput, TOutput, core_Empty>
    where TDeleteFunctions : ISessionFunctions<TInput, TOutput, core_Empty>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
    {
    public long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator, ref Cursor, resetCursor: false);
        return Iterator.Counter;
    }

    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;

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

    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TSessionFunctions0, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> Session, Status Status)
        where TSessionFunctions0 : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty> {
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

    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TSessionFunctions0, StoreFunctions<SpanByteComparer, DefaultRecordDisposer>, SpanByteAllocator<StoreFunctions<SpanByteComparer, DefaultRecordDisposer>>> Session, Status Status, ref SpanByteAndMemory Output)
        where TSessionFunctions0 : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty> {
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

    private protected TValue ConvertOutput(ref SpanByteAndMemory output) =>
        Deserialize<TValue>(GetReadOnlySpan(output));
}
