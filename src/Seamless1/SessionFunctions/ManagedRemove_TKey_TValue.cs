using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

using System.Buffers;
/// <summary>
/// RMWで値の読み取りと削除を同時に行うセッション関数。
/// </summary>
internal readonly  struct ManagedRemove_TKey_TValue():
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,SpanByteAndMemory,EqualityEmpty>{
    private readonly MemoryPool<byte> memoryPool=MemoryPool<byte>.Shared;

    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason){
        throw new NotSupportedException("Upsert");
    }
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    public bool NeedInitialUpdate(ref SpanByte key,ref EqualityEmpty input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;// キー未存在時は新規作成しない
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    // キー存在時（Mutable）は値をOutputにコピーしてからExpireAndStopで論理削除
    public bool InPlaceUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output.CopyFrom(value.AsReadOnlySpan(),this.memoryPool);//RemoveなのでoldValueをコピー
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("InitialUpdaterが呼ばれないので呼ばれない");
    }
    // キー存在時（ReadOnly/Disk）はコピー更新を許可して値を読み取る
    public bool NeedCopyUpdate(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return true;
    }
    public bool CopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output.CopyFrom(oldValue.AsReadOnlySpan(),this.memoryPool);//RemoveなのでoldValueをコピー
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        //RMW 確定後に呼ばれる。
    }
    public void RMWCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref SpanByteAndMemory output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        //CompletePendingWithOutputs
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref SpanByteAndMemory output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Readの非同期I/O完了時に呼ばれるので呼ばれない");
    }
    public int GetRMWModifiedValueLength(ref SpanByte value,ref EqualityEmpty input){
        return value.Length+4;
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty input){
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte value,ref EqualityEmpty input){
        throw new NotSupportedException("Upsert");
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref SpanByteAndMemory output){

    }
}
