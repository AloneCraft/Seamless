using Seamless.Collections.Tsavorite;
namespace Seamless.Collections.Tsavorite.SessionFunctions;
/// <summary>
/// RMW で Key,Value 両方が一致するエントリのみ削除するセチE��ョン関数、E
/// output=true で削除成功を通知する、E
/// 冁E��でログの値をデシリアライズし、input�E�EValue�E�と Equals 比輁E��る、E
/// </summary>
public readonly struct Remove_KeyValuePair<TValue, TStrategy>:
    ISessionFunctions<TValue,bool,EqualityEmpty>    where TStrategy : ISerializer<TValue> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo) where TSourceLogRecord : ISourceLogRecord =>
        // Read 操作�E使用しなぁE��E
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
        // NeedInitialUpdate ぁEfalse を返すため到達しなぁE��E
        throw new NotSupportedException();
    }
    // キー存在時！Eutable�E��E Value が一致する場合�Eみ ExpireAndStop で論理削除
    public bool InPlaceUpdater(ref LogRecord logRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        var storedValue = TStrategy.Deserialize(logRecord.ValueSpan);
        if(storedValue.Equals(input)) {
            output=true;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate ぁEfalse のため InitialUpdater も呼ばれず、到達しなぁE��E
        throw new NotSupportedException();
    }
    // キー存在時！EeadOnly/Disk�E��E値が一致した場合�Eみ Mutable に削除レコードを追加する
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TSourceLogRecord : ISourceLogRecord {
        var storedValue = TStrategy.Deserialize(srcLogRecord.ValueSpan);
        return storedValue.Equals(input);
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TSourceLogRecord : ISourceLogRecord {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TSourceLogRecord : ISourceLogRecord {
        // CopyUpdater ぁEfalse を返して処琁E��打ち刁E��ため、到達しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作�E使用しなぁE��E
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
        // Upsert 操作�E使用しなぁE��E
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
        // Upsert 操作�E使用しなぁE��E
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
        // Read 操作�E使用しなぁE��E
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
        // NeedInitialUpdate ぁEfalse を返すため到達しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // Upsert 操作�E使用しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // Upsert 操作�E使用しなぁE��E
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
        // Upsert 操作�E使用しなぁE��E
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
    public void ConvertOutputToHeap(ref TValue input,ref bool output) {
    }
}
