using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

/// <summary>
/// UnmanagedCollection 用セッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly  struct UnmanagedRead<TKey,TValue>:
    ISessionFunctions<EqualityEmpty,TValue,EqualityEmpty>,
    ICreate<UnmanagedRead<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>{
    public static UnmanagedRead<TKey,TValue> Create()=>new();
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref ReadInfo readInfo)
        where TSourceLogRecord:ISourceLogRecord{
        output=MemoryMarshal.Read<TValue>(srcLogRecord.ValueSpan);
        return true;
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo){
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo){
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo){
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo){
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref EqualityEmpty input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref EqualityEmpty input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ReadOnlySpan<byte> srcValue,ref TValue output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,IHeapObject srcValue,ref TValue output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,in TSourceLogRecord inputLogRecord,ref TValue output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
    }
    /// <summary>
    /// NeedInitialUpdate が true で、キー未存在時は InitialUpdater で値を書き込み
    /// </summary>
    public bool NeedInitialUpdate<TKey0>(TKey0 key,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo){
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(MemoryMarshal.AsBytes(new ReadOnlySpan<TValue>(in output)),in sizeInfo);
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo){
        return false;
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo){
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord{
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord{
        // NeedCopyUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord{
        // NeedCopyUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey0,TEpochAccessor>(TKey0 key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor{
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    [ExcludeFromCodeCoverage]
    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TKey0,TEpochAccessor>(TKey0 key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor{
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input)
        where TSourceLogRecord:ISourceLogRecord{
        return new(){KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>()};
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey0>(TKey0 key,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>()};
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,ReadOnlySpan<byte> value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>()};
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,IHeapObject value,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>(),ValueIsObject=true};
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0,TSourceLogRecord>(TKey0 key,in TSourceLogRecord inputLogRecord,ref EqualityEmpty input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
        #else
        where TKey0 : IKey
        #endif
        where TSourceLogRecord:ISourceLogRecord{
        return new(){KeySize=Unsafe.SizeOf<TKey>(),ValueSize=inputLogRecord.Info.ValueIsObject?ObjectIdMap.ObjectIdSize:Unsafe.SizeOf<TValue>(),ValueIsObject=inputLogRecord.Info.ValueIsObject};
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref TValue output){
    }
}
