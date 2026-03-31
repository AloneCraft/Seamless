using System.Diagnostics;
using Garnet.common;
using MemoryPack;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Tsavorite.core;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うコレクションの抽象クラス
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
//public abstract class Collection<TKey,TValue>:Collectionx<TKey,TValue,AddSessionFunctions,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjecSAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
public abstract class Collection<TKey,TValue>:IDisposable where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    private protected const int RecordOverheadBytes=128;
    internal readonly TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    private protected readonly Add SessionFunctions=new();
    public ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add                ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> ClientSession;
    private protected readonly ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Delete             ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>DeleteClientSession;
    protected Collection(TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store,long LogMemorySize=0,double MutableFraction=1.0){
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(this.SessionFunctions);
        this.DeleteClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Delete>(new ());
    }
    public void Clear(){
        var Session=this.ClientSession;
        Session.BasicContext.CompletePending(wait:true);
        Session.Dispose();
        this.Store.Reset();
        this.ClientSession=this.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(this.SessionFunctions);
    }









    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength){
        if(this.LogMemorySize<=0) {
            return false;
        }
        var MutableBytes = (long)(this.LogMemorySize*this.MutableFraction);
        if(MutableBytes<=0) {
            MutableBytes=this.LogMemorySize;
        }
        var Log = this.Store.Log;
        var Used = Log.TailAddress-Log.BeginAddress;
        var Required = (long)KeyLength+ValueLength+RecordOverheadBytes;
        return Used+Required>MutableBytes;
    }
    private protected static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;
    public long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        return Iterator.Counter;
    }
    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.Store.EntryCount;
    private protected Status Read(TKey Key,ref SpanByteAndMemory Output) {
        var Session=this.ClientSession;
        var KeyBytes = Serialize(Key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Status = Session.BasicContext.Read(FixedKey,ref Output);
        return Pending(Session,Status,ref Output);
    }

    
    
    
    
    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>Session,Status Status)
        where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            CompletedOutputs.Next();
            Status=CompletedOutputs.Current.Status;
            CompletedOutputs.Dispose();
        }
        return Status;
    }
    //private protected static Status Pending<TSessionFunctions0,GStoreFunctions0,SAllocator0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,GStoreFunctions0,SAllocator0>Session,Status Status)
    //    where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    //    where GStoreFunctions0  :Tsavorite.core.IStoreFunctions
    //    where SAllocator0 : IAllocator<GStoreFunctions0>{
    //    if(Status.IsPending) {
    //        Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        CompletedOutputs.Next();
    //        Status=CompletedOutputs.Current.Status;
    //        CompletedOutputs.Dispose();
    //    } else {
    //    }
    //    return Status;
    //}
    //private protected static Status Pending(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,GStoreFunctions,SAllocator>Session,Status Status,ref SpanByteAndMemory Output){
    //    if(Status.IsPending) {
    //        Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        CompletedOutputs.Next();
    //        Status=CompletedOutputs.Current.Status;
    //        Output=CompletedOutputs.Current.Output;
    //        CompletedOutputs.Dispose();
    //    } else {
    //    }
    //    return Status;
    //}
    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>Session,Status Status,ref SpanByteAndMemory Output)
        where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            CompletedOutputs.Next();
            Status=CompletedOutputs.Current.Status;
            Output=CompletedOutputs.Current.Output;
            CompletedOutputs.Dispose();
        }
        return Status;
    }
    //private protected static Status Pending<TSessionFunctions0,GStoreFunctions0,SAllocator0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,GStoreFunctions0,SAllocator0>Session,Status Status,ref SpanByteAndMemory Output)
    //    where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    //    where GStoreFunctions0  :Tsavorite.core.IStoreFunctions
    //    where SAllocator0 : IAllocator<GStoreFunctions0>{
    //    if(Status.IsPending) {
    //        Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        CompletedOutputs.Next();
    //        Status=CompletedOutputs.Current.Status;
    //        Output=CompletedOutputs.Current.Output;
    //        CompletedOutputs.Dispose();
    //    } else {
    //    }
    //    return Status;
    //}
    private protected TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes = Serialize(Key);
        var ValueBytes = Serialize(Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }


        var Session=this.ClientSession;
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var FixedInput = PinnedSpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory FixedOutput = default;
        var Status = Session.BasicContext.RMW(FixedKey,ref FixedInput,ref FixedOutput);
        Status=Pending(Session,Status);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }


    public bool Contains(TKey Key) {
        SpanByteAndMemory Output = default;
        var Status = this.Read(Key,ref Output);
        Debug.Assert(Status.Found==!Status.NotFound);
        return Status.Found;
    }
    public bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = Serialize(key);
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(PinnedSpanByte);
        SpanByteAndMemory Output = default;
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input,ref Output);
        Status=Pending(DeleteClientSession,Status);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Status.Found;
    }
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
            this.ClientSession.Dispose();
        }
        //アンマネージドリソースの解放（必要なければ省略可）
        this.disposed=true;
    }
    ~Collection()=>this.Dispose(false);
}
