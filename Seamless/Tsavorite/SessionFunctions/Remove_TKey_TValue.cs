using Seamless.Tsavorite.Collections;
namespace Seamless.Tsavorite.SessionFunctions;

/// <summary>
/// Dictionary 逕ｨ縺ｮ Remove(key, out value) 繧ｻ繝・・ｽ・ｽ繝ｧ繝ｳ髢｢謨ｰ縲・
/// 蜿門ｾ励＠縺溷､縺ｯ session 蜀・・ｽ・ｽ TValue 縺ｫ繝・・ｽ・ｽ繝ｪ繧｢繝ｩ繧､繧ｺ縺励※霑斐☆縲・
/// </summary>
public readonly struct Remove_TKey_TValue<TValue, TStrategy>:
    ISessionFunctions<EqualityEmpty,TValue,EqualityEmpty>    where TStrategy : ISerializer<TValue> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Read 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    public bool InPlaceUpdater(ref LogRecord logRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        output=TStrategy.Deserialize(logRecord.ValueSpan);
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate 縺・false 縺ｮ縺溘ａ InitialUpdater 繧ょ他縺ｰ繧後★縲∝芦驕斐＠縺ｪ縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        output=TStrategy.Deserialize(srcLogRecord.ValueSpan);
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // CopyUpdater 縺・false 繧定ｿ斐＠縺ｦ蜃ｦ逅・・ｽ・ｽ謇薙■蛻・・ｽ・ｽ縺溘ａ縲∝芦驕斐＠縺ｪ縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        if(!status.NotFound)
            output=TStrategy.Deserialize(diskLogRecord.ValueSpan);
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // Read 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=srcLogRecord.ValueSpan.Length };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // NeedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
        #else
        where TKey : IKey
        #endif
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懶ｿｽE菴ｿ逕ｨ縺励↑縺・・ｽ・ｽE
    throw new NotSupportedException();
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
