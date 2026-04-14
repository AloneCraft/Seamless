using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.Collections;
#pragma warning disable CS8618
public abstract partial class Collection<
    TKey,
    TValue,
#if TSAVORITE1_1_1
    TSessionKey,
    TSessionValue,
#endif
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TContainsDeleteFunctions,
    TStoreFunctions,
    TAllocator
>:IDisposable{
    private long LogMemorySize=>this.CollectionStore.LogMemorySize;
    private double MutableFraction=>this.CollectionStore.MutableFraction;

    private protected OutOfMemoryException ThrowLogFull(){
        var Log=this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength){
        Debug.Assert(this.LogMemorySize>=8192);
        var Log=this.CollectionStore.Store.Log;
        return this.CollectionStore.OverflowMutableStrategyKind switch{
            OverflowMutableStrategyKind.Memory=>this.WouldOverflowMutable<MemoryOverflowMutableStrategy>(Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            OverflowMutableStrategyKind.Storage=>this.WouldOverflowMutable<StorageOverflowMutableStrategy>(Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            _=>throw new UnreachableException(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool WouldOverflowMutable<TOverflowMutableStrategy>(long BeginAddress,long TailAddress,int KeyLength,int ValueLength)
        where TOverflowMutableStrategy:struct,IOverflowMutableStrategy<TOverflowMutableStrategy>{
        return TOverflowMutableStrategy.WouldOverflowMutable(this.LogMemorySize,this.MutableFraction,BeginAddress,TailAddress,KeyLength,ValueLength);
    }
    public int Count=>(int)this.GetLongCount();
    public long EntryCount=>this.CollectionStore.Store.EntryCount;
    private protected abstract Status Read(TKey Key,out TValue Output);
    public abstract bool IsDisposed{get;}

    private bool disposed;

    public void Dispose(){
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing){
        if(this.disposed){
            return;
        }
        if(disposing){
            this.AddClientSession.Dispose();
        }
        this.disposed=true;
    }

    ~Collection()=>this.Dispose(false);
}
