using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

using Tsavorite.core;

internal readonly struct SymmetricExceptWith:
#if TSAVORITE2_0BETA3
	ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Collections.Empty>
#elif TSAVORITE1_1_1
	ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Seamless.Collections.Empty>
#endif
{
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
#elif TSAVORITE1_1_1
	public bool SingleReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo) => true;
	public bool ConcurrentReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo) => true;
#endif
#if TSAVORITE2_0BETA3
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
#elif TSAVORITE1_1_1
	public bool SingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
		Src.CopyTo(ref Dst);
		return true;
	}
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
	[ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
		throw new NotSupportedException();
	}
	[ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord : ISourceLogRecord {
		throw new NotSupportedException();
	}
#elif TSAVORITE1_1_1
	public bool ConcurrentWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
		Src.CopyTo(ref Dst);
		return true;
	}
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
		throw new NotSupportedException();
	}
	[ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
		throw new NotSupportedException();
	}
	[ExcludeFromCodeCoverage]public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord:ISourceLogRecord{
		throw new NotSupportedException();
	}
#elif TSAVORITE1_1_1
	public void PostSingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason) { }
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
		where TKey:IKey,allows ref struct
		where TEpochAccessor:IEpochAccessor{
		throw new NotSupportedException();
	}
	[ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
		where TKey:IKey,allows ref struct
		where TEpochAccessor:IEpochAccessor{
		throw new NotSupportedException();
	}
#elif TSAVORITE1_1_1
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException();
    }
#endif
#if TSAVORITE2_0BETA3
	public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
		where TKey : IKey, allows ref struct {
		return true;
	}
#elif TSAVORITE1_1_1
	public bool NeedInitialUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => true;
#endif
#if TSAVORITE2_0BETA3
	public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		rmwInfo.Action = RMWAction.Default;
		return dstLogRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
	}
#elif TSAVORITE1_1_1
	public bool InitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		RmwInfo.Action = RMWAction.Default;
		Input.CopyTo(ref Value);
		return true;
	}
#endif
#if TSAVORITE2_0BETA3
	public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
#elif TSAVORITE1_1_1
	public bool InPlaceUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		RmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
	}
#elif TSAVORITE1_1_1
	public void PostInitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) { }
#endif
#if TSAVORITE2_0BETA3
	public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
		return true;
	}
#elif TSAVORITE1_1_1
	public bool NeedCopyUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => true;
#endif
#if TSAVORITE2_0BETA3
	public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
#elif TSAVORITE1_1_1
	public bool CopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
		RmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
#endif
#if TSAVORITE2_0BETA3
	public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
		return true;
	}
#elif TSAVORITE1_1_1
	public bool PostCopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => true;
#endif
#if TSAVORITE2_0BETA3
	public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
		where TKey : IKey, allows ref struct
		where TEpochAccessor : IEpochAccessor {
	}
#elif TSAVORITE1_1_1
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }
#endif
#if TSAVORITE2_0BETA3
	public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
		//CompletePendingWithOutputs
	}
#elif TSAVORITE1_1_1
    public void RMWCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Collections.Empty Ctx,Status Status,RecordMetadata RecordMetadata) { }
	public bool SingleDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) => true;
	public void PostSingleDeleter(ref SpanByte Key,ref DeleteInfo DeleteInfo) { }
	public bool ConcurrentDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) => true;
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException("SymmetricExceptWith");
	}
	[ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
		throw new NotSupportedException("SymmetricExceptWith");
	}
	[ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
		throw new NotSupportedException("SymmetricExceptWith");
	}
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
		where TKey : IKey, allows ref struct
		where TEpochAccessor : IEpochAccessor {
		throw new NotSupportedException();
	}
#elif TSAVORITE1_1_1
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte Key,ref DeleteInfo DeleteInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException();
    }
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
		throw new NotSupportedException("Read非同期で呼ばれる");
	}
#elif TSAVORITE1_1_1
	public void ReadCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Collections.Empty Ctx,Status Status,RecordMetadata RecordMetadata) { }
#endif
#if TSAVORITE2_0BETA3
	public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
		where TSourceLogRecord : ISourceLogRecord {
		return new() { KeySize=srcLogRecord.Key.Length,ValueSize=input.Length };
	}
#elif TSAVORITE1_1_1
	public int GetRMWModifiedValueLength(ref SpanByte Value,ref SpanByte Input) => Value.Length+4;
#endif
#if TSAVORITE2_0BETA3
	public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		return new() { KeySize=key.KeyBytes.Length,ValueSize=input.Length };
	}
#elif TSAVORITE1_1_1
	public int GetRMWInitialValueLength(ref SpanByte Input) => Input.Length+4;
#endif
#if TSAVORITE2_0BETA3
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
#elif TSAVORITE1_1_1
	public int GetUpsertValueLength(ref SpanByte Value,ref SpanByte Input) => Value.Length+4;
#endif
#if TSAVORITE2_0BETA3
	public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){
	}
#elif TSAVORITE1_1_1
	public void ConvertOutputToHeap(ref SpanByte Input,ref SpanByteAndMemory Output) { }
#endif
}
