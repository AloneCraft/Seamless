using System.Buffers.Binary;
namespace Seamless.Collections.Tsavorite;

/// <summary>アドレスはストアと回収世代の中でだけ有効。外部へポインタを渡さない。</summary>
public readonly record struct HistoryAddress(Guid StoreId,long Generation,long Address);
public readonly record struct HistoryRecord(byte[] Key,byte[] Row,long CommitVersion,bool Deleted,HistoryAddress Address,HistoryAddress PreviousAddress);
public sealed class RetainedHistoryUnavailableException(string message):IOException(message);

/// <summary>共通取引接続層向けのコピー更新ストア。値の削除フラグも通常の行として保持する。</summary>
public sealed partial class HistoryStore:CollectionStore<byte[],byte[]>{
    private readonly Guid identity=Guid.NewGuid();
    private long generation=1;
    private long maximumCommittedVersion;
    internal HistoryStore(TsavoriteLayout layout):base(layout){this.Kind=layout.Kind;this.PageBytes=layout.PageSize;this.SchemaIdentity=layout.RecordFormat!["SeamlessHistoryV1|".Length..];}
    public string SchemaIdentity{get;}
    public global::Seamless.Execution.StoreKind Kind{get;}
    public long PageBytes{get;}
    public long MaximumCommittedVersion=>Interlocked.Read(ref this.maximumCommittedVersion);
    public bool HasRecoveredCheckpoint{get;private set;}
    protected override long? RecordCommitVersion=>this.MaximumCommittedVersion;
    protected override void RestoreRecordCommitVersion(long? version){this.maximumCommittedVersion=version??throw new InvalidDataException("The versioned checkpoint is missing its publication version.");this.HasRecoveredCheckpoint=true;}
    internal void Installed(long version){long before;do{before=Interlocked.Read(ref this.maximumCommittedVersion);if(version<=before){return;}}while(Interlocked.CompareExchange(ref this.maximumCommittedVersion,version,before)!=before);}
    public HistoryAccessor Create()=>new(this,null);
    public ParallelRegion<HistoryAccessor> BeginParallel(){this.InvalidateManagedCount();return this.BeginParallelCore(0,region=>new HistoryAccessor(this,region));}
    internal HistoryAddress Token(long value)=>new(this.identity,Volatile.Read(ref this.generation),value);
    internal void Validate(HistoryAddress address){
        if(address.StoreId!=this.identity||address.Generation!=Volatile.Read(ref this.generation)){throw new InvalidOperationException("The history address belongs to a different store or reclamation generation.");}
        if(address.Address<this.Store.Log.BeginAddress){throw new RetainedHistoryUnavailableException("The requested history has already been reclaimed.");}
    }
    /// <summary>全accessorとparallel regionを閉じた所有者だけが呼べる。</summary>
    public void Reclaim(){
        using(var access=this.Create()){access.RemoveDeletedHeads();}
        base.Compact();Interlocked.Increment(ref this.generation);
    }
}

public sealed partial class HistoryAccessor:IDisposable{
    private readonly HistoryStore store;
    private readonly ParallelRegionState? region;
    private readonly ClientSession<FixedSpanByteKey,byte[],HistoryRead,Empty,HistoryFunctions,SpanByteStoreFunctions,SpanByteStoreAllocator> session;
    private bool disposed;
    internal HistoryAccessor(HistoryStore store,ParallelRegionState? region){
        this.store=store;this.region=region;store.OnAccessorCreated(region);
        try{this.session=store.Store.NewSession<FixedSpanByteKey,byte[],HistoryRead,Empty,HistoryFunctions>(new());}
        catch{store.OnAccessorDisposed(region,false,0);throw;}
    }
    public unsafe HistoryRecord? ReadRecord(ReadOnlySpan<byte> key){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var input=Array.Empty<byte>();var output=default(HistoryRead);var options=new ReadOptions{CopyOptions=ReadCopyOptions.None};Status status;
        fixed(byte* pointer=key){status=this.session.BasicContext.Read((FixedSpanByteKey)key,ref input,ref output,ref options,out var metadata);output=this.Complete(ref status,output,metadata);}
        if(!status.Found){return null;}return this.Record(output);
    }
    public HistoryRecord ReadRecordAtAddress(HistoryAddress address){
        ObjectDisposedException.ThrowIf(this.disposed,this);this.store.Validate(address);
        var input=Array.Empty<byte>();var output=default(HistoryRead);var options=new ReadOptions{CopyOptions=ReadCopyOptions.None};
        var status=this.session.BasicContext.ReadAtAddress(address.Address,ref input,ref output,ref options,out var metadata);output=this.Complete(ref status,output,metadata);
        if(!output.Captured){throw new RetainedHistoryUnavailableException("A retained history record could not be read.");}return this.Record(output);
    }
    public HistoryRecord? ReadVisibleRecord(ReadOnlySpan<byte> key,long readVersion){
        var record=this.ReadRecord(key);
        while(record.HasValue){
            var current=record.Value;
            // PreviousAddressは同一キーだけの連鎖ではない。衝突した別キーを版として返さない。
            if(current.Key.AsSpan().SequenceEqual(key)&&current.CommitVersion<=readVersion){return current;}
            // 未存在のキーの終端には0以外の予約アドレスも使われる。実レコードの下限と区別する。
            if(current.PreviousAddress.Address<LogAddress.FirstValidAddress||current.PreviousAddress.Address>=current.Address.Address){return null;}
            record=this.ReadRecordAtAddress(current.PreviousAddress);
        }
        return null;
    }
    public unsafe void AppendCommittedVersion(ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long commitVersion,bool deleted){
        ObjectDisposedException.ThrowIf(this.disposed,this);ArgumentOutOfRangeException.ThrowIfNegative(commitVersion);
        using var structure=this.store.Structure.Write();
        var input=HistoryFunctions.Encode(row,commitVersion,deleted);var output=default(HistoryRead);var options=new RMWOptions();Status status;
        this.store.EnsureHistoryCapacity(checked(this.store.Store.Log.TailAddress+this.store.EstimateAppendBytes(key.Length,row.Length)));
        fixed(byte* pointer=key){status=this.session.BasicContext.RMW((FixedSpanByteKey)key,ref input,ref output,ref options);this.Complete(ref status,output);}this.store.Installed(commitVersion);
        this.store.InvalidateManagedCount();
    }
    private HistoryRead Complete(ref Status status,HistoryRead output,RecordMetadata metadata=default){
        if(status.IsPending){
            using var cpu=global::Seamless.Execution.CpuScheduler.SuspendForIo();
            this.session.BasicContext.CompletePendingWithOutputs(out var completed,wait:true);
            using(completed){if(!completed.Next()){throw new IOException("The native operation completed without a result.");}output=completed.Current.Output;status=completed.Current.Status;metadata=completed.Current.RecordMetadata;}
        }
        if(!status.IsCompletedSuccessfully){throw new IOException("The native history operation failed: "+status);}
        // ReadInfo.Addressはhash-tag探索前の位置の場合がある。完了metadataの実レコード位置を使う。
        if(output.Captured){output=output with{Address=metadata.Address};}return output;
    }
    private HistoryRecord Record(HistoryRead read){
        if(!read.Captured||read.Address<LogAddress.FirstValidAddress){throw new InvalidDataException("The native read did not capture completed record metadata.");}
        var header=HistoryFunctions.Decode(read.Value);
        return new(read.Key,read.Value.AsSpan(HistoryFunctions.HeaderBytes).ToArray(),header.Version,header.Deleted,this.store.Token(read.Address),this.store.Token(read.Previous));
    }
    public void Dispose(){
        if(this.disposed){return;}this.disposed=true;
        try{this.session.Dispose();}finally{this.store.OnAccessorDisposed(this.region,false,0);}
    }
}

internal readonly record struct HistoryRead(byte[] Key,byte[] Value,long Address,long Previous,bool Captured);
internal sealed class HistoryFunctions:SessionFunctionsBase<byte[],HistoryRead,Empty>{
    internal const int HeaderBytes=21;
    private const ulong Magic=0x315643434D4C5353;
    internal static byte[] Encode(ReadOnlySpan<byte> row,long version,bool deleted){
        var value=new byte[checked(HeaderBytes+row.Length)];BinaryPrimitives.WriteUInt64LittleEndian(value,Magic);BinaryPrimitives.WriteInt64LittleEndian(value.AsSpan(8),version);
        if(deleted){value[16]=1;}BinaryPrimitives.WriteInt32LittleEndian(value.AsSpan(17),row.Length);row.CopyTo(value.AsSpan(HeaderBytes));return value;
    }
    internal static (long Version,bool Deleted) Decode(ReadOnlySpan<byte> value){
        if(value.Length<HeaderBytes||BinaryPrimitives.ReadUInt64LittleEndian(value)!=Magic||value[16]>1||BinaryPrimitives.ReadInt32LittleEndian(value[17..])!=value.Length-HeaderBytes){throw new InvalidDataException("The versioned row format is invalid.");}
        var version=BinaryPrimitives.ReadInt64LittleEndian(value[8..]);if(version<0){throw new InvalidDataException("The committed version is invalid.");}return(version,value[16]!=0);
    }
    public override bool Reader<T>(in T source,ref byte[] input,ref HistoryRead output,ref ReadInfo info){using var cpu=global::Seamless.Execution.CpuScheduler.EnterCallback();output=new(source.KeyBytes.ToArray(),source.ValueSpan.ToArray(),0,source.Info.PreviousAddress,true);return true;}
    public override RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref byte[] input)=>new(){KeySize=key.KeyBytes.Length,ValueSize=input.Length};
    public override RecordFieldInfo GetRMWModifiedFieldInfo<T>(in T source,ref byte[] input)=>new(){KeySize=source.KeyBytes.Length,ValueSize=input.Length};
    public override bool InPlaceUpdater(ref LogRecord record,ref byte[] input,ref HistoryRead output,ref RMWInfo info)=>false;
    public override bool InitialUpdater(ref LogRecord record,in RecordSizeInfo size,ref byte[] input,ref HistoryRead output,ref RMWInfo info)=>record.TrySetValueSpanAndPrepareOptionals(input,in size);
    public override bool CopyUpdater<T>(in T source,ref LogRecord record,in RecordSizeInfo size,ref byte[] input,ref HistoryRead output,ref RMWInfo info){
        info.PreserveCopyUpdaterSourceRecord=true;
        return record.TrySetValueSpanAndPrepareOptionals(input,in size);
    }
}
