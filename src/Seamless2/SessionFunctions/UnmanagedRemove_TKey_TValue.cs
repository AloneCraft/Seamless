using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.SessionFunctions;

/// <summary>
/// UnmanagedDictionary 用の Remove(key, out value) セッション関数。
/// 取得した値をそのまま TValue に復元して返す。
/// </summary>
public readonly struct UnmanagedRemove_TKey_TValue<TKey, TValue>:
    ISessionFunctions<EqualityEmpty, TValue, EqualityEmpty>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    [ExcludeFromCodeCoverage]
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    public bool NeedInitialUpdate<TKey0>(TKey0 key,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        output=MemoryMarshal.Read<TValue>(logRecord.ValueSpan);
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate が false のため InitialUpdater も呼ばれず、到達しない。
        throw new NotSupportedException();
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return true;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        output=MemoryMarshal.Read<TValue>(srcLogRecord.ValueSpan);
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey0, TEpochAccessor>(TKey0 key,ref EqualityEmpty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey0, TEpochAccessor>(TKey0 key,ref EqualityEmpty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
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
    public void PostDeleteOperation<TKey0, TEpochAccessor>(TKey0 key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey0, TEpochAccessor>(TKey0 key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        if(!status.NotFound)
            output=MemoryMarshal.Read<TValue>(diskLogRecord.ValueSpan);
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=Unsafe.SizeOf<TKey>(), ValueSize=Unsafe.SizeOf<TValue>() };
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey0>(TKey0 key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,ReadOnlySpan<byte> value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey0, TSourceLogRecord>(TKey0 key,in TSourceLogRecord inputLogRecord,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
        #else
        where TKey0 : IKey
        #endif
        where TSourceLogRecord : ISourceLogRecord {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref TValue output) {
    }
}