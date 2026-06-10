using Tsavorite.Collections;
using Tsavorite.Stores;
namespace Tsavorite.SessionFunctions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Collection 用セチE��ョン関数、EanagedAdd は RMW と Upsert の両方で使われる。RMWではAddセマンチE��クス�E�キー既存時は拒否�E�、E
/// 冁E��でシリアライズするため、呼び側で PinnedSpanByte めEGCHandle のピン止めが不要、E
/// </summary>
public readonly struct Upsert<TValue, TStrategy>:
    ISessionFunctions<TValue,bool,EqualityEmpty>,
    ICreate<Upsert<TValue,TStrategy>>    where TStrategy : ISerializer<TValue> {
    public static Upsert<TValue,TStrategy> Create() => new();
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static void ThrowReadDeleteEntryPoint() {
        // Upsert は RMW / Upsert にだけバインドされる。直接 Read / Delete のコールバックには到達しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowReadDeleteEntryPoint<T>() {
        // Upsert は RMW / Upsert にだけバインドされる。直接 Read / Delete のコールバックには到達しなぁE��E
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    [DoesNotReturn]
    private static T ThrowCopyUpdateEntryPoint<T>() {
        // NeedCopyUpdate が常に false のため、CopyUpdater 系には到達しなぁE��E
        throw new NotSupportedException();
    }
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowReadDeleteEntryPoint<bool>();
    }

    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        ThrowReadDeleteEntryPoint();
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

    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
    }

    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
    }

    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
    }

    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,ReadOnlySpan<byte> srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        RecordSizeInfo sizeInfo=new(){ FieldInfo=this.GetUpsertFieldInfo(logRecord,srcValue,ref input) };
        logRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }

    public bool InPlaceWriter(ref LogRecord logRecord,ref TValue input,IHeapObject srcValue,ref bool output,ref UpsertInfo upsertInfo) {
        RecordSizeInfo sizeInfo=new(){ FieldInfo=this.GetUpsertFieldInfo(logRecord,srcValue,ref input) };
        logRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }

    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,ref TValue input,in TSourceLogRecord inputLogRecord,ref bool output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        RecordSizeInfo sizeInfo=new(){ FieldInfo=this.GetUpsertFieldInfo(dstLogRecord,in inputLogRecord,ref input) };
        dstLogRecord.PopulateRecordSizeInfoForIPU(ref sizeInfo);
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }

    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)

    #if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

    #else

        where TKey : IKey

    #endif

        where TEpochAccessor : IEpochAccessor {
    }

    public void PostUpsertOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)

    #if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

    #else

        where TKey : IKey

    #endif

        where TEpochAccessor : IEpochAccessor {
    }

    /// <summary>
    /// キー未存在時�E新規作�Eする
    /// </summary>
    public bool NeedInitialUpdate<TKey>(TKey key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return true;
    }

    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        // TValue を�E部でシリアライズしてログレコードに書き込む
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(TStrategy.Serialize(ref input),in sizeInfo);
    }

    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        output=true;
    }

    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return false;
    }

    public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowCopyUpdateEntryPoint<bool>();
    }

    public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref TValue input,ref bool output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return ThrowCopyUpdateEntryPoint<bool>();
    }

    /// <summary>
    /// キー既存時は InPlaceUpdater=false で更新を拒否�E�Eutput は default(false) のまま�E�失敗！E
    /// </summary>
    public bool InPlaceUpdater(ref LogRecord logRecord,ref TValue input,ref bool output,ref RMWInfo rmwInfo) {
        return false;
    }

    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)

    #if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

    #else

        where TKey : IKey

    #endif

        where TEpochAccessor : IEpochAccessor {
    }

    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }

    public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadDeleteEntryPoint<bool>();
    }

    public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        ThrowReadDeleteEntryPoint();
    }

    public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        return ThrowReadDeleteEntryPoint<bool>();
    }

    public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)

    #if NET10_0_OR_GREATER

        where TKey : IKey, allows ref struct

    #else

        where TKey : IKey

    #endif

        where TEpochAccessor : IEpochAccessor {
        ThrowReadDeleteEntryPoint();
    }

    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref TValue input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.KeyBytes.Length,ValueSize=TStrategy.Serialize(ref input).Length };
    }

    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=TStrategy.Serialize(ref input).Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct{
        #else
        where TKey : IKey{
        #endif
        return new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref TValue input)
        #if NET10_0_OR_GREATER
        where TKey : IKey, allows ref struct
        #else
        where TKey : IKey
        #endif
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.DataHeader.ValueIsObject ? ObjectIdMap.ObjectIdSize : inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.DataHeader.ValueIsObject };
    }

    public void BeforeConsistentReadCallback(long hash) {
    }
    public void AfterConsistentReadKeyCallback() {
    }
    public void BeforeConsistentReadKeyBatchCallback(ReadOnlySpan<PinnedSpanByte> parameters) {
    }
    public bool AfterConsistentReadKeyBatchCallback(int keyCount) {
        return true;
    }
    public void ConvertOutputToHeap(ref TValue input,ref bool output) {
    }
}
