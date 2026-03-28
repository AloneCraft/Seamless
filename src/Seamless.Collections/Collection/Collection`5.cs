using Garnet.common;
namespace Seamless.Collections;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
public abstract class Collection<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>:IDisposable
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TSessionFunctions:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    where TStoreFunctions:IStoreFunctions
    where TAllocator:IAllocator<TStoreFunctions>{
    private protected const int RecordOverheadBytes=128;
    internal readonly TsavoriteKV<TStoreFunctions,TAllocator> Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    private protected readonly TSessionFunctions SessionFunctions;
    public ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session;
    private protected readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions,TStoreFunctions,TAllocator> DeleteSession;
    private protected Collection(TsavoriteKV<TStoreFunctions,TAllocator> Store,TSessionFunctions SessionFunctions,long LogMemorySize=0,double MutableFraction=1.0){
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.SessionFunctions=SessionFunctions;
        this.Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(SessionFunctions);
        this.DeleteSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions>(new DeleteSessionFunctions());
    }
    public void Clear(){
        var Session=this.Session;
        Session.BasicContext.CompletePending(wait:true);
        Session.Dispose();
        this.Store.Reset();
        this.Session=this.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(this.SessionFunctions);
    }









    private protected KeyEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetKeyEnumerable()=>new(this.Session);
    private protected ValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetValueEnumerable()=>new(this.Session);
    private protected KeyValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetKeyValueEnumerable()=>new(this.Session);
    public abstract bool IsDisposed{get;}
    private bool disposed;
    public void Dispose(){
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing){
        if(this.disposed) return;
        if(disposing){
            //マネージドリソースの解放
            this.Session.Dispose();
        }
        //アンマネージドリソースの解放（必要なければ省略可）
        this.disposed=true;
    }
    [ExcludeFromCodeCoverage]~Collection()=>this.Dispose(false);
}
