namespace Tsavorite.SessionFunctions;

/// <summary>
/// IntersectWith 逕ｨ縺ｮ RMW 繧ｻ繝・す繝ｧ繝ｳ髢｢謨ｰ縲・
/// <list type="bullet">
///   <item><see cref="Collections.HashSet{TKey}.IntersectWith"/> 竊・RMW</item>
///   <item><see cref="Collections.HashSet{TKey}.IntersectWith"/> 竊・RMW</item>
/// </list>
/// RMW 縺ｧ譌｢蟄倥く繝ｼ繧・ExpireAndStop・郁ｫ也炊蜑企勁・峨＠縲∵悴蟄伜惠繧ｭ繝ｼ縺ｯ NeedInitialUpdate=false 縺ｧ辟｡隕悶☆繧九・
/// Read / Upsert / Delete 縺ｯ菴ｿ逕ｨ縺励↑縺・・
/// </summary>
internal readonly struct IntersectWith:
    ISessionFunctions<EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    // 笏笏 Read・域悴菴ｿ逕ｨ・俄楳笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Read 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // ReadCompletionCallback 縺ｯ髱槫酔譛・Read 譎ゅ・縺ｿ蜻ｼ縺ｰ繧後ｋ縲３MW 縺ｮ縺ｿ菴ｿ逕ｨ縺吶ｋ縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }

    // 笏笏 Upsert・域悴菴ｿ逕ｨ・俄楳笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
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
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException();
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

    // 笏笏 Delete・域悴菴ｿ逕ｨ・俄楳笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
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

    // 笏笏 RMW・・ntersectWith 譛ｬ菴難ｼ俄楳笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // 繧ｭ繝ｼ縺悟ｭ伜惠縺励↑縺・ｴ蜷医・譁ｰ隕丈ｽ懈・縺励↑縺・ｼ・OTFOUND 繧定ｿ斐＆縺帙ｋ・峨・
        // Tsavorite 縺ｯ doingCU=false 縺ｮ縺ｨ縺搾ｼ井ｾ具ｼ啜ombstone繝ｬ繧ｳ繝ｼ繝牙ｮ御ｺ・ｾ鯉ｼ峨％縺ｮ繝｡繧ｽ繝・ラ繧貞他縺ｶ縲・
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        // IntersectWith 縺ｧ縺ｯ RMW 縺ｧ譁ｰ隕上Ξ繧ｳ繝ｼ繝峨ｒ菴懈・縺励↑縺・・eedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        // IntersectWith 縺ｧ縺ｯ RMW 縺ｧ譁ｰ隕上Ξ繧ｳ繝ｼ繝峨ｒ菴懈・縺励↑縺・・eedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    public bool InPlaceUpdater(ref LogRecord logRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
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
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // IterateLookup 縺ｯ繝・ぅ繧ｹ繧ｯ荳翫Ξ繧ｳ繝ｼ繝峨ｂ霑斐☆縺溘ａ縲ヽMW 縺・IsPending 縺ｫ縺ｪ繧九％縺ｨ縺後≠繧九・
        // 縺昴・螳御ｺ・凾縺ｫ蜻ｼ縺ｰ繧後ｋ縲・qualityEmpty 縺ｫ縺ｯ蠕悟・逅・ｸ崎ｦ√・
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=0 };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // IntersectWith 縺ｧ縺ｯ RMW 縺ｧ譁ｰ隕上Ξ繧ｳ繝ｼ繝峨ｒ菴懈・縺励↑縺・・eedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }

    // 笏笏 縺昴・莉・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
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
        // IterateLookup 縺ｯ繝・ぅ繧ｹ繧ｯ荳翫Ξ繧ｳ繝ｼ繝峨ｂ霑斐☆縺溘ａ縲ヽMW 縺・IsPending 縺ｫ縺ｪ繧九％縺ｨ縺後≠繧九・
        // EqualityEmpty 縺ｯ繝偵・繝励ョ繝ｼ繧ｿ繧呈戟縺溘↑縺・◆繧∝､画鋤荳崎ｦ√・
    }
}
