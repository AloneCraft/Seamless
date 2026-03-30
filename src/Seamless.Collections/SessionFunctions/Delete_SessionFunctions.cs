namespace Seamless.Collections;
using System.Diagnostics.CodeAnalysis;

using Tsavorite.core;
public struct Delete_SessionFunctions:ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>{
    [ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓SingleWriter
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓SingleWriter
    [ExcludeFromCodeCoverage]public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓ConcurrentWriter
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓ConcurrentWriter
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓ConcurrentWriter
    [ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓PostSingleWriter
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓PostUpserOperation
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("RMWは呼ばれない");
    }
    //↓PostUpserOperation
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("RMWは呼ばれない");
    }
    public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TKey : IKey, allows ref struct {
        return false;// キー未存在時は新規作成しない
    }
    [ExcludeFromCodeCoverage]public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    // キー存在時は Tombstone を挿入して削除
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("CopyUpdaterでRMWAction.ExpireAndStop&&falseが返されるので呼ばれない");
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { 
        //pending完了時に呼ばれる
    }

    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) { 
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("RMWは呼ばれない");
    }


    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("RMWは呼ばれない");
    }


    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=input.Length };
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }


    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWは呼ばれない");
    }


    public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){
        //RMWでPending待ちで削除するとき呼ばれる
    }
}