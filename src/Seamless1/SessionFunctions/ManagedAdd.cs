using Seamless.Stores;

namespace Seamless.SessionFunctions;

/// <summary>
/// ManagedCollection 用セッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly struct ManagedAdd:
    ISessionFunctions<ManagedSessionValue,ManagedSessionValue,ManagedSessionValue,bool,EqualityEmpty>, ICreate<ManagedAdd> {
    public static ManagedAdd Create() => new();

    public bool SingleReader(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool dst,ref ReadInfo readInfo) {
        dst=true;
        return true;
    }
    public bool ConcurrentReader(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        dst=true;
        return true;
    }
    public void ReadCompletionCallback(ref ManagedSessionValue key,ref ManagedSessionValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        output=status.Found;
    }

    public bool SingleWriter(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }

    public void PostSingleWriter(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason) {
        //Upsert,RMW
    }

    public bool ConcurrentWriter(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        if(src.Length>dst.Length)
            return false;
        src.CopyTo(ref dst);
        return true;
    }

    public void PostUpsertOperation<TEpochAccessor>(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) where TEpochAccessor : IEpochAccessor {
        //Upsert([])とRMW(Add)
    }

    public bool NeedInitialUpdate(ref ManagedSessionValue key,ref ManagedSessionValue input,ref bool output,ref RMWInfo rmwInfo) =>
        true;

    public bool InitialUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        input.CopyTo(ref value);
        return true;
    }

    public void PostInitialUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo) {
        output=true;
    }

    public bool NeedCopyUpdate(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo) => false;

    public bool CopyUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        // Read のみ使用するため、RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    }

    public bool PostCopyUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref bool output,ref RMWInfo rmwInfo) =>
        // Read のみ使用するため、RMW 系コールバックには到達しない。
        throw new NotSupportedException();

    public bool InPlaceUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) =>
        false;

    public int GetRMWModifiedValueLength(ref ManagedSessionValue value,ref ManagedSessionValue input) {
        // NeedCopyUpdate が常に false のため到達しない。
        throw new NotSupportedException();
    }

    public int GetRMWInitialValueLength(ref ManagedSessionValue input) => input.Length+4;

    public int GetUpsertValueLength(ref ManagedSessionValue value,ref ManagedSessionValue input) => value.Length+4;

    public void PostRMWOperation<TEpochAccessor>(ref ManagedSessionValue key,ref ManagedSessionValue input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {

    }

    public void RMWCompletionCallback(ref ManagedSessionValue key,ref ManagedSessionValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }

    public bool SingleDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        // Add は Upsert と RMW を使用するため、Read/Delete 系コールバックには到達しない。
        throw new NotSupportedException();
    }

    public void PostSingleDeleter(ref ManagedSessionValue key,ref DeleteInfo deleteInfo) {
        // Add は Upsert と RMW を使用するため、Read/Delete 系コールバックには到達しない。
        throw new NotSupportedException();
    }

    public bool ConcurrentDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        // Add は Upsert と RMW を使用するため、Read/Delete 系コールバックには到達しない。
        throw new NotSupportedException();
    }

    public void PostDeleteOperation<TEpochAccessor>(ref ManagedSessionValue key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        // Add は Upsert と RMW を使用するため、Read/Delete 系コールバックには到達しない。
        throw new NotSupportedException();
    }

    public void ConvertOutputToHeap(ref ManagedSessionValue input,ref bool output) {
    }
}
