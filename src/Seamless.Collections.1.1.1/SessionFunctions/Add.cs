namespace Seamless.SessionFunctions;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
/// <summary>
/// ManagedCollection 用セッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
public readonly struct Add():
    ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,core_Empty>
    {
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo){
        var Span = value.AsReadOnlySpan();
        if(Span.IsEmpty) 
            return true;
        dst.CopyFrom(Span, MemoryPool<byte>.Shared);
        return true;
    }
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        var Span = value.AsReadOnlySpan();
        if(Span.IsEmpty)
            return true;
        dst.CopyFrom(Span,MemoryPool<byte>.Shared);
        return true;
    }
    public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }
    public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        if(src.Length>dst.Length) 
            return false;
        src.CopyTo(ref dst);
        return true;
    }
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{
        //Upsert([])とRMW(Add)
    }
    public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason){
        //Upsert,RMW
    }
    public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => 
        true;
    public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        input.CopyTo(ref value);
        return true;
    }
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => 
        false;
    public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        //Upsertの未存在分岐で呼ばれる。RMWのキー未存在分岐でも呼ばれる。
    }
    public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => false;
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("RMWで更新しない限りない");
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => 
        throw new NotSupportedException("RMWで更新しない限りない");
        //true;
    public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{

    }
    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,core_Empty ctx,Status status,RecordMetadata recordMetadata){
    }
    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,core_Empty ctx,Status status,RecordMetadata recordMetadata){
    }
    [ExcludeFromCodeCoverage]public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input){
        throw new NotSupportedException("NeedCopyUpdate() が常に false 計上デッドコード");
    }
    public int GetRMWInitialValueLength(ref SpanByte input) => input.Length+4;
    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output){
    }
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException("AddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotImplementedException("AddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException("AddはUpsert,RMW");
    }
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotImplementedException("AddはUpsert,RMW");
    }
}
