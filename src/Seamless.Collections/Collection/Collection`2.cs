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
public abstract partial class Collection<TKey,TValue>:IDisposable where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    private protected const int RecordOverheadBytes=128;
    private protected readonly GTsavoriteKV Store;
    private protected readonly long LogMemorySize;
    private protected readonly double MutableFraction;
    //private protected readonly Add SessionFunctions=new();
    public GAddClientSession ClientSession { get;private set;}
    //private protected readonly GDeleteClientSession DeleteClientSession;
    private protected GDeleteClientSession DeleteClientSession{ get;private set;}
    //private void Initialize(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir){
    //    this.Settings = new KVSettings {
    //        IndexSize=IndexSize,
    //        LogMemorySize=MemorySize,
    //        PageSize=PageSize,
    //        SegmentSize=SegmentSize,

    //        MutableFraction=MutableFraction,
    //        LogDevice=LogDevice,
    //        ObjectLogDevice=ObjectLogDevice,
    //        ObjectLogSegmentSize=ObjectLogSegmentSize,
    //        CheckpointDir=CheckpointDir,
    //    };
    //}
    private partial void Initialize();
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
    protected Collection(GTsavoriteKV Store,long LogMemorySize=0,double MutableFraction=1.0){
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
        this.Store=Store;
        this.LogMemorySize=LogMemorySize;
        this.MutableFraction=MutableFraction;
        this.Initialize();
        //this.ClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(this.SessionFunctions);
        //this.DeleteClientSession=Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Delete>(new ());
    }
    public void Clear(){
        var Session=this.ClientSession;
        Session.BasicContext.CompletePending(wait:true);
        Session.Dispose();
        this.Store.Reset();
        this.Initialize();
        //this.ClientSession=this.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add>(this.SessionFunctions);
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
    //private protected Status Read(TKey Key,ref SpanByteAndMemory Output) {
    //    var Session=this.ClientSession;
    //    var KeyBytes = Serialize(Key);
    //    var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
    //    var Status = Session.BasicContext.Read(FixedKey,ref Output);
    //    return Pending(Session,Status,ref Output);
    //}
    private protected partial Status Read(TKey Key,out SpanByteAndMemory Output);

    
    
    
    
    //private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,GStoreFunctions,GAllocator>Session,Status Status)
    //    where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
    //    if(Status.IsPending) {
    //        Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        using(CompletedOutputs){
    //            if(CompletedOutputs.Next())
    //                Status=CompletedOutputs.Current.Status;
    //        }
    //    }
    //    return Status;
    //}
    //private protected static Status Pending<TSessionFunctions0>( ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,GStoreFunctions,GAllocator>Session,Status Status,ref SpanByteAndMemory Output)
    //    where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
    //    if(Status.IsPending) {
    //        Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
    //        using(CompletedOutputs){
    //            if(CompletedOutputs.Next()){
    //                Status=CompletedOutputs.Current.Status;
    //                Output=CompletedOutputs.Current.Output;
    //            }
    //        }
    //    }
    //    return Status;
    //}
    private protected partial TryAddResult PrivateTryAdd(TKey Key,TValue Value);
    public bool Contains(TKey Key) {
        var Status = this.Read(Key,out _);
        Debug.Assert(Status.Found==!Status.NotFound);
        return Status.Found;
    }
    public partial bool Remove(TKey key);
    //public bool Remove(TKey key) {
    //    var DeleteClientSession = this.DeleteClientSession;
    //    var KeyBytes = Serialize(key);
    //    var FixedKey = (FixedSpanByteKey)KeyBytes;
    //    var Input = default(PinnedSpanByte);
    //    SpanByteAndMemory Output = default;
    //    var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input,ref Output);
    //    Status=Pending(DeleteClientSession,Status);
    //    Debug.Assert(Status.Found!=Status.NotFound);
    //    return Status.Found;
    //}
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
