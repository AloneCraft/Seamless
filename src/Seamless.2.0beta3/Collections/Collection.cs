using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
public abstract partial class Collection<
    TKey,
    TValue,
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TDeleteFunctions,
    TStoreFunctions,
    TAllocator
>
    where TKey : IEquatable<TKey>
    //where TReadFunctions  : struct, ISessionFunctions<TSessionKey,TSessionValue,EqualityEmpty, TValue, EqualityEmpty>
    //where TAddFunctions   : struct, ISessionFunctions<TSessionKey,TSessionValue,TValue, bool, EqualityEmpty>
    //where TDeleteFunctions: struct, ISessionFunctions<TSessionKey,TSessionValue, EqualityEmpty, bool, EqualityEmpty>
    where TReadFunctions   : struct,ISessionFunctions<EqualityEmpty,TReadOutput, EqualityEmpty>,ICreate<TReadFunctions>
    where TAddFunctions   : struct,ISessionFunctions<TAddInput,bool, EqualityEmpty>,ICreate<TAddFunctions>
    where TDeleteFunctions: struct,ISessionFunctions<EqualityEmpty, bool, EqualityEmpty>,ICreate<TDeleteFunctions>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions>
    {
    public abstract long GetLongCount();

    //private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;
    //private protected ClientSession<FixedSpanByteKey, PinnedSpanByte, TReadOutput, EqualityEmpty, TReadFunctions, TStoreFunctions, TAllocator> ReadClientSession { get; private set; }
    //private protected ClientSession<FixedSpanByteKey, TAddInput, bool, EqualityEmpty, TAddFunctions, TStoreFunctions, TAllocator> AddClientSession { get; private set; }
    //private protected ClientSession<FixedSpanByteKey, EqualityEmpty, bool, EqualityEmpty, TDeleteFunctions, TStoreFunctions, TAllocator> DeleteClientSession { get; private set; }
    //private protected readonly CollectionStore<TKey, TValue, TStoreFunctions, TAllocator> CollectionStore;
    private protected ClientSession<FixedSpanByteKey,EqualityEmpty,TReadOutput,EqualityEmpty,TReadFunctions,TStoreFunctions,TAllocator> ReadClientSession { get; private set; }
    private protected ClientSession<FixedSpanByteKey,TAddInput,bool,EqualityEmpty,TAddFunctions,TStoreFunctions,TAllocator> AddClientSession { get; private set; }
    private protected ClientSession<FixedSpanByteKey,EqualityEmpty,bool,EqualityEmpty,TDeleteFunctions,TStoreFunctions,TAllocator> DeleteClientSession { get; private set; }
    private readonly CollectionStore<TKey, TValue,TStoreFunctions, TAllocator> CollectionStore;
    protected Collection(CollectionStore<TKey, TValue, TStoreFunctions, TAllocator> CollectionStore) {
        this.CollectionStore = CollectionStore;
        var Store = CollectionStore.Store;
        this.ReadClientSession = Store.NewSession<FixedSpanByteKey, EqualityEmpty, TReadOutput, EqualityEmpty, TReadFunctions>(TReadFunctions.Create());
        this.AddClientSession = Store.NewSession<FixedSpanByteKey, TAddInput, bool, EqualityEmpty, TAddFunctions>(TAddFunctions.Create());
        this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, EqualityEmpty, bool, EqualityEmpty, TDeleteFunctions>(TDeleteFunctions.Create());
    }

    public virtual void Clear() {
        var Session = this.AddClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
		this.ReadClientSession = Store.NewSession<FixedSpanByteKey, EqualityEmpty, TReadOutput, EqualityEmpty, TReadFunctions>(TReadFunctions.Create());
		this.AddClientSession = Store.NewSession<FixedSpanByteKey, TAddInput, bool, EqualityEmpty, TAddFunctions>(TAddFunctions.Create());
		this.DeleteClientSession = Store.NewSession<FixedSpanByteKey, EqualityEmpty, bool, EqualityEmpty, TDeleteFunctions>(TDeleteFunctions.Create());
	}

	//private protected abstract Status Read(TKey Key, out SpanByteAndMemory Output);
    private protected static Status Pending<TInput0, TOutput0, TSessionFunctions0>(ClientSession<FixedSpanByteKey, TInput0, TOutput0,EqualityEmpty, TSessionFunctions0, TStoreFunctions, TAllocator> Session,Status Status)
        where TSessionFunctions0 : ISessionFunctions<TInput0, TOutput0, EqualityEmpty> {

	//private protected static Status Pending
	//	where TSessionFunctions0 : struct, ISessionFunctions<TInput0, TOutput0, TContext0> {
		if(Status.IsPending) {
			Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs, wait: true);
			using(CompletedOutputs) {
				Debug.Assert(CompletedOutputs.Next());
				Status = CompletedOutputs.Current.Status;
			}
		}
		return Status;
	}

	private protected static Status Pending<TInput0, TOutput0, TSessionFunctions0>(ClientSession<FixedSpanByteKey, TInput0, TOutput0, EqualityEmpty, TSessionFunctions0, TStoreFunctions, TAllocator> Session, Status Status, ref TOutput0 Output)
		where TSessionFunctions0 : ISessionFunctions<TInput0, TOutput0, EqualityEmpty> {
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
    //private protected abstract TValue ConvertOutput(ref TValue output);

    public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out Value);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        return true;
    }
    private protected abstract TryAddResult PrivateTryAdd(TKey Key, TValue Value);
}
