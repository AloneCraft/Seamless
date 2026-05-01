using System.Runtime.CompilerServices;
using Seamless.Stores;

namespace Seamless.SessionFunctions;

public readonly struct UnmanagedContains<TKey, TValue>:
    ISessionFunctions<TKey,TValue,EqualityEmpty,bool,EqualityEmpty>,
    ICreate<UnmanagedContains<TKey,TValue>>
    where TKey : unmanaged
    where TValue : unmanaged {
    public static UnmanagedContains<TKey,TValue> Create() => new();

    public bool SingleReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool dst,ref ReadInfo readInfo) {
        dst=true;
        return true;
    }

    public bool ConcurrentReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        dst=true;
        return true;
    }

    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        if(status.Found)
            output=true;
    }

    public bool SingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        dst=src;
        return true;
    }

    public void PostSingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason) {
    }

    public bool ConcurrentWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        dst=src;
        return true;
    }

    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
    }

    public bool NeedInitialUpdate(ref TKey key,ref EqualityEmpty input,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool InitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public void PostInitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool NeedCopyUpdate(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool CopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public bool PostCopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool InPlaceUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public int GetRMWModifiedValueLength(ref TValue value,ref EqualityEmpty input) => throw new NotSupportedException();
    public int GetRMWInitialValueLength(ref EqualityEmpty input) => throw new NotSupportedException();
    public int GetUpsertValueLength(ref TValue value,ref EqualityEmpty input) => Unsafe.SizeOf<TValue>();

    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
    }

    public void RMWCompletionCallback(ref TKey key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }

    public bool SingleDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
    public bool ConcurrentDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor => throw new NotSupportedException();

    public void ConvertOutputToHeap(ref EqualityEmpty input,ref bool output) {
    }
}