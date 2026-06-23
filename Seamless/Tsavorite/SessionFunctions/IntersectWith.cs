namespace Seamless.Tsavorite.SessionFunctions;

/// <summary>
/// IntersectWith 逕ｨ縺�E� RMW 繧�E�繝�Eす繝ｧ繝ｳ髢�E�謨�E�縲・
/// <list type="bullet">
///   <item><see cref="Collections.HashSet{TKey}.IntersectWith"/> 竊�ERMW</item>
///   <item><see cref="Collections.HashSet{TKey}.IntersectWith"/> 竊�ERMW</item>
/// </list>
/// RMW 縺�E�譌｢蟁E��く繝ｼ繧・ExpireAndStop・郁E��也炊蜑企勁・峨�E�縲∵悴蟁E��惠繧�E�繝ｼ縺�E� NeedInitialUpdate=false 縺�E�辟｡隕悶☁E��九・
/// Read / Upsert / Delete 縺�E�菴�E�逕ｨ縺励↑縺・・
/// </summary>
internal readonly struct IntersectWith:
    ISessionFunctions<EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    // 笏笏 Read・域悴菴�E�逕ｨ・俁E��笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Read 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // ReadCompletionCallback 縺�E�髱槫酔譛�ERead 譎ゅ・縺�E�蜻�E�縺�E�繧後ｋ縲�E�MW 縺�E�縺�E�菴�E�逕ｨ縺吶�E�縺溘ａ蛻�E�驕斐�E�縺�E�縺・・
    throw new NotSupportedException();
    }

    // 笏笏 Upsert・域悴菴�E�逕ｨ・俁E��笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
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
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
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
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
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
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
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
        // Upsert 謫堺�E�懊�E菴�E�逕ｨ縺励↑縺・・
    throw new NotSupportedException();
    }

    // 笏笏 Delete・域悴菴�E�逕ｨ・俁E��笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API 縺�E�菴�E�逕ｨ縺励↑縺・�E�MW 縺�E�繧医�E�ETombstone 謖ｿ蜈･縺�E�蜑企勁縺吶�E�縲・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API 縺�E�菴�E�逕ｨ縺励↑縺・�E�MW 縺�E�繧医�E�ETombstone 謖ｿ蜈･縺�E�蜑企勁縺吶�E�縲・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API 縺�E�菴�E�逕ｨ縺励↑縺・�E�MW 縺�E�繧医�E�ETombstone 謖ｿ蜈･縺�E�蜑企勁縺吶�E�縲・
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
        // Tsavorite Delete API 縺�E�菴�E�逕ｨ縺励↑縺・�E�MW 縺�E�繧医�E�ETombstone 謖ｿ蜈･縺�E�蜑企勁縺吶�E�縲・
    throw new NotSupportedException();
    }

    // 笏笏 RMW・・ntersectWith 譛ｬ菴難�E�俁E��笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        // 繧�E�繝ｼ縺悟ｭ伜惠縺励↑縺・�E��E�蜷医・譁E��隕丈ｽ懈�E縺励↑縺・�E�・OTFOUND 繧定ｿ斐！E��帙ｋ�E峨・
        // Tsavorite 縺�E� doingCU=false 縺�E�縺�E�縺搾�E�井ｾ具�E�啜ombstone繝ｬ繧�E�繝ｼ繝牙�E�御�E�・�E�鯉ｼ峨�E�E���E�繝｡繧�E�繝�Eラ繧貞他縺�E�縲・
    return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        // IntersectWith 縺�E�縺�E� RMW 縺�E�譁E��隕上Ξ繧�E�繝ｼ繝峨�E�菴懈�E縺励↑縺・・eedInitialUpdate 縺・false 繧定ｿ斐�E縺溘ａ蛻�E�驕斐�E�縺�E�縺・・
    throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        // IntersectWith 縺�E�縺�E� RMW 縺�E�譁E��隕上Ξ繧�E�繝ｼ繝峨�E�菴懈�E縺励↑縺・・eedInitialUpdate 縺・false 繧定ｿ斐�E縺溘ａ蛻�E�驕斐�E�縺�E�縺・・
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
        // CopyUpdater 縺・false 繧定ｿ斐＠縺�E�蜁E��送E�E�E�謁E��■蛻・�E�縺溘ａ縲∝芦驕斐�E�縺�E�縺・・
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
        // IterateLookup 縺�E�繝�EぁE���E�繧�E�荳翫Ξ繧�E�繝ｼ繝峨�E�霑斐�E縺溘ａ縲ヽMW 縺・IsPending 縺�E�縺�E�繧九！E���E�縺後≠繧九・
        // 縺昴・螳御�E�・凾縺�E�蜻�E�縺�E�繧後ｋ縲・qualityEmpty 縺�E�縺�E�蠕悟・送E�E�E�崎ｦ√・
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
        // IntersectWith 縺�E�縺�E� RMW 縺�E�譁E��隕上Ξ繧�E�繝ｼ繝峨�E�菴懈�E縺励↑縺・・eedInitialUpdate 縺・false 繧定ｿ斐�E縺溘ａ蛻�E�驕斐�E�縺�E�縺・・
    throw new NotSupportedException();
    }

    // 笏笏 縺昴・莉�E笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
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
        // IterateLookup 縺�E�繝�EぁE���E�繧�E�荳翫Ξ繧�E�繝ｼ繝峨�E�霑斐�E縺溘ａ縲ヽMW 縺・IsPending 縺�E�縺�E�繧九！E���E�縺後≠繧九・
        // EqualityEmpty 縺�E�繝�E・繝励ョ繝ｼ繧�E�繧呈戟縺溘�E縺・◁E��∝､画鋤荳崎ｦ√・
    }
}
