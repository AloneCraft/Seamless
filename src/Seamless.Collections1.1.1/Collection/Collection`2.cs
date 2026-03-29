using System.Diagnostics;
using MemoryPack;
using Tsavorite.core;

namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うコレクションの抽象クラス
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
//public abstract class Collection<TKey, TValue>:Collection<TKey,TValue,AddSessionFunctions<TKey,TValue>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>where TKey : IEquatable<TKey>where TValue : IEquatable<TValue>{
public abstract class Collection<TKey,TValue>:IDisposable where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    private protected const int RecordOverheadBytes = 128;
    private protected readonly TsavoriteKV<SpanByte,SpanByte,TStoreFunctions,TAllocator> Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    private protected readonly AddSessionFunctions<TKey,TValue> SessionFunctions;
    public                     ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,AddSessionFunctions<TKey,TValue>,TStoreFunctions,TAllocator> ClientSession;
    private protected readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions<TKey>,TStoreFunctions,TAllocator> DeleteClientSession;
    protected Collection(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store,long LogMemorySize=0,double MutableFraction=1.0){
        //:base(Store,new AddSessionFunctions<TKey,TValue>(),LogMemorySize,MutableFraction){
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.SessionFunctions=new();
        this.ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,AddSessionFunctions<TKey,TValue>>(SessionFunctions);
        this.DeleteClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions<TKey>>(new DeleteSessionFunctions<TKey>());
    }
    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
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
    private protected Status Read(TKey Key,out SpanByteAndMemory Output){
        var KeyBytes = Serialize(Key);
        SpanByteAndMemory Value0 = default;
        unsafe {
            fixed(byte* pKey = KeyBytes){
                var Session=this.ClientSession;
                var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
                var Status = Session.BasicContext.Read(ref FixedKey,ref Value0);
                var Result = Pending(Session,Status,ref Value0);
                Output = Value0;
                return Result;
            }
        }
    }
    //private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,TStoreFunctions,TAllocator>Session,Status Status)
    //    where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
    //    if(Status.IsPending) {
    //        Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        CompletedOutputs.Next();
    //        Status=CompletedOutputs.Current.Status;
    //        CompletedOutputs.Dispose();
    //    } else {
    //    }
    //    return Status;
    //}
    private protected static Status Pending<TSessionFunctions0>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,TStoreFunctions,TAllocator>Session,Status Status)
        where TSessionFunctions0:SSessionFunctions{
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            CompletedOutputs.Next();
            Status=CompletedOutputs.Current.Status;
            CompletedOutputs.Dispose();
        } else {
        }
        return Status;
    }
    private protected static Status Pending<TSessionFunctions0,TStoreFunctions0,TAllocator0>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,TStoreFunctions0,TAllocator0>Session,Status Status,ref SpanByteAndMemory Output)
        where TSessionFunctions0:SSessionFunctions
        where TStoreFunctions0  :SStoreFunctions
        where TAllocator0 : IAllocator<SpanByte,SpanByte,TStoreFunctions0>{
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            CompletedOutputs.Next();
            Status=CompletedOutputs.Current.Status;
            Output=CompletedOutputs.Current.Output;
            CompletedOutputs.Dispose();
        } else {
        }
        return Status;
    }
    private protected TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes = Serialize(Key);
        var ValueBytes = Serialize(Value);
        if(this.WouldOverflowMutable(KeyBytes.Length,ValueBytes.Length)) {
            return TryAddResult.LogFull;
        }
        unsafe {
            fixed(byte* pKey = KeyBytes, pValue = ValueBytes) {
                var Session=this.ClientSession;
                var FixedKey   =SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
                var FixedInput=SpanByte.FromPinnedPointer(pValue,ValueBytes.Length);
                SpanByteAndMemory Output = default;
                var Status = Session.BasicContext.RMW(ref FixedKey,ref FixedInput,ref Output);
                Status=Pending(this.ClientSession,Status);
                if(Status.NotFound) {
                    return TryAddResult.Success;
                }
                return TryAddResult.KeyAlreadyExists;
            }
        }
    }
    public bool Contains(TKey Key) {
        var Status = this.Read(Key,out _);
        Debug.Assert(Status.Found==!Status.NotFound);
        return Status.Found;
    }
    public bool Remove(TKey Key) {
        var DeleteSession = this.DeleteClientSession;
        var KeyBytes = Serialize(Key);
        SpanByte Input = default;
        SpanByteAndMemory Output = default;
        unsafe{
            fixed(byte* pKey=KeyBytes){
                var FixedKey = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
                var Status = DeleteSession.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
                Status=Pending(DeleteSession,Status);
                Debug.Assert(Status.Found!=Status.NotFound);
                return Status.Found||Status.Record.Created||Status.Record.InPlaceUpdated;
            }
        }
        //var Context = this.Session.BasicContext;
        //var KeyBytes = Serialize(Key);
        //unsafe{
        //    fixed(byte* pKey=KeyBytes){
        //        var Status=Context.Delete(SpanByte.FromPinnedPointer(pKey,KeyBytes.Length));
        //        Status=Pending(Session,Status);
        //        return Status.Found||Status.Record.Created||Status.Record.InPlaceUpdated;
        //    }
        //}
    }
    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        this.Store.Reset();
        this.ClientSession=this.Store.NewSession<SpanByte,SpanByteAndMemory,Empty,AddSessionFunctions<TKey,TValue>>(this.SessionFunctions);
    }
    private protected KeyEnumerable<TKey,TValue,AddSessionFunctions<TKey,TValue>,TStoreFunctions,TAllocator> GetKeyEnumerable() => new(this.ClientSession);
    private protected ValueEnumerable<TKey,TValue,AddSessionFunctions<TKey,TValue>,TStoreFunctions,TAllocator> GetValueEnumerable() => new(this.ClientSession);
    private protected KeyValueEnumerable<TKey,TValue,AddSessionFunctions<TKey,TValue>,TStoreFunctions,TAllocator> GetKeyValueEnumerable() => new(this.ClientSession);
    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    private bool Disposed;
    public abstract bool IsDisposed{get;}
    protected virtual void Dispose(bool disposing) {
        if(this.Disposed) return;
        if(disposing) {
            //マネージドリソースの解放
            this.ClientSession.Dispose();
        }
        //アンマネージドリソースの解放（必要なければ省略可）
        this.Disposed=true;
    }
    ~Collection() => this.Dispose(false);
}
