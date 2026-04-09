using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class Collection<
    TKey,
    TValue,
    TAddFunctions,
    TDeleteFunctions,
    TStoreFunctions,
    TAllocator




>
    where TKey : IEquatable<TKey>
    where TAddFunctions : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty>, new()
    where TDeleteFunctions : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty>, new()
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
    {
    public abstract long GetLongCount();

    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;

    private protected ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TAddFunctions, TStoreFunctions, TAllocator> ClientSession { get; private set; }
    private protected ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TDeleteFunctions, TStoreFunctions, TAllocator> DeleteClientSession { get; private set; }

    protected Collection(CollectionStore<TKey, TValue, TStoreFunctions, TAllocator> CollectionStore) {
        this.CollectionStore = CollectionStore;
        var Store = CollectionStore.Store;
        this.ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TAddFunctions>(new());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TDeleteFunctions>(new());
    }

    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TAddFunctions>(new());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TDeleteFunctions>(new());
    }

    private protected abstract Status Read(TKey Key, out SpanByteAndMemory Output);

    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TSessionFunctions0, TStoreFunctions, TAllocator> Session,Status Status)
        where TSessionFunctions0 : struct, ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty> {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using(CompletedOutputs) {
	            Debug.Assert(CompletedOutputs.Next());
	            Status = CompletedOutputs.Current.Status;
	        }
        }
        return Status;
    }

    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, core_Empty, TSessionFunctions0, TStoreFunctions, TAllocator> Session, Status Status, ref SpanByteAndMemory Output)
        where TSessionFunctions0 : ISessionFunctions<PinnedSpanByte, SpanByteAndMemory, core_Empty> {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
            using(CompletedOutputs) {
	            Debug.Assert(CompletedOutputs.Next());
	            Status = CompletedOutputs.Current.Status;
                Output = CompletedOutputs.Current.Output;
	        }
        }
        return Status;
    }
    private protected abstract TValue ConvertOutput(ref SpanByteAndMemory output);

    public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out var Output);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        Value=this.ConvertOutput(ref Output);
        return true;
    }
    private protected abstract TryAddResult PrivateTryAdd(TKey Key, TValue Value);
}
