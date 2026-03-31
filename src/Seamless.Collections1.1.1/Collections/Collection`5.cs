using Seamless.Collections.Enumerators;
using Seamless.SessionFunctions;
namespace Seamless.Collections;
using Tsavorite.core;
//AddSessionFunctions<TKey,TValue>,
//StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,
//SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>
public abstract class Collection<TKey, TValue, TSessionFunctions, TStoreFunctions, TAllocator>:IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TSessionFunctions : SSessionFunctions
    where TStoreFunctions : SStoreFunctions
    where TAllocator : IAllocator<SpanByte,SpanByte,TStoreFunctions> {
    private protected const int RecordOverheadBytes = 128;
    private protected readonly TsavoriteKV<SpanByte,SpanByte,TStoreFunctions,TAllocator> Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    private protected readonly TSessionFunctions SessionFunctions;
    public                     ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> ClientSession;
    private protected readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,Delete<TKey>,TStoreFunctions,TAllocator> DeleteClientSession;
    private protected Collection(TsavoriteKV<SpanByte,SpanByte,TStoreFunctions,TAllocator> Store,TSessionFunctions SessionFunctions,long LogMemorySize = 0,double MutableFraction = 1.0) {
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.SessionFunctions=SessionFunctions;
        this.ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(SessionFunctions);
        this.DeleteClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Delete<TKey>>(new Delete<TKey>());
    }
    //private protected void ThrowLogFull() {
    //    var Log = this.Store.Log;
    //    throw new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    //}
    //private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
    //    //return false;
    //    if(this.LogMemorySize<=0) {
    //        return false;
    //    }
    //    var MutableBytes = (long)(this.LogMemorySize*this.MutableFraction);
    //    if(MutableBytes<=0) {
    //        MutableBytes=this.LogMemorySize;
    //    }
    //    var Log = this.Store.Log;
    //    var Used = Log.TailAddress-Log.BeginAddress;
    //    var Required = (long)KeyLength+ValueLength+RecordOverheadBytes;
    //    return Used+Required>MutableBytes;
    //}
    //private protected static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    //private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;
    //public long GetLongCount() {
    //    var Iterator = new CounterIterator();
    //    long Cursor = 0;
    //    while(this.Session.IterateLookup(ref Iterator,ref Cursor,resetCursor: false)) ;
    //    return Iterator.Counter;
    //}
    //public int Count => (int)this.GetLongCount();
    //public long EntryCount => this.Store.EntryCount;
    //public virtual bool Remove(TKey Key) {
    //    var Context = this.Session.BasicContext;
    //    var KeyBytes = Serialize(Key);
    //    unsafe{
    //        fixed(byte* pKey=KeyBytes){
    //            var Status=Context.Delete(SpanByte.FromPinnedPointer(pKey,KeyBytes.Length));
    //            if(Status.IsPending){
    //                this.Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //                CompletedOutputs.Next();
    //                Status=CompletedOutputs.Current.Status;
    //                CompletedOutputs.Dispose();
    //            }else{
    //            }
    //            return Status.Found;
    //            //return Status.Found||Status.Record.Created||Status.Record.InPlaceUpdated;
    //        }
    //    }
    //}




    public void Clear() {
        var Session = this.ClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        this.Store.Reset();
        this.ClientSession=this.Store.NewSession<SpanByte,SpanByteAndMemory,Empty,TSessionFunctions>(this.SessionFunctions);
    }
    private protected KeyEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetKeyEnumerable() => new(this.ClientSession);
    private protected ValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetValueEnumerable() => new(this.ClientSession);
    private protected KeyValueEnumerable<TKey,TValue,TSessionFunctions,TStoreFunctions,TAllocator> GetKeyValueEnumerable() => new(this.ClientSession);
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
