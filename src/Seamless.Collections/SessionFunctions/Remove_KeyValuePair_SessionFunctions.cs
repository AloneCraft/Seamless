using System.Diagnostics.CodeAnalysis;
namespace Seamless.Collections;
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
public readonly struct Remove_KeyValuePair_SessionFunctions:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
	private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
	public Remove_KeyValuePair_SessionFunctions(){}
	private void WriteMarker(ref SpanByteAndMemory output) {
		output.Memory=this.memoryPool.Rent(1);
		output.Length=1;
		output.Memory.Memory.Span[0]=1;
	}
    [ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	//Upsert (not used)
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
    }
    public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
    }
	// キー未存在時は新規作成しない
	public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TKey : IKey, allows ref struct {
		return false;
	}
	public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		return false;
	}
	// キー存在時（Mutable）はValueが一致する場合のみExpireAndStopで論理削除
	public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		if(logRecord.ValueSpan.SequenceEqual(input.ReadOnlySpan)) {
			WriteMarker(ref output);
			rmwInfo.Action=RMWAction.ExpireAndStop;
		}
		return false;
	}
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
    }
	// キー存在時（ReadOnly/Disk）はValueが一致する場合のみコピー更新を許可
	public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
		return srcLogRecord.ValueSpan.SequenceEqual(input.ReadOnlySpan);
	}
	public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
		WriteMarker(ref output);
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
    [ExcludeFromCodeCoverage]public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
	}
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
    }
	//Delete (not used)
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
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
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		throw new NotSupportedException();
	}
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
	[ExcludeFromCodeCoverage]public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output) { }
}
