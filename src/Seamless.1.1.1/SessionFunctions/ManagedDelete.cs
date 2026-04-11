using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

public partial struct ManagedDelete:
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,bool,EqualityEmpty>,
    ICreate<ManagedDelete>
    {
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool dst,ref ReadInfo readInfo)    {
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){    throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte value,ref EqualityEmpty input){
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason) { 
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    public bool NeedInitialUpdate(ref SpanByte key,ref EqualityEmpty input,ref bool output,ref RMWInfo rmwInfo){
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    // キーが存在する場合は Tombstone を挿入して削除。output=trueで成功を通知。
    public bool InPlaceUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo) { 
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    public bool NeedCopyUpdate(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo){
        return true;
    }
    public bool CopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref bool output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
    }
    public void RMWCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
    }
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException("Read");
    }
    public int GetRMWModifiedValueLength(ref SpanByte value,ref EqualityEmpty input) => value.Length;
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty input) => throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref bool output) { 
    }
}
