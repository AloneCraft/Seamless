using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

/// <summary>
/// UnmanagedCollection 逕ｨ繧ｻ繝・す繝ｧ繝ｳ髢｢謨ｰ縲・dd 縺ｯ Upsert 縺ｨ RMW 縺ｮ荳｡譁ｹ縺ｧ蜻ｼ縺ｰ繧後ｋ縺溘ａ縲∽ｸ｡譁ｹ縺ｮ蜃ｦ逅・ｒ螳溯｣・☆繧句ｿ・ｦ√′縺ゅｋ縲・
/// </summary>
public readonly  struct UnmanagedRead<TKey,TValue>:
    ISessionFunctions<EqualityEmpty,TValue,EqualityEmpty>,
    ICreate<UnmanagedRead<TKey,TValue>>
    where TKey:unmanaged
    where TValue:unmanaged{
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
    /// NeedInitialUpdate 縺・true 縺ｧ縲√く繝ｼ譛ｪ蟄伜惠譎ゅ・ InitialUpdater 縺ｧ蛟､繧呈嶌縺崎ｾｼ縺ｿ
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
    /// 繧ｭ繝ｼ譌｢蟄俶凾縺ｯ NeedCopyUpdate=false 縺九▽ InPlaceUpdater=false 縺ｧ譖ｴ譁ｰ繧呈拠蜷ｦ
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo){
        return false;
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo){
    }
    /// <summary>
    /// 繧ｭ繝ｼ譌｢蟄俶凾縺ｯ NeedCopyUpdate=false 縺九▽ InPlaceUpdater=false 縺ｧ譖ｴ譁ｰ繧呈拠蜷ｦ
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord{
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord{
        // NeedCopyUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord{
        // NeedCopyUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
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
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
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
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
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
