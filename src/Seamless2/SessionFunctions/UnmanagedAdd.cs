using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

/// <summary>
/// UnmanagedCollection 用セッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly  struct UnmanagedAdd<TKey, TValue>():
    ISessionFunctions<TValue,bool,EqualityEmpty>,
    ICreate<UnmanagedAdd<TKey,TValue>>
    where TKey:unmanaged, IEquatable<TKey>
    where TValue:unmanaged, IEquatable<TValue> {
    public static UnmanagedAdd<TKey,TValue> Create() => new();
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord:ISourceLogRecord {
        // UnmanagedAdd は RMW でのみ使用するため、他のコールバックには到達しない。
        throw new NotSupportedException();
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo){
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo){
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo){
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo){
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public void PostUpsertOperation<TKey0, TEpochAccessor>(TKey0 key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor {
    }
    public void PostUpsertOperation<TKey0, TEpochAccessor>(TKey0 key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor {
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord {
    }
    /// <summary>
    /// NeedInitialUpdate が true で、キー未存在時は InitialUpdater で値を書き込み
    /// </summary>
    public bool NeedInitialUpdate<TKey0>(TKey0 key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo){
        var inputSpan=MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TValue,byte>(ref input),Unsafe.SizeOf<TValue>());
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(inputSpan,in sizeInfo);
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo){
        return false;
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo){
        output=true;
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord {
        return false;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord {
        // NeedCopyUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord {
        // NeedCopyUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey0, TEpochAccessor>(TKey0 key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    public void PostDeleteOperation<TKey0, TEpochAccessor>(TKey0 key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord:ISourceLogRecord {
        return new(){ KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey0>(TKey0 key,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){ KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,ReadOnlySpan<byte> value,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){ KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,IHeapObject value,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){ KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>(),ValueIsObject=true };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0, TSourceLogRecord>(TKey0 key,in TSourceLogRecord inputLogRecord,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
        #else
        where TKey0 : IKey
        #endif
        where TSourceLogRecord:ISourceLogRecord {
        return new(){ KeySize=Unsafe.SizeOf<TKey>(),ValueSize=inputLogRecord.Info.ValueIsObject ? ObjectIdMap.ObjectIdSize:Unsafe.SizeOf<TValue>(),ValueIsObject=inputLogRecord.Info.ValueIsObject };
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output){
    }
}
