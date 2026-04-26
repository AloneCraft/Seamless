using Seamless.Stores;

namespace Seamless.SessionFunctions;

public readonly struct ManagedContains:
    ISessionFunctions<ManagedSessionValue,ManagedSessionValue,EqualityEmpty,bool,EqualityEmpty>,
    ICreate<ManagedContains> {
    public static ManagedContains Create() => new();

    public bool SingleReader(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref bool dst,ref ReadInfo readInfo) {
        dst=true;
        return true;
    }

    public bool ConcurrentReader(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        dst=true;
        return true;
    }

    public void ReadCompletionCallback(ref ManagedSessionValue key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        if(status.Found)
            output=true;
    }

    public bool SingleWriter(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }

    public bool ConcurrentWriter(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        if(src.Length>dst.Length)
            return false;
        src.CopyTo(ref dst);
        return true;
    }

    public void PostSingleWriter(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason) {
    }

    public void PostUpsertOperation<TEpochAccessor>(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
    }

    public bool NeedInitialUpdate(ref ManagedSessionValue key,ref EqualityEmpty input,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool InitialUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public void PostInitialUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool NeedCopyUpdate(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool CopyUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public bool PostCopyUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref bool output,ref RMWInfo rmwInfo) => throw new NotSupportedException();
    public bool InPlaceUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public int GetRMWModifiedValueLength(ref ManagedSessionValue value,ref EqualityEmpty input) => throw new NotSupportedException();
    public int GetRMWInitialValueLength(ref EqualityEmpty input) => throw new NotSupportedException();
    public int GetUpsertValueLength(ref ManagedSessionValue value,ref EqualityEmpty input) => value.Length+4;

    public void PostRMWOperation<TEpochAccessor>(ref ManagedSessionValue key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
    }

    public void RMWCompletionCallback(ref ManagedSessionValue key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }

    public bool SingleDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public void PostSingleDeleter(ref ManagedSessionValue key,ref DeleteInfo deleteInfo) => throw new NotSupportedException();
    public bool ConcurrentDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => throw new NotSupportedException();
    public void PostDeleteOperation<TEpochAccessor>(ref ManagedSessionValue key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor => throw new NotSupportedException();

    public void ConvertOutputToHeap(ref EqualityEmpty input,ref bool output) {
    }
}