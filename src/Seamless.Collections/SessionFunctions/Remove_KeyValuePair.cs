using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;
using System.Buffers;
using Tsavorite.core;
/// <summary>
/// RMWでKey,Value両方が一致するエントリのみ削除するセッション関数。
/// Inputにシリアライズ済みのValue bytesを渡し、ストア上のValueSpanとバイト比較する。
/// 一致すればOutputにマーカー(1byte)を書き込みExpireAndStopで論理削除。
/// 不一致またはキー未存在ならOutputは空のまま。
/// RMW
///     if(FoundMutable){
///         InPlaceUpdater → ValueSpan==Input ? マーカー出力+ExpireAndStop : 何もしない(false)
///     }else if(FoundReadOnly || FoundDisk){
///         NeedCopyUpdate → ValueSpan==Input ? true : false
///         CopyUpdater → マーカー出力+ExpireAndStop
///     }else{
///         NeedInitialUpdate → false（キー未存在なので何もしない）
///     }
/// </summary>
internal readonly struct Remove_KeyValuePair():
#if TSAVORITE2_0BETA3
    ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
#elif TSAVORITE1_1_1
    ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
#endif
    {
	private readonly MemoryPool<byte> MemoryPool = MemoryPool<byte>.Shared;
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)where TSourceLogRecord : ISourceLogRecord=>
        throw new NotSupportedException("Read");
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo)=>
        throw new NotSupportedException("Read");
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo)=>
        throw new NotSupportedException("Read");
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
	}
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
        throw new NotSupportedException("Upsert");
    }
#elif TSAVORITE1_1_1
        [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason){
        throw new NotSupportedException("Upsert");
	}
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
	}
#endif
#if TSAVORITE2_0BETA3
    public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TKey : IKey, allows ref struct {
        return false;// キー未存在時は新規作成しない
    }
#elif TSAVORITE1_1_1
	public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;// キー未存在時は新規作成しない
	}
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
		throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	}
#endif
#if TSAVORITE2_0BETA3
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        // キー存在時（Mutable）はValueが一致する場合のみExpireAndStopで論理削除
        if(logRecord.ValueSpan.SequenceEqual(input.ReadOnlySpan)) {
            output.Length=1;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
    }
#elif TSAVORITE1_1_1
	public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
    	// キー存在時（Mutable）は値をOutputにコピーしてからExpireAndStopで論理削除
        if(value.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan())){
            output.Length=1;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
	}
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("InitialUpdaterが呼ばれないので呼ばれない");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		throw new NotSupportedException("InitialUpdaterが呼ばれないので呼ばれない");
	}
#endif
#if TSAVORITE2_0BETA3
	// キー存在時（ReadOnly/Disk）は値が一致したらMutableに削除レコードを追加する
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        return srcLogRecord.ValueSpan.SequenceEqual(input.ReadOnlySpan);
    }
#elif TSAVORITE1_1_1
	// キー存在時（ReadOnly/Disk）は値が一致したらMutableに削除レコードを追加する
	public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return oldValue.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan());
	}
#endif
#if TSAVORITE2_0BETA3
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        output.Length=1;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
#elif TSAVORITE1_1_1
	public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output.Length=1;
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
#elif TSAVORITE1_1_1
	public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
	}
#endif
#if TSAVORITE2_0BETA3
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
        //RMW 確定後に呼ばれる。
    }
#elif TSAVORITE1_1_1
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
		//RMW 確定後に呼ばれる。
	}
#endif
#if TSAVORITE2_0BETA3
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
        //CompletePendingWithOutputs
    }
#elif TSAVORITE1_1_1
	public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
		//CompletePendingWithOutputs
	}
#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Delete");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
	}
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Read の非同期I/O完了時に呼ばれるので呼ばれない");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
		throw new NotSupportedException("Readの非同期I/O完了時に呼ばれるので呼ばれない");
	}
#endif
#if TSAVORITE2_0BETA3
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=input.Length };
    }
#elif TSAVORITE1_1_1
    public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public int GetRMWInitialValueLength(ref SpanByte input){
	    throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	}
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input){
        throw new NotSupportedException("Upsert");
    }
#endif
#if TSAVORITE2_0BETA3
    public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){

    }
#elif TSAVORITE1_1_1
    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { 
        //RMWでPending待ちで削除するとき呼ばれる
    }
#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
#endif
}
