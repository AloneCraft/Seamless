using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Seamless.SessionFunctions;

/// <summary>
/// UnmanagedCollection 用の削除セッション関数。RMW で Tombstone を挿入して論理削除する。
/// </summary>
public readonly struct UnmanagedDelete<TKey,TValue>:
    ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,core_Empty>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    [ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey0:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey0:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    public bool NeedInitialUpdate<TKey0>(TKey0 key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TKey0 : IKey, allows ref struct {
        return false;// キー未存在時は新規作成しない
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    // キー存在時は Tombstone を挿入して削除
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TKey0,TEpochAccessor>(TKey0 key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)where TKey0 : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,core_Empty ctx,Status status,RecordMetadata recordMetadata) {
    }
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete API は使用しない");
    }
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete API は使用しない");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete API は使用しない");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey0,TEpochAccessor>(TKey0 key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey0 : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Delete API は使用しない");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,core_Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Read は使用しない");
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey0>(TKey0 key,ref PinnedSpanByte input)
        where TKey0 : IKey, allows ref struct {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
        where TKey0 : IKey, allows ref struct {
        throw new NotSupportedException("Upsert は使用しない");
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,IHeapObject value,ref PinnedSpanByte input)
        where TKey0 : IKey, allows ref struct {
        throw new NotSupportedException("Upsert は使用しない");
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey0,TSourceLogRecord>(TKey0 key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
        where TKey0 : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert は使用しない");
    }
    public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){
        //RMWでPending待ちで削除するとき呼ばれる
    }
}
