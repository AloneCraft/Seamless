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
        throw new NotSupportedException("Read");
	}
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Read");
    }
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
	[ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
		where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
	}
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
        throw new NotSupportedException("Upsert");
    }
	[ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
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
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason){
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
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
#endif
#if TSAVORITE2_0BETA3
	public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
		where TKey : IKey, allows ref struct {
        return true;// キー未存在時は新規作成する
	}
#elif TSAVORITE1_1_1
	public bool NeedInitialUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo){
        return true;// キー未存在時は新規作成する
    }
#endif
#if TSAVORITE2_0BETA3
	public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        //NeedInitialUpdateがtrueを返すときに呼ばれる
		return dstLogRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
	}
#elif TSAVORITE1_1_1
    public bool InitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        //NeedInitialUpdateがtrueを返すときに呼ばれる
		RmwInfo.Action = RMWAction.Default;
		Input.CopyTo(ref Value);
		return true;
	}
#endif
#if TSAVORITE2_0BETA3
	public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
		rmwInfo.Action=RMWAction.ExpireAndStop;//キーが存在する場合は削除する。SymmetricExceptWithなので、更新はせずに削除のみ行う。
		return false;
	}
#elif TSAVORITE1_1_1
    public bool InPlaceUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		RmwInfo.Action=RMWAction.ExpireAndStop;//キーが存在する場合は削除する。SymmetricExceptWithなので、更新はせずに削除のみ行う。
		return false;
	}
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
	}
#elif TSAVORITE1_1_1
    public void PostInitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo){

    }
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
	[ExcludeFromCodeCoverage]public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
		where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
	}
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool PostCopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
	}
#endif
#if TSAVORITE2_0BETA3
	public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
		where TKey : IKey, allows ref struct
		where TEpochAccessor : IEpochAccessor {
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。
	}
#elif TSAVORITE1_1_1
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。
    }
#endif
#if TSAVORITE2_0BETA3
	public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
		//CompletePendingWithOutputs
	}
#elif TSAVORITE1_1_1
    public void RMWCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Collections.Empty Ctx,Status Status,RecordMetadata RecordMetadata){
        //CompletePendingWithOutputs
    }
#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte Key,ref DeleteInfo DeleteInfo){
        throw new NotSupportedException("Delete");
    }
	[ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Delete");
    }
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete");
	}
	[ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
	}
	[ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete");
	}
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
		where TKey : IKey, allows ref struct
		where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Delete");
	}
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TEpochAccessor>(ref SpanByte Key,ref DeleteInfo DeleteInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
    }
#endif
#if TSAVORITE2_0BETA3
	[ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
		throw new NotSupportedException("Read非同期で呼ばれる");
	}
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Collections.Empty Ctx,Status Status,RecordMetadata RecordMetadata){
        throw new NotSupportedException("Read非同期で呼ばれる");
    }
#endif
#if TSAVORITE2_0BETA3
	public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
		where TSourceLogRecord : ISourceLogRecord {
		return new() { KeySize=srcLogRecord.Key.Length,ValueSize=input.Length };
	}
#elif TSAVORITE1_1_1
	public int GetRMWModifiedValueLength(ref SpanByte Value,ref SpanByte Input){
        return Value.Length+4;
    }
#endif
#if TSAVORITE2_0BETA3
	public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
		where TKey : IKey, allows ref struct {
		return new() { KeySize=key.KeyBytes.Length,ValueSize=input.Length };
	}
#elif TSAVORITE1_1_1
	public int GetRMWInitialValueLength(ref SpanByte Input){
        return Input.Length+4;
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
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte Value,ref SpanByte Input){
        throw new NotSupportedException("Upsert");
    }
#endif
#if TSAVORITE2_0BETA3
	public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){
	}
#elif TSAVORITE1_1_1
    public void ConvertOutputToHeap(ref SpanByte Input,ref SpanByteAndMemory Output){
    }
#endif
}
