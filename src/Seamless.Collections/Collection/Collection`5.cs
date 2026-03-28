using Garnet.common;
namespace Seamless.Collections;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
//public abstract class Collectionx<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator>:IDisposable
public abstract class Collectionx<TKey,TValue>:IDisposable
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    //where TSessionFunctions:SSessionFunctions//ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    {
    //AddSessionFunctions;
    //StoreFunctions<SpanByteComparer,DefaultRecordDisposer>
    //ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>
    private protected const int RecordOverheadBytes=128;
    internal readonly TsavoriteKV<TStoreFunctions,TAllocator> Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    private protected readonly TSessionFunctions SessionFunctions;
    public TClientSession Session;
    private protected readonly TDeleteClientSession DeleteSession;
    private protected Collectionx(TsavoriteKV<TStoreFunctions,TAllocator> Store,TSessionFunctions SessionFunctions,long LogMemorySize=0,double MutableFraction=1.0){
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.SessionFunctions=SessionFunctions;
        this.Session=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(SessionFunctions);
        this.DeleteSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions>(new ());
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
        } else{
        }
        //アンマネージドリソースの解放（必要なければ省略可）
        this.disposed=true;
    }
    ~Collectionx()=>this.Dispose(false);
}
