using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

/// <summary>
/// IntersectWith 用の RMW セッション関数。
/// <list type="bullet">
///   <item><see cref="Collections.ManagedHashSet{TKey}.IntersectWith"/> → RMW</item>
///   <item><see cref="Collections.UnmanagedHashSet{TKey}.IntersectWith"/> → RMW</item>
/// </list>
/// RMW で既存キーを ExpireAndStop（論理削除）し、未存在キーは NeedInitialUpdate=false で無視する。
/// Read / Upsert / Delete は使用しない。
/// </summary>
internal readonly struct IntersectWith:
    ISessionFunctions<EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    // ── Read（未使用）────────────────────────────────────────────
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException("Read非同期で呼ばれる");
    }

    // ── Upsert（未使用）──────────────────────────────────────────
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Upsert");
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
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref EqualityEmpty input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }

    // ── Delete（未使用）──────────────────────────────────────────
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Delete");
    }

    // ── RMW（IntersectWith 本体）──────────────────────────────────
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        where TKey : IKey, allows ref struct {
        // キーが存在しない場合は新規作成しない（NOTFOUND を返させる）。
        // Tsavorite は doingCU=false のとき（例：Tombstoneレコード完了後）このメソッドを呼ぶ。
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはない。");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはない。");
    }
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
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
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // IterateLookup はディスク上レコードも返すため、RMW が IsPending になることがある。
        // その完了時に呼ばれる。EqualityEmpty には後処理不要。
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=0 };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはない。");
    }

    // ── その他 ───────────────────────────────────────────────────
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref EqualityEmpty output) {
        // IterateLookup はディスク上レコードも返すため、RMW が IsPending になることがある。
        // EqualityEmpty はヒープデータを持たないため変換不要。
    }
}
