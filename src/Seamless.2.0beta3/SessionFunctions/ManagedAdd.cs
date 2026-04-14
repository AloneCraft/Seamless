namespace Seamless.SessionFunctions;

using System.Diagnostics.CodeAnalysis;
/// <summary>
/// ManagedCollection 用セッション関数。ManagedAdd は RMW と Upsert の両方で使われる。RMWではAddセマンティクス（キー既存時は拒否）。
/// </summary>
public readonly partial struct ManagedAdd:
    ISessionFunctions<ManagedSessionKey,bool,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref ManagedSessionKey input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("ManagedAddクラスのRMWでのみ使われる");
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref ManagedSessionKey input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref ManagedSessionKey input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
    }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
    }
    /// <summary>
    /// キー未存在時は新規作成する
    /// </summary>
    public bool NeedInitialUpdate<TKey>(TKey key,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo)
        where TKey : IKey, allows ref struct {
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
    }
    /// <summary>
    /// キー既存時は InPlaceUpdater=false で更新を拒否（output は default(false) のまま＝失敗）
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo) {
        return false;
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo) {
        output=true;
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false で更新を拒否（output は default(false) のまま＝失敗）
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("NeedCopyUpdateがfalseを返すので呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref ManagedSessionKey input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("NeedCopyUpdateがfalseを返すので呼ばれない");
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref ManagedSessionKey input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref ManagedSessionKey input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("ManagedAddクラスのRMWでのみ使われる");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("ManagedAddクラスのRMWでのみ使われる");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("ManagedAddクラスのRMWでのみ使われる");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("ManagedAddクラスのRMWでのみ使われる");
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref ManagedSessionKey input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref ManagedSessionKey input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.KeyBytes.Length,ValueSize=input.ReadOnlySpan.Length };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref ManagedSessionKey input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=input.ReadOnlySpan.Length };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref ManagedSessionKey input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref ManagedSessionKey input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref ManagedSessionKey input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.Info.ValueIsObject ? ObjectIdMap.ObjectIdSize : inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.Info.ValueIsObject };
    }
    public void ConvertOutputToHeap(ref ManagedSessionKey input,ref bool output) {
    }
}
