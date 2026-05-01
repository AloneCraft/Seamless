using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

/// <summary>
/// ManagedUnmanagedDictionary 逕ｨ繧ｻ繝・す繝ｧ繝ｳ髢｢謨ｰ縲・
/// 繧ｭ繝ｼ縺ｯSpanByte(managed)縲∝､縺ｯTValue縺ｮ逕溘ヰ繧､繝医〒譬ｼ邏阪☆繧九・
/// UnmanagedAdd 縺ｨ蜷後§蜍穂ｽ懊□縺・TKey:unmanaged 蛻ｶ邏・ｒ謖√◆縺ｪ縺・・
/// </summary>
public readonly struct ManagedUnmanagedAdd<TValue>:
    ISessionFunctions<TValue,bool,EqualityEmpty>,
    ICreate<ManagedUnmanagedAdd<TValue>>
    where TValue:unmanaged {
    public static ManagedUnmanagedAdd<TValue> Create() => new();
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord:ISourceLogRecord {
        // ManagedUnmanagedAdd 縺ｯ RMW 縺ｧ縺ｮ縺ｿ菴ｿ逕ｨ縺吶ｋ縺溘ａ縲∽ｻ悶・繧ｳ繝ｼ繝ｫ繝舌ャ繧ｯ縺ｫ縺ｯ蛻ｰ驕斐＠縺ｪ縺・・
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
    public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor {
    }
    public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
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
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo){
        return false;
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo){
        output=true;
    }
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord {
        return false;
    }
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord {
        // NeedCopyUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord:ISourceLogRecord {
        // NeedCopyUpdate 縺・false 繧定ｿ斐☆縺溘ａ蛻ｰ驕斐＠縺ｪ縺・・
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TKey0,TEpochAccessor>(TKey0 key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
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
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
        throw new NotSupportedException();
    }
    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
        throw new NotSupportedException();
    }
    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
        throw new NotSupportedException();
    }
    public void PostDeleteOperation<TKey0,TEpochAccessor>(TKey0 key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
    #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
    #else
        where TKey0 : IKey
    #endif
        where TEpochAccessor:IEpochAccessor {
        // Tsavorite Delete API 縺ｯ菴ｿ逕ｨ縺励↑縺・３MW 縺ｫ繧医ｋ Tombstone 謖ｿ蜈･縺ｧ蜑企勁縺吶ｋ縲・
        throw new NotSupportedException();
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord:ISourceLogRecord {
        return new(){ KeySize=srcLogRecord.KeyBytes.Length,ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey0>(TKey0 key,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){ KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,ReadOnlySpan<byte> value,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){ KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,IHeapObject value,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct{
        #else
        where TKey0 : IKey{
        #endif
        return new(){ KeySize=key.KeyBytes.Length,ValueSize=Unsafe.SizeOf<TValue>(),ValueIsObject=true };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0,TSourceLogRecord>(TKey0 key,in TSourceLogRecord inputLogRecord,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey0 : IKey, allows ref struct
        #else
        where TKey0 : IKey
        #endif
        where TSourceLogRecord:ISourceLogRecord {
        return new(){ KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.Info.ValueIsObject ? ObjectIdMap.ObjectIdSize:Unsafe.SizeOf<TValue>(),ValueIsObject=inputLogRecord.Info.ValueIsObject };
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output){
    }
}
