using Garnet.common;

using MemoryPack;

using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using System.Diagnostics;
using Tsavorite.core;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うコレクションの抽象クラス
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
public abstract class VariableCollection<TKey,TValue> : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    
    private protected bool PrivateProtectedWouldOverflowMutable(int KeyLength,int ValueLength){
        Debug.Assert(this.LogMemorySize>=8192);
        var MutableBytes=(long)(this.LogMemorySize*this.MutableFraction);
        if(MutableBytes<=0){
            MutableBytes=this.LogMemorySize;
        } else{}
        var Log=this.CollectionStore.Store.Log;
        var Used=Log.TailAddress-Log.BeginAddress;
        var Required=(long)KeyLength+ValueLength+RecordOverheadBytes;
        return Used+Required>MutableBytes;
        //var Log = this.CollectionStore.Store.Log;
        //var Used = Log.TailAddress - Log.BeginAddress;
        //var MutableBytes = (long)(this.LogMemorySize * this.MutableFraction);
        //if(MutableBytes <= 0) {
        //    MutableBytes = this.LogMemorySize;
        //}
        //var Required = (long)KeyLength + ValueLength + RecordOverheadBytes;
        //// メモリの80%を使用したらコンパクションを実行
        //if(Used + Required > MutableBytes * 0.8) {
        //    this.CollectionStore.CompactWithLookup();
        //}   
        //return false; // Storage版は常にfalseを返す（エビクションで対応）
    }
    private protected const int RecordOverheadBytes=128;
    private protected long LogMemorySize=>this.CollectionStore.LogMemorySize;
    private protected double MutableFraction=>this.CollectionStore.MutableFraction;
    //private protected readonly long LogMemorySize;
    //private protected readonly double MutableFraction;
    //private protected readonly Add SessionFunctions=new();
    //private protected readonly GDeleteClientSession DeleteClientSession;
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









    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    private protected abstract bool WouldOverflowMutable(int KeyLength,int ValueLength);
    //private protected bool WouldOverflowMutable(int KeyLength,int ValueLength){
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
    private protected static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);
    private protected static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes)!;

    public long GetLongCount() {
        var Iterator = new CounterIterator();
        long Cursor = 0;
        this.ClientSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        return Iterator.Counter;
    }
    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.CollectionStore.Store.EntryCount;

    private protected readonly CollectionStore<TKey,TValue>CollectionStore;
#if TSAVORITE2_0BETA3
    public ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Add                ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> ClientSession { get;private set;}
    private protected ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,Delete             ,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> DeleteClientSession{ get;private set;}
    protected VariableCollection(CollectionStore<TKey,TValue>CollectionStore){
        this.CollectionStore=CollectionStore;
        var Store=CollectionStore.Store;
        this.ClientSession      =Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Add>(new());
        this.DeleteClientSession=Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Delete>(new());
    }
    public void Clear(){
        var Session=this.ClientSession;
        Session.BasicContext.CompletePending(wait:true);
        Session.Dispose();
        var Store=this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession      =Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Add>(new());
        this.DeleteClientSession=Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Empty, Delete>(new());
    }
    private protected Status Read(TKey Key,out SpanByteAndMemory Output){
        Output=default;
        var Session=this.ClientSession;
        var KeyBytes = Serialize(Key);
        var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
        var Status = Session.BasicContext.Read(FixedKey,ref Output);
        return Pending(Session,Status,ref Output);
    }
    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.ReadOnlySpan;
    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>Session,Status Status)
        where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            using(CompletedOutputs){
                if(CompletedOutputs.Next())
                    Status=CompletedOutputs.Current.Status;
            }
        }
        return Status;
    }
    private protected static Status Pending<TSessionFunctions0>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>Session,Status Status,ref SpanByteAndMemory Output)
        where TSessionFunctions0:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            using(CompletedOutputs){
                if(CompletedOutputs.Next()){
                    Status=CompletedOutputs.Current.Status;
                    Output=CompletedOutputs.Current.Output;
                }
            }
        }
        return Status;
    }
    private protected TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes = Serialize(Key);
        var ValueBytes = Serialize(Value);
        var log = this.CollectionStore.Store.Log;
        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)) {
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
    //public bool Remove(TKey key) {
    //    var DeleteClientSession = this.DeleteClientSession;
    //    var KeyBytes = Serialize(key);
    //    var FixedKey = (FixedSpanByteKey)KeyBytes;
    //    var Input = default(PinnedSpanByte);
    //    SpanByteAndMemory Output = default;
    //    var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input,ref Output);
    //    Status=Pending(DeleteClientSession,Status);
    //    Debug.Assert(Status.Found!=Status.NotFound);
    //    Debug.Assert(Status.Found==Output.Length>0);
    //    return Output.Length>0;
    //}
#elif TSAVORITE1_1_1
    //private protected readonly TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store;
    public ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,Add                ,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> ClientSession { get;private set;}
    private protected ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,Delete             ,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> DeleteClientSession{ get;private set;}
    protected VariableCollection(CollectionStore<TKey,TValue> CollectionStore){
        this.CollectionStore= CollectionStore;
        var Store= CollectionStore.Store;
        this.ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new());
        this.DeleteClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Delete>(new());
    }
    public void Clear(){
        var Session=this.ClientSession;
        Session.BasicContext.CompletePending(wait:true);
        Session.Dispose();
        var Store=this.CollectionStore.Store;
        Store.Reset();
        this.ClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Add>(new());
        this.DeleteClientSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,Delete>(new());
    }
    private protected Status Read(TKey Key,out SpanByteAndMemory Output){
        var KeyBytes = Serialize(Key);
        var Session=this.ClientSession;
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Status = Session.BasicContext.Read(FixedKey,out var Value0);
        var Result = Pending(Session,Status,ref Value0);
        Output = Value0;
        return Result;
    }
    private protected static ReadOnlySpan<byte> GetReadOnlySpan(SpanByteAndMemory a) => a.AsReadOnlySpan();
    private protected static Status Pending<TSessionFunctions0>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions0,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>Session,Status Status)
        where TSessionFunctions0:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>{
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
        where TSessionFunctions0:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions0  :IStoreFunctions<SpanByte,SpanByte>
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
        var log = this.CollectionStore.Store.Log;
        if(this.WouldOverflowMutable(KeyBytes.Length, ValueBytes.Length)){
            return TryAddResult.LogFull;
        }
        var Session=this.ClientSession;
        var FixedKey  =SpanByte.FromPinnedSpan(KeyBytes);
        var FixedInput=SpanByte.FromPinnedSpan(ValueBytes);
        SpanByteAndMemory Output = default;
        var Status = Session.BasicContext.RMW(ref FixedKey,ref FixedInput,ref Output);
        Status=Pending(this.ClientSession,Status);
        if(Status.NotFound) {
            return TryAddResult.Success;
        }
        return TryAddResult.KeyAlreadyExists;
    }
    //public bool Remove(TKey Key){
    //    var DeleteSession = this.DeleteClientSession;
    //    var KeyBytes = Serialize(Key);
    //    var Input = default(SpanByte);
    //    var Output = default(SpanByteAndMemory);
    //    var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
    //    var Status = DeleteSession.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
    //    Status=Pending(DeleteSession,Status);
    //    return Output.Length>0;
    //    //Debug.Assert(Status.Found!=Status.NotFound);
    //    //return Status.Found||Status.Record.Created||Status.Record.InPlaceUpdated;
    //}
#endif
    public bool Remove(TKey key) {
        var DeleteClientSession = this.DeleteClientSession;
        var KeyBytes = Serialize(key);
        var Output = default(SpanByteAndMemory);
#if TSAVORITE2_0BETA3
        var FixedKey = (FixedSpanByteKey)KeyBytes;
        var Input = default(PinnedSpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(FixedKey, ref Input,ref Output);
#elif TSAVORITE1_1_1
        var FixedKey = SpanByte.FromPinnedSpan(KeyBytes);
        var Input = default(SpanByte);
        var Status = DeleteClientSession.BasicContext.RMW(ref FixedKey,ref Input,ref Output);
#endif
        Status=Pending(DeleteClientSession,Status);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Status.Found;
    }
    public bool Contains(TKey Key) {
        var Status = this.Read(Key,out _);
        Debug.Assert(Status.Found==!Status.NotFound);
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
    ~VariableCollection()=>this.Dispose(false);
}
