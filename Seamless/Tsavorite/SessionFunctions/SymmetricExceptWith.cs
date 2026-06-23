namespace Seamless.Tsavorite.SessionFunctions;

internal readonly struct SymmetricExceptWith:
    ISessionFunctions<EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Read 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
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
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
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
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return true;// 繧ｭ繝ｼ譛ｪ蟄伜惠譎ゅ・譁ｰ隕丈ｽ懈・縺吶ｋ
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        //NeedInitialUpdate縺荊rue繧定ｿ斐☆縺ｨ縺阪↓蜻ｼ縺ｰ繧後ｋ
    return dstLogRecord.TrySetValueSpanAndPrepareOptionals(ReadOnlySpan<byte>.Empty,in sizeInfo);
    }
    public bool InPlaceUpdater(ref LogRecord logRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;//繧ｭ繝ｼ縺悟ｭ伜惠縺吶ｋ蝣ｴ蜷医・蜑企勁縺吶ｋ縲４ymmetricExceptWith縺ｪ縺ｮ縺ｧ縲∵峩譁ｰ縺ｯ縺帙★縺ｫ蜑企勁縺ｮ縺ｿ陦後≧縲・
    return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // CopyUpdater 縺・false 繧定ｿ斐＠縺ｦ蜃ｦ逅・ｒ謇薙■蛻・ｋ縺溘ａ縲∝芦驕斐＠縺ｪ縺・・
    throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        //RMW縺梧・蜉溘＠縺ｦ繧ょ､ｱ謨励＠縺ｦ繧ゅ∽ｸ｡譁ｹ縺ｮ繧ｱ繝ｼ繧ｹ縺ｧ蜻ｼ縺ｰ繧後ｋ縲４ymmetricExceptWith 縺ｧ縺ｯ RMW 縺ｮ邨先棡縺ｫ髢｢繧上ｉ縺壻ｽ輔ｂ縺励↑縺・・
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        //CompletePendingWithOutputs
    }
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
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
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // ReadCompletionCallback 縺ｯ髱槫酔譛・Read 譎ゅ・縺ｿ蜻ｼ縺ｰ繧後ｋ縲３MW 縺ｮ縺ｿ菴ｿ逕ｨ縺吶ｋ縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
    throw new NotSupportedException();
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=Unsafe.SizeOf<EqualityEmpty>() };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<EqualityEmpty>() };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
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
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
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
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref EqualityEmpty output) {
    }
}
