using Seamless.SessionFunctions;
using Seamless.ScanIteratorFunctions;
using System.Diagnostics.CodeAnalysis;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class Collection<TKey,TValue,TSessionKey,TSessionValue,TInput,TOutput,TAddFunctions,TDeleteFunctions,TStoreFunctions,TAllocator>
    where TKey : IEquatable<TKey>
    where TAddFunctions   : struct, ISessionFunctions<TSessionKey,TSessionValue,TInput, TOutput, core_Empty>
    where TDeleteFunctions: struct, ISessionFunctions<TSessionKey,TSessionValue, TInput, TOutput, core_Empty>
    where TStoreFunctions : IStoreFunctions<TSessionKey,TSessionValue>
    where TAllocator : IAllocator<TSessionKey,TSessionValue, TStoreFunctions>
    {
    public abstract long GetLongCount();

    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.AsReadOnlySpan();

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
}
