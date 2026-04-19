using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct SymmetricExceptWith:
    ISessionFunctions<EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Read");
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
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref EqualityEmpty output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref EqualityEmpty output,ref UpsertInfo upsertInfo)
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
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)
        where TKey : IKey, allows ref struct {
        return true;// キー未存在時は新規作成する
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        //NeedInitialUpdateがtrueを返すときに呼ばれる
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(ReadOnlySpan<byte>.Empty,in sizeInfo);
    }
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;//キーが存在する場合は削除する。SymmetricExceptWithなので、更新はせずに削除のみ行う。
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
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        //CompletePendingWithOutputs
    }
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
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException("Read非同期で呼ばれる");
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=0 };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=0 };
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
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref EqualityEmpty output) {
    }
}
