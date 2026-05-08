using Seamless.Collections;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

using System.Diagnostics.CodeAnalysis;
/// <summary>
/// Collection 用セチE��ョン関数、EanagedRead は Read でのみ使われる、E
/// Reader 冁E��ログメモリから直接 TValue へチE��リアライズ�E�ゼロコピ�Eリード）、E
/// </summary>
public readonly struct Read<TValue, TStrategy>():
    ISessionFunctions<EqualityEmpty,TValue,EqualityEmpty>,
    ICreate<Read<TValue,TStrategy>>    where TStrategy : ISerializer<TValue> {
    public static Read<TValue,TStrategy> Create() => new();
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static void ThrowReadOnlyEntryPoint() {
        // Read は Read にだけバインドされる。書き込み・更新・削除のコールバックには到達しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowReadOnlyEntryPoint<T>() {
        // Read は Read にだけバインドされる。書き込み・更新・削除のコールバックには到達しなぁE��E
        throw new NotSupportedException();
    }
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        // ログメモリから直接チE��リアライズ。コピ�Eなし、E
        output=TStrategy.Deserialize(srcLogRecord.ValueSpan);
        return true;
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedInitialUpdate<TKey>(TKey key,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        ThrowReadOnlyEntryPoint();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadOnlyEntryPoint<bool>();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
    #else
        where TKey : IKey
    #endif
        where TEpochAccessor : IEpochAccessor {
        ThrowReadOnlyEntryPoint();
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        if(status.Found)
            output=TStrategy.Deserialize(diskLogRecord.ValueSpan);
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    [ExcludeFromCodeCoverage]
    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
        #else
        where TKey : IKey
        #endif
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadOnlyEntryPoint<RecordFieldInfo>();
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref TValue output) {
    }
}
