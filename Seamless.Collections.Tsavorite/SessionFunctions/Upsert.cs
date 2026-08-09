namespace Seamless.Collections.Tsavorite;

using global::System.Diagnostics.CodeAnalysis;

/// <summary>
/// Collection用のセッション関数。ManagedAddはRMWとUpsertの両方で使用され、RMWではAddと同じ意味論でキー既存時の追加を拒否する。
/// 値はStrategy側でシリアライズするため、呼び出し側でPinnedSpanByteやGCHandleを固定する必要はない。
/// </summary>
public readonly struct Upsert<TValue, TStrategy>:
    ISessionFunctions<TValue,bool,EqualityEmpty>,
    ICreate<Upsert<TValue,TStrategy>> where TStrategy : ISerializer<TValue> {
    public static Upsert<TValue,TStrategy> Create() => new();
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static void ThrowReadDeleteEntryPoint() {
        // UpsertはRMW/Upsertにだけバインドされるため、直接Read/Deleteのコールバックには到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowReadDeleteEntryPoint<T>() {
        // UpsertはRMW/Upsertにだけバインドされるため、直接Read/Deleteのコールバックには到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowCopyUpdateEntryPoint<T>() {
        // NeedCopyUpdateが常にfalseのため、CopyUpdater系には到達しない。
        throw new NotSupportedException();
    }
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadDeleteEntryPoint<bool>();
    }

    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        ThrowReadDeleteEntryPoint();
    }

    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }

    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }

    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }

    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
    }

    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
    }

    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
    }

    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        RecordSizeInfo sizeInfo = new() { FieldInfo=this.GetUpsertFieldInfo(logRecord,srcValue,ref input) };
        logRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }

    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        RecordSizeInfo sizeInfo = new() { FieldInfo=this.GetUpsertFieldInfo(logRecord,srcValue,ref input) };
        logRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }

    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        RecordSizeInfo sizeInfo = new() { FieldInfo=this.GetUpsertFieldInfo(dstLogRecord,in inputLogRecord,ref input) };
        dstLogRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }

    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)

#if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

#else

        where TKey : IKey

#endif

        where TEpochAccessor : IEpochAccessor {
    }

    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)

#if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

#else

        where TKey : IKey

#endif

        where TEpochAccessor : IEpochAccessor {
    }

    /// <summary>
    /// キーが存在しない場合は新規作成する。
    /// </summary>
    public bool NeedInitialUpdate<TKey>(TKey key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        return true;
    }

    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        // TValueをStrategy側でシリアライズしてログレコードに書き込む。
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(TStrategy.Serialize(ref input),in sizeInfo);
    }

    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        output=true;
    }

    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return false;
    }

    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowCopyUpdateEntryPoint<bool>();
    }

    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowCopyUpdateEntryPoint<bool>();
    }

    /// <summary>
    /// キーが存在する場合はInPlaceUpdater=falseで更新を拒否し、outputはdefault(false)のまま失敗を示す。
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        return false;
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

    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadDeleteEntryPoint<bool>();
    }

    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        ThrowReadDeleteEntryPoint();
    }

    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadDeleteEntryPoint<bool>();
    }

    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)

#if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

#else

        where TKey : IKey

#endif

        where TEpochAccessor : IEpochAccessor {
        ThrowReadDeleteEntryPoint();
    }

    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.KeyBytes.Length,ValueSize=TStrategy.Serialize(ref input).Length };
    }

    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=TStrategy.Serialize(ref input).Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref TValue input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
#else
        where TKey : IKey
#endif
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.DataHeader.ValueIsObject ? ObjectIdMap.ObjectIdSize : inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.DataHeader.ValueIsObject };
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
