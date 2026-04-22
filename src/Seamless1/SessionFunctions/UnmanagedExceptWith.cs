using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct UnmanagedExceptWith<TKey>:
    ISessionFunctions<TKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,EqualityEmpty>
    where TKey : unmanaged, IEquatable<TKey> {
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty value,ref EqualityEmpty output,ref ReadInfo readInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty value,ref EqualityEmpty output,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty src,ref EqualityEmpty dst,ref EqualityEmpty output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty src,ref EqualityEmpty dst,ref EqualityEmpty output,ref UpsertInfo upsertInfo,WriteReason reason) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty src,ref EqualityEmpty dst,ref EqualityEmpty output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedInitialUpdate(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty value,ref EqualityEmpty output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty value,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedCopyUpdate(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty oldValue,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty oldValue,ref EqualityEmpty newValue,ref EqualityEmpty output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty oldValue,ref EqualityEmpty newValue,ref EqualityEmpty output,ref RMWInfo rmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceUpdater(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty value,ref EqualityEmpty output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWModifiedValueLength(ref EqualityEmpty value,ref EqualityEmpty input) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty input) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref EqualityEmpty value,ref EqualityEmpty input) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void RMWCompletionCallback(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException();
    }
    public bool SingleDeleter(ref TKey key,ref EqualityEmpty value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        return true;
    }
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo) {
    }
    public bool ConcurrentDeleter(ref TKey key,ref EqualityEmpty value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        return true;
    }
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref EqualityEmpty output) {
    }
}