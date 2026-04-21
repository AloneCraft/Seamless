using Seamless.Stores;
namespace Seamless.SessionFunctions;

using System.Buffers;
/// <summary>
/// ManagedCollection 用セッション関数。ManagedAdd は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly  struct ManagedRead():
    ISessionFunctions<ManagedSessionValue,ManagedSessionValue,EqualityEmpty,ManagedOutput,EqualityEmpty>,
    ICreate<ManagedRead> {
    public static ManagedRead Create()=>new();
    private readonly MemoryPool<byte> memoryPool=MemoryPool<byte>.Shared;
    public bool SingleReader(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref ManagedOutput dst,ref ReadInfo readInfo){
        var Span=value.AsReadOnlySpan();
        if(Span.IsEmpty)
            return true;
        dst.CopyFrom(Span,MemoryPool<byte>.Shared);
        return true;
    }
    public bool ConcurrentReader(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref ManagedOutput dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        var Span=value.AsReadOnlySpan();
        if(Span.IsEmpty)
            return true;
        dst.CopyFrom(Span,MemoryPool<byte>.Shared);
        return true;
    }
    public bool SingleWriter(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref ManagedOutput output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        src.CopyTo(ref dst);
        return true;
    }
    public bool ConcurrentWriter(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref ManagedOutput output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        if(src.Length>dst.Length)
            return false;
        src.CopyTo(ref dst);
        return true;
    }
    public void PostUpsertOperation<TEpochAccessor>(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) where TEpochAccessor:IEpochAccessor {
    }
    public void PostSingleWriter(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue src,ref ManagedSessionValue dst,ref ManagedOutput output,ref UpsertInfo upsertInfo,WriteReason reason){
    }
    public bool NeedInitialUpdate(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedOutput output,ref RMWInfo rmwInfo) =>
        true;
    public bool InitialUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref ManagedOutput output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        return true;
    }
    public bool InPlaceUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref ManagedOutput output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) =>
        false;
    public void PostInitialUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref ManagedOutput output,ref RMWInfo rmwInfo){
        // RMWのキー未存在分岐で呼ばれる。output.Length>0で成功を通知する。
        output.CopyFrom(value.AsReadOnlySpan(),this.memoryPool);
    }
    public bool NeedCopyUpdate(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue value,ref ManagedOutput output,ref RMWInfo rmwInfo) => false;
    public bool CopyUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref ManagedOutput output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        // Read のみ使用するため、RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    }
    public bool PostCopyUpdater(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedSessionValue oldValue,ref ManagedSessionValue newValue,ref ManagedOutput output,ref RMWInfo rmwInfo) =>
        // Read のみ使用するため、RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    public int GetUpsertValueLength(ref ManagedSessionValue value,ref EqualityEmpty input) => value.Length+4;
    public void PostRMWOperation<TEpochAccessor>(ref ManagedSessionValue key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor {
    }
    public void RMWCompletionCallback(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedOutput output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public void ReadCompletionCallback(ref ManagedSessionValue key,ref EqualityEmpty input,ref ManagedOutput output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public int GetRMWModifiedValueLength(ref ManagedSessionValue value,ref EqualityEmpty input){
        // NeedCopyUpdate が常に false のため到達しない。
        throw new NotSupportedException();
    }
    public int GetRMWInitialValueLength(ref EqualityEmpty input) => 4;
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref ManagedOutput output){
    }
    public bool SingleDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        // Read のみ使用するため、Upsert/RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    }
    public void PostSingleDeleter(ref ManagedSessionValue key,ref DeleteInfo deleteInfo){
        // Read のみ使用するため、Upsert/RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    }
    public bool ConcurrentDeleter(ref ManagedSessionValue key,ref ManagedSessionValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        // Read のみ使用するため、Upsert/RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    }
    public void PostDeleteOperation<TEpochAccessor>(ref ManagedSessionValue key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor {
        // Read のみ使用するため、Upsert/RMW 系コールバックには到達しない。
        throw new NotSupportedException();
    }
}
