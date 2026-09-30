namespace Seamless.Collections.Tsavorite;

/// <summary>
/// IntersectWithの動作を検証する。
/// <list type="bullet">
///   <item><see cref="HashSet{TKey}.IntersectWith"/> HashSetの動作を検証する。</item>
///   <item><see cref="HashSet{TKey}.IntersectWith"/> HashSetの動作を検証する。</item>
/// </list>
/// NeedInitialUpdate は false を返すため到達しない。
/// Upsert 経路はこの処理では使用しない。
/// </summary>
internal readonly struct IntersectWith:
    ISessionFunctions<EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    // Read 経路はこの処理では使用しない。
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Read 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // ReadCompletionCallback 経路はこの処理では使用しない。
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 経路はこの処理では使用しない。
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
        // Upsert 経路はこの処理では使用しない。
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
        // Upsert 経路はこの処理では使用しない。
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
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // Upsert 経路はこの処理では使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // Upsert 経路はこの処理では使用しない。
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
        // Upsert 経路はこの処理では使用しない。
        // IntersectWithの動作を検証する。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Delete API は使わず、RMW で Tombstone を挿入する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Delete API は使わず、RMW で Tombstone を挿入する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        // Delete API は使わず、RMW で Tombstone を挿入する。
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
        // Delete API は使わず、RMW で Tombstone を挿入する。
        // IntersectWithの動作を検証する。
        throw new NotSupportedException();
    }

    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // IntersectWithの動作を検証する。
        // IntersectWithの動作を検証する。
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        // IntersectWithの動作を検証する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        // IntersectWithの動作を検証する。
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
        // CopyUpdater は false を返すため到達しない。
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
        // IntersectWithの動作を検証する。
        // IntersectWithの動作を検証する。
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=0 };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
#if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct {
#else
        where TKey : IKey{
#endif
        // IntersectWithの動作を検証する。
        // IntersectWithの動作を検証する。
        throw new NotSupportedException();
    }

    public void PreSingleKeyConsistentRead(long hash) {
    }
    public void PostSingleKeyConsistentReadCallback() {
    }
    public void PreBatchKeyConsistentReadCallback(ReadOnlySpan<PinnedSpanByte> parameters) {
    }
    public bool PostBatchKeyConsistentReadCallback(int keyCount) {
        return true;
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref EqualityEmpty output) {
        // IntersectWithの動作を検証する。
        // IntersectWithの動作を検証する。
    }
}
