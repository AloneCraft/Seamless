using System.Diagnostics.CodeAnalysis;
using Seamless.Collections;
namespace Seamless.SessionFunctions;
/// <summary>
/// RMW 縺ｧ Key,Value 荳｡譁ｹ縺御ｸ閾ｴ縺吶ｋ繧ｨ繝ｳ繝医Μ縺ｮ縺ｿ蜑企勁縺吶ｋ繧ｻ繝・す繝ｧ繝ｳ髢｢謨ｰ縲・
/// output=true 縺ｧ蜑企勁謌仙粥繧帝夂衍縺吶ｋ縲・
/// 蜀・Κ縺ｧ繝ｭ繧ｰ縺ｮ蛟､繧偵ョ繧ｷ繝ｪ繧｢繝ｩ繧､繧ｺ縺励（nput・・Value・峨→ Equals 豈碑ｼ・☆繧九・
/// </summary>
public readonly struct Remove_KeyValuePair<TValue, TValueSerializer>:
    ISessionFunctions<TValue,bool,EqualityEmpty>    where TValueSerializer : ISerializer<TValue> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo) where TSourceLogRecord : ISourceLogRecord =>
        // Read 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    public bool NeedInitialUpdate<TKey>(TKey key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey {
#endif
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    // 繧ｭ繝ｼ蟄伜惠譎ゑｼ・utable・峨・ Value 縺御ｸ閾ｴ縺吶ｋ蝣ｴ蜷医・縺ｿ ExpireAndStop 縺ｧ隲也炊蜑企勁
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        var storedValue = TValueSerializer.Deserialize(logRecord.ValueSpan);
        if(storedValue.Equals(input)) {
            output=true;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate 縺・false 縺ｮ縺溘ａ InitialUpdater 繧ょ他縺ｰ繧後★縲∝芦驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    // 繧ｭ繝ｼ蟄伜惠譎ゑｼ・eadOnly/Disk・峨・蛟､縺御ｸ閾ｴ縺励◆蝣ｴ蜷医・縺ｿ Mutable 縺ｫ蜑企勁繝ｬ繧ｳ繝ｼ繝峨ｒ霑ｽ蜉縺吶ｋ
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TSourceLogRecord : ISourceLogRecord {
        var storedValue = TValueSerializer.Deserialize(srcLogRecord.ValueSpan);
        return storedValue.Equals(input);
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TSourceLogRecord : ISourceLogRecord {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TSourceLogRecord : ISourceLogRecord {
        // CopyUpdater 縺・false 繧定ｿ斐＠縺ｦ蜃ｦ逅・ｒ謇薙■蛻・ｋ縺溘ａ縲∝芦驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
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
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
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
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // Read 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=srcLogRecord.ValueSpan.Length };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // NeedInitialUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 謫堺ｽ懊・菴ｿ逕ｨ縺励↑縺・・
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output) {
    }
}
