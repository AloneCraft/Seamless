namespace Seamless.SessionFunctions;
using System.Diagnostics.CodeAnalysis;

using Tsavorite.core;
public struct Delete:
#if TSAVORITE2_0BETA3
    ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Collections.Empty>
#elif TSAVORITE1_1_1
    ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Seamless.Collections.Empty>
#endif
    { 
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Readで呼ばれる");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo)    {
        throw new NotSupportedException("Readで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Readで呼ばれる");
    }

#endif
#if TSAVORITE2_0BETA3
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input){
        throw new NotSupportedException("Upsertで呼ばれる");
    }
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    //↓SingleWriter
    [ExcludeFromCodeCoverage]public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    //↓ConcurrentWriter
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsertで呼ばれる");
    }
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }
#endif
#if TSAVORITE2_0BETA3
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
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason) { 
        throw new NotSupportedException("RMWは呼ばれない");
    }
#endif
#if TSAVORITE2_0BETA3
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
#elif TSAVORITE1_1_1
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{
        throw new NotSupportedException("RMWは呼ばれない");
    }
#endif
#if TSAVORITE2_0BETA3
    public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TKey : IKey, allows ref struct {
        return false;// キー未存在時は新規作成しない
    }
#elif TSAVORITE1_1_1
    public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;
    }
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
#elif TSAVORITE1_1_1
    public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
        //return false;
    }
#endif
#if TSAVORITE2_0BETA3
    // キー存在時は Tombstone を挿入して削除
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
#elif TSAVORITE1_1_1
    // キーが存在する場合は Tombstone を挿入して削除 → Status.Found
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
#elif TSAVORITE1_1_1
    public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { 
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }

#endif
#if TSAVORITE2_0BETA3
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
#elif TSAVORITE1_1_1
    public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return true;
    }
#endif
#if TSAVORITE2_0BETA3
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
#elif TSAVORITE1_1_1
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("CopyUpdaterでRMWAction.ExpireAndStop&&falseが返されるので呼ばれない");
    }
#elif TSAVORITE1_1_1
    public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterでRMWAction.ExpireAndStop&&falseが返されるので呼ばれない");
    }
#endif
#if TSAVORITE2_0BETA3
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
    }
#elif TSAVORITE1_1_1
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
    }
#endif
#if TSAVORITE2_0BETA3
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata) { 
        //pending完了時に呼ばれる
    }
#elif TSAVORITE1_1_1
    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata) { 
        //pending完了時に呼ばれる
    }
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) { 
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
        throw new NotSupportedException("RMWは呼ばれない");
    }
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata) { 
        throw new NotSupportedException("RMWは呼ばれない");
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
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
#elif TSAVORITE1_1_1
    public int GetRMWInitialValueLength(ref SpanByte input) => input.Length+4;
#endif
    [ExcludeFromCodeCoverage]
#if TSAVORITE2_0BETA3
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3
    [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWは呼ばれない");
    }
#elif TSAVORITE1_1_1
#endif
#if TSAVORITE2_0BETA3

    public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){
        //RMWでPending待ちで削除するとき呼ばれる
    }
#elif TSAVORITE1_1_1
    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { 
        //RMWでPending待ちで削除するとき呼ばれる
    }
#endif
}