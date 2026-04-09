using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;


namespace Seamless.SessionFunctions;

public readonly struct UnmanagedDelete<TKey,TValue>:
    ISessionFunctions<TKey,TValue,TValue,TValue,core_Empty>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {

    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref TKey key,ref TValue input,ref TValue value,ref TValue dst,ref ReadInfo readInfo) {
        throw new NotSupportedException("Read");
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref TKey key,ref TValue input,ref TValue value,ref TValue dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Read");
    }

    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref TValue input,ref TValue output,core_Empty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException("Read");
    }

    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Upsert");
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,WriteReason reason) {
        throw new NotSupportedException("Upsert");
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Upsert");
    }

    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref TValue input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor {
        throw new NotSupportedException("Upsert");
    }

    public bool NeedInitialUpdate(ref TKey key,ref TValue input,ref TValue output,ref RMWInfo rmwInfo) => false;

    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("NeedInitialUpdate が false を返すので呼ばれない");
    }

    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("InitialUpdater が呼ばれないので呼ばれない");
    }

    public bool NeedCopyUpdate(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue output,ref RMWInfo rmwInfo) => true;

    public bool CopyUpdater(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue newValue,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action = RMWAction.ExpireAndStop;
        return false;
    }

    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue newValue,ref TValue output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException("CopyUpdater が false を返すのでここは呼ばれない");
    }

    public bool InPlaceUpdater(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action = RMWAction.ExpireAndStop;
        return false;
    }

    public int GetRMWModifiedValueLength(ref TValue value,ref TValue input) => Unsafe.SizeOf<TValue>();
    public int GetRMWInitialValueLength(ref TValue input) => Unsafe.SizeOf<TValue>();
    public int GetUpsertValueLength(ref TValue value,ref TValue input) => Unsafe.SizeOf<TValue>();

    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor {
    }

    public void RMWCompletionCallback(ref TKey key,ref TValue input,ref TValue output,core_Empty ctx,Status status,RecordMetadata recordMetadata) {
    }

    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Delete");
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException("Delete");
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Delete");
    }

    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor {
        throw new NotSupportedException("Delete");
    }

    public void ConvertOutputToHeap(ref TValue input,ref TValue output) {
    }
}
