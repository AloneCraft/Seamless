using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Stores;
namespace Seamless.Tsavorite.SessionFunctions;

using global::System.Diagnostics.CodeAnalysis;
/// <summary>
/// Collection 逕ｨ繧ｻ繝・・ｽ・ｽ繝ｧ繝ｳ髢｢謨ｰ縲・anagedRead 縺ｯ Read 縺ｧ縺ｮ縺ｿ菴ｿ繧上ｌ繧九・
/// Reader 蜀・・ｽ・ｽ繝ｭ繧ｰ繝｡繝｢繝ｪ縺九ｉ逶ｴ謗･ TValue 縺ｸ繝・・ｽ・ｽ繝ｪ繧｢繝ｩ繧､繧ｺ・ｽE・ｽ繧ｼ繝ｭ繧ｳ繝費ｿｽE繝ｪ繝ｼ繝会ｼ峨・
/// </summary>
public readonly struct Read<TValue, TStrategy>():
    ISessionFunctions<EqualityEmpty,TValue,EqualityEmpty>,
    ICreate<Read<TValue,TStrategy>>    where TStrategy : ISerializer<TValue> {
    public static Read<TValue,TStrategy> Create() => new();
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static void ThrowReadOnlyEntryPoint() {
        // Read 縺ｯ Read 縺ｫ縺縺代ヰ繧､繝ｳ繝峨＆繧後ｋ縲よ嶌縺崎ｾｼ縺ｿ繝ｻ譖ｴ譁ｰ繝ｻ蜑企勁縺ｮ繧ｳ繝ｼ繝ｫ繝舌ャ繧ｯ縺ｫ縺ｯ蛻ｰ驕斐＠縺ｪ縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowReadOnlyEntryPoint<T>() {
        // Read 縺ｯ Read 縺ｫ縺縺代ヰ繧､繝ｳ繝峨＆繧後ｋ縲よ嶌縺崎ｾｼ縺ｿ繝ｻ譖ｴ譁ｰ繝ｻ蜑企勁縺ｮ繧ｳ繝ｼ繝ｫ繝舌ャ繧ｯ縺ｫ縺ｯ蛻ｰ驕斐＠縺ｪ縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // 繝ｭ繧ｰ繝｡繝｢繝ｪ縺九ｉ逶ｴ謗･繝・・ｽ・ｽ繝ｪ繧｢繝ｩ繧､繧ｺ縲ゅさ繝費ｿｽE縺ｪ縺励・
    output=TStrategy.Deserialize(srcLogRecord.ValueSpan);
        return true;
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceUpdater(ref LogRecord logRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        if(status.Found)
            output=TStrategy.Deserialize(diskLogRecord.ValueSpan);
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
        #else
        where TKey : IKey
        #endif
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    public void BeforeConsistentReadCallback(long hash) {
    }
    public void AfterConsistentReadKeyCallback() {
    }
    public void BeforeConsistentReadKeyBatchCallback(ReadOnlySpan<PinnedSpanByte> parameters) {
    }
    public bool AfterConsistentReadKeyBatchCallback(int keyCount) {
        return true;
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref TValue output) {
    }
}
