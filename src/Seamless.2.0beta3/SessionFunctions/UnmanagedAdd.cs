using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.SessionFunctions;

/// <summary>
/// UnmanagedCollection 用セッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly partial struct UnmanagedAdd<TKey,TValue>():
    ISessionFunctions<TValue,bool,EqualityEmpty>,
    ICreate<UnmanagedAdd<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    public static UnmanagedAdd<TKey,TValue> Create() => new();
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("UnmagedAddクラスのRMWでのみ使われる");
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey0:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostUpsertOperation<TKey0,TEpochAccessor>(TKey0 key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey0:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
    }
    /// <summary>
    /// NeedInitialUpdate が true で、キー未存在時は InitialUpdater で値を書き込み
    /// </summary>
    public bool NeedInitialUpdate<TKey0>(TKey0 key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TKey0 : IKey, allows ref struct {
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        // TValueの参照をバイトのReadOnlySpanとして解釈（アロケーション・コピーなし）
        var inputSpan = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TValue, byte>(ref input), 
            Unsafe.SizeOf<TValue>()
        );
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(inputSpan, in sizeInfo);
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        return false;
    }
    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo){
        // RMWのキー未存在分岐で呼ばれる。output=trueで成功を通知する。
        output = true;
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("NeedCopyUpdate が false を返すので呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("NeedCopyUpdate が false を返すので呼ばれない");
    }
    public void PostRMWOperation<TKey0,TEpochAccessor>(TKey0 key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
        where TKey0 : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Add は Delete を使用しない");
    }
    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Add は Delete を使用しない");
    }
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Add は Delete を使用しない");
    }
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey0,TEpochAccessor>(TKey0 key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
        where TKey0:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Add は Delete を使用しない");
    }
    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetRMWInitialFieldInfo<TKey0>(TKey0 key,ref TValue input)
        where TKey0 : IKey, allows ref struct {
        return new() { KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,ReadOnlySpan<byte> value,ref TValue input)
        where TKey0 : IKey, allows ref struct {
        return new() { KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>() };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0>(TKey0 key,IHeapObject value,ref TValue input)
        where TKey0 : IKey, allows ref struct {
        return new() { KeySize=Unsafe.SizeOf<TKey>(),ValueSize=Unsafe.SizeOf<TValue>(),ValueIsObject=true };
    }
    public RecordFieldInfo GetUpsertFieldInfo<TKey0,TSourceLogRecord>(TKey0 key,in TSourceLogRecord inputLogRecord,ref TValue input)
        where TKey0 : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=Unsafe.SizeOf<TKey>(),ValueSize=inputLogRecord.Info.ValueIsObject ? ObjectIdMap.ObjectIdSize : Unsafe.SizeOf<TValue>(),ValueIsObject=inputLogRecord.Info.ValueIsObject };
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output){
    }
}
