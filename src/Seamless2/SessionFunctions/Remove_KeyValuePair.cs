using System.Diagnostics.CodeAnalysis;
using Seamless.Collections;
namespace Seamless.SessionFunctions;
/// <summary>
/// RMW で Key,Value 両方が一致するエントリのみ削除するセッション関数。
/// output=true で削除成功を通知する。
/// 内部でログの値をデシリアライズし、input（TValue）と Equals 比較する。
/// </summary>
public readonly struct Remove_KeyValuePair<TValue, TValueSerializer>:
    ISessionFunctions<TValue,bool,EqualityEmpty>
    where TValue : IEquatable<TValue>
    where TValueSerializer : ISerializer<TValue> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo) where TSourceLogRecord : ISourceLogRecord =>
        throw new NotSupportedException("Read");
    public bool NeedInitialUpdate<TKey>(TKey key,ref TValue input,ref bool output,ref RMWInfo rmwInfo) where TKey : IKey, allows ref struct {
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    // キー存在時（Mutable）は Value が一致する場合のみ ExpireAndStop で論理削除
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
        throw new NotSupportedException("InitialUpdaterが呼ばれないので呼ばれない");
    }
    // キー存在時（ReadOnly/Disk）は値が一致した場合のみ Mutable に削除レコードを追加する
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
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException("Upsert");
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
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor) where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor) where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException("Read");
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.Key.Length,ValueSize=srcLogRecord.ValueSpan.Length };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref TValue input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref TValue input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref TValue input)
        where TKey : IKey, allows ref struct {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref TValue input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("Upsert");
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output) {
    }
}
