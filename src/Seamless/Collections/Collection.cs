using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.Collections;

[DebuggerDisplay("Count={Count}")]
public abstract partial class Collection : IDisposable
{
    private bool disposed;

    public int Count => (int)this.GetLongCountCore();
    public long EntryCount => this.GetEntryCountCore();
    public abstract bool IsDisposed { get; }

    private protected OutOfMemoryException ThrowLogFull()
    {
        var (BeginAddress, HeadAddress, TailAddress) = this.GetLogAddresses();
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {BeginAddress}, HeadAddress: {HeadAddress}, TailAddress: {TailAddress}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool WouldOverflowMutable(int KeyLength, int ValueLength)
    {
        Debug.Assert(this.GetLogMemorySize() >= 8192);
        var (BeginAddress, _, TailAddress) = this.GetLogAddresses();
        return this.GetOverflowMutableStrategyKind() switch
        {
            OverflowMutableStrategyKind.Memory => this.WouldOverflowMutable<MemoryOverflowMutableStrategy>(BeginAddress, TailAddress, KeyLength, ValueLength),
            OverflowMutableStrategyKind.Storage => this.WouldOverflowMutable<StorageOverflowMutableStrategy>(BeginAddress, TailAddress, KeyLength, ValueLength),
            _ => throw new UnreachableException(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool WouldOverflowMutable<TOverflowMutableStrategy>(long BeginAddress, long TailAddress, int KeyLength, int ValueLength)
        where TOverflowMutableStrategy : struct, IOverflowMutableStrategy<TOverflowMutableStrategy>
    {
        return TOverflowMutableStrategy.WouldOverflowMutable(this.GetLogMemorySize(), this.GetMutableFraction(), BeginAddress, TailAddress, KeyLength, ValueLength);
    }

    protected abstract long GetLongCountCore();
    protected abstract long GetEntryCountCore();
    protected abstract long GetLogMemorySize();
    protected abstract double GetMutableFraction();
    private protected abstract OverflowMutableStrategyKind GetOverflowMutableStrategyKind();
    protected abstract (long BeginAddress, long HeadAddress, long TailAddress) GetLogAddresses();
    protected abstract void DisposeSessions();

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }
        if (disposing)
        {
            this.DisposeSessions();
        }
        this.disposed = true;
    }

    ~Collection() => this.Dispose(false);
}

#if TSAVORITE1_1_1
public abstract partial class Collection<
    TKey,
    TValue,
    TSessionKey,
    TSessionValue,
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TContainsDeleteFunctions,
    TStoreFunctions,
    TAllocator
>
    where TKey : IEquatable<TKey>
    where TReadFunctions : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty
    >,
    ICreate<TReadFunctions>
    where TAddFunctions : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        TAddInput,
        bool,
        EqualityEmpty
    >,
    ICreate<TAddFunctions>
    where TContainsDeleteFunctions : struct, ISessionFunctions<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        bool,
        EqualityEmpty
    >,
    ICreate<TContainsDeleteFunctions>
    where TStoreFunctions : IStoreFunctions<
        TSessionKey,
        TSessionValue
    >
    where TAllocator : IAllocator<
        TSessionKey,
        TSessionValue,
        TStoreFunctions
    > {
    protected override long GetLongCountCore() => this.GetLongCount();
    protected override long GetEntryCountCore() => this.CollectionStore.Store.EntryCount;
    protected override long GetLogMemorySize() => this.CollectionStore.LogMemorySize;
    protected override double GetMutableFraction() => this.CollectionStore.MutableFraction;
    private protected override OverflowMutableStrategyKind GetOverflowMutableStrategyKind() => this.CollectionStore.OverflowMutableStrategyKind;
    protected override (long BeginAddress,long HeadAddress,long TailAddress) GetLogAddresses() {
        var Log=this.CollectionStore.Store.Log;
        return (Log.BeginAddress,Log.HeadAddress,Log.TailAddress);
    }
    protected override void DisposeSessions() {
        this.AddClientSession.Dispose();
    }
}
#elif TSAVORITE2_0BETA3
public abstract partial class Collection<
    TKey,
    TValue,
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TContainsDeleteFunctions,
    TStoreFunctions,
    TAllocator
>
    where TKey : IEquatable<TKey>
    where TReadFunctions : struct, ISessionFunctions<
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty
    >,
    ICreate<TReadFunctions>
    where TAddFunctions : struct, ISessionFunctions<
        TAddInput,
        bool,
        EqualityEmpty
    >,
    ICreate<TAddFunctions>
    where TContainsDeleteFunctions : struct, ISessionFunctions<
        EqualityEmpty,
        bool,
        EqualityEmpty
    >,
    ICreate<TContainsDeleteFunctions>
    where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<
        TStoreFunctions
    > {
    protected override long GetLongCountCore() => this.GetLongCount();
    protected override long GetEntryCountCore() => this.CollectionStore.Store.EntryCount;
    protected override long GetLogMemorySize() => this.CollectionStore.LogMemorySize;
    protected override double GetMutableFraction() => this.CollectionStore.MutableFraction;
    private protected override OverflowMutableStrategyKind GetOverflowMutableStrategyKind() => this.CollectionStore.OverflowMutableStrategyKind;
    protected override (long BeginAddress,long HeadAddress,long TailAddress) GetLogAddresses() {
        var Log=this.CollectionStore.Store.Log;
        return (Log.BeginAddress,Log.HeadAddress,Log.TailAddress);
    }
    protected override void DisposeSessions() {
        this.AddClientSession.Dispose();
    }
}
#endif
