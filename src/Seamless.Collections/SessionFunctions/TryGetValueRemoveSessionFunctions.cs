namespace Seamless.Collections;
using System.Buffers;
using Tsavorite.core;
/// <summary>
/// RMWで値の読み取りと削除を同時に行うセッション関数。
/// キーが存在する場合は値をOutputにコピーしてからExpireAndStopで論理削除する。
/// キーが存在しない場合はNeedInitialUpdate=falseでNotFoundを返す。
/// RMW
///     if(FoundMutable){
///         GetRMWModifiedFieldInfo
///         InPlaceUpdater → 値をOutputにコピーし、ExpireAndStopで論理削除
///     }else if(FoundReadOnly || FoundDisk){
///         NeedCopyUpdate → true
///         GetRMWModifiedFieldInfo
///         CopyUpdater → 値をOutputにコピーし、ExpireAndStopで論理削除
///     }else{
///         NeedInitialUpdate → false（キー未存在なので何もしない）
///     }
/// </summary>
public struct TryGetValueRemoveSessionFunctions:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
	private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
	public TryGetValueRemoveSessionFunctions(){}
	public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	//Upsert (not used)
	public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
	public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
	public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
	public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
	public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) { }
	public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) { }
	public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)where TSourceLogRecord : ISourceLogRecord { }
	public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor { }
	public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor { }
	// キー未存在時は新規作成しない
	public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TKey : IKey, allows ref struct {
		return false;
	}
	public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		return false;
	}
	// キー存在時（Mutable）は値をOutputにコピーしてからExpireAndStopで論理削除
	public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		logRecord.ValueSpan.CopyTo(ref output,this.memoryPool);
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { }
	// キー存在時（ReadOnly/Disk）はコピー更新を許可して値を読み取る
	public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
		return true;
	}
	public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
		srcLogRecord.ValueSpan.CopyTo(ref output,this.memoryPool);
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
	}
	public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { }
	//Delete (not used)
	public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException();
	}
	public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException();
	}
	public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException();
	}
	public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
		throw new NotSupportedException();
	}
	public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { throw new NotSupportedException(); }
	public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
		where TSourceLogRecord : ISourceLogRecord {
		return new() { KeySize=srcLogRecord.Key.Length,ValueSize=input.Length };
	}
	public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		return new() { KeySize=key.KeyBytes.Length,ValueSize=input.Length };
	}
	public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		throw new NotSupportedException();
	}
	public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		throw new NotSupportedException();
	}
	public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output) { }
}
