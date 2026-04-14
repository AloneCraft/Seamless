namespace Seamless.SessionFunctions;

using System.Diagnostics.CodeAnalysis;
/// <summary>
/// ManagedCollection 用セッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly partial struct ManagedAdd():
    ISessionFunctions<ManagedSessionValue,ManagedSessionValue,ManagedSessionValue,bool,EqualityEmpty>,
    ICreate<ManagedAdd> {
    public bool SingleReader(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool dst,ref ReadInfo readInfo){
        return true;
    }
    public bool ConcurrentReader(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        return true;
    }
    public bool SingleWriter(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        src.CopyTo(ref dst);
        return true;
    }
    public bool ConcurrentWriter(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        if(src.Length>dst.Length)
            return false;
        src.CopyTo(ref dst);
        return true;
    }
    public void PostUpsertOperation<TEpochAccessor>(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) where TEpochAccessor : IEpochAccessor {
        //Upsert([])とRMW(Add)
    }
    public void PostSingleWriter(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason){
        //Upsert,RMW
    }
    public bool NeedInitialUpdate(ref ManagedSessionValue key,ref ManagedSessionValue input,ref bool output,ref RMWInfo rmwInfo) =>
        true;
    public bool InitialUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        input.CopyTo(ref value);
        return true;
    }
    public bool InPlaceUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) =>
        false;
    public void PostInitialUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo){
        output=true;
    }
    public bool NeedCopyUpdate(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue value,ref bool output,ref RMWInfo rmwInfo) => false;
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWで更新しない限りない");
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref ManagedSessionValue key,ref ManagedSessionValue input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref bool output,ref RMWInfo rmwInfo) =>
        throw new NotSupportedException("RMWで更新しない限りない");
    //true;
    public int GetUpsertValueLength(ref ManagedSessionValue value,ref ManagedSessionValue input) => value.Length+4;
    public void PostRMWOperation<TEpochAccessor>(ref ManagedSessionValue key,ref ManagedSessionValue input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {

    }
    public void RMWCompletionCallback(ref ManagedSessionValue key,ref ManagedSessionValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public void ReadCompletionCallback(ref ManagedSessionValue key,ref ManagedSessionValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWModifiedValueLength(ref ManagedSessionValue value,ref ManagedSessionValue input){
        throw new NotSupportedException("NeedCopyUpdate() が常に false 計上デッドコード");
    }
    public int GetRMWInitialValueLength(ref ManagedSessionValue input) => input.Length+4;
    public void ConvertOutputToHeap(ref ManagedSessionValue input,ref bool output){
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException("AddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref ManagedSessionValue key,ref DeleteInfo deleteInfo){
        throw new NotImplementedException("AddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException("AddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref ManagedSessionValue key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        throw new NotImplementedException("AddはUpsert,RMW");
    }
}
