namespace Seamless.SessionFunctions;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
/// <summary>
/// ManagedCollection 用セッション関数。ManagedAdd は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly partial struct ManagedRead():
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,SpanByteAndMemory,EqualityEmpty>,
    ICreate<ManagedRead>{
    private readonly MemoryPool<byte> memoryPool=MemoryPool<byte>.Shared;
    public bool SingleReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo){
        var Span=value.AsReadOnlySpan();
        if(Span.IsEmpty)
            return true;
        dst.CopyFrom(Span,MemoryPool<byte>.Shared);
        return true;
    }
    public bool ConcurrentReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        var Span=value.AsReadOnlySpan();
        if(Span.IsEmpty)
            return true;
        dst.CopyFrom(Span,MemoryPool<byte>.Shared);
        return true;
    }
    public bool SingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        src.CopyTo(ref dst);
        return true;
    }
    public bool ConcurrentWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        if(src.Length>dst.Length)
            return false;
        src.CopyTo(ref dst);
        return true;
    }
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) where TEpochAccessor:IEpochAccessor{
    }
    public void PostSingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason){
    }
    public bool NeedInitialUpdate(ref SpanByte key,ref EqualityEmpty input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)=>
        true;
    public bool InitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        return true;
    }
    public bool InPlaceUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo)=>
        false;
    public void PostInitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        // RMWのキー未存在分岐で呼ばれる。output.Length>0で成功を通知する。
        output.CopyFrom(value.AsReadOnlySpan(),this.memoryPool);
    }
    public bool NeedCopyUpdate(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)=>false;
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWで更新しない限りない");
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)=>
        throw new NotSupportedException("RMWで更新しない限りない");
    public int GetUpsertValueLength(ref SpanByte value,ref EqualityEmpty input)=>value.Length+4;
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }
    public void RMWCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref SpanByteAndMemory output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    public void ReadCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref SpanByteAndMemory output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWModifiedValueLength(ref SpanByte value,ref EqualityEmpty input){
        throw new NotSupportedException("NeedCopyUpdate() が常に false 計上デッドコード");
    }
    public int GetRMWInitialValueLength(ref EqualityEmpty input)=>4;
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref SpanByteAndMemory output){
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException("ManagedAddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotImplementedException("ManagedAddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException("ManagedAddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotImplementedException("ManagedAddはUpsert,RMW");
    }
}
