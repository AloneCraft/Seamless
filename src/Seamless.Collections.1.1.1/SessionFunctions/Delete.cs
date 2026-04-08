using System.Runtime.CompilerServices;
namespace Seamless.SessionFunctions;
using System.Diagnostics.CodeAnalysis;

public struct Delete:
    ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,core_Empty>
    { 
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo)    {
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input){
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason) { 
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{
        throw new NotSupportedException("RMWは呼ばれない");
    }
    public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
        //return false;
    }
    // キーが存在する場合は Tombstone を挿入して削除 → Status.Found
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { 
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return true;
    }
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
    }
    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,core_Empty ctx,Status status,RecordMetadata recordMetadata) {
        //pending完了時に呼ばれる
    }
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,core_Empty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException("RMWは呼ばれない");
    }
    public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref SpanByte input) => input.Length+4;
    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { 
        //RMWでPending待ちで削除するとき呼ばれる
    }
}
public struct Delete<TKey,TValue>:
    ISessionFunctions<TKey,TValue,TValue,TValue,Empty>
    where TKey:unmanaged,IEquatable<TKey>
    { 
    [ExcludeFromCodeCoverage]public bool SingleReader(ref TKey key,ref TValue input,ref TValue value,ref TValue dst,ref ReadInfo readInfo)    {
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref TKey key,ref TValue input,ref TValue value,ref TValue dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref TValue value,ref TValue input){
        throw new NotSupportedException("Upsertで呼ばれる");
    }
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        dst=src;
        return true;
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        dst=src;
        return true;
    }
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,WriteReason reason) { 
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref TValue input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{
        throw new NotSupportedException("RMWは呼ばれない");
    }
    public bool NeedInitialUpdate(ref TKey key,ref TValue input,ref TValue output,ref RMWInfo rmwInfo){
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
        //return false;
    }
    // キーが存在する場合は Tombstone を挿入して削除 → Status.Found
    public bool InPlaceUpdater(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo) { 
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }
    public bool NeedCopyUpdate(ref TKey key,ref TValue input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo){
        return true;
    }
    public bool CopyUpdater(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue newValue,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        //output.Length=1;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue newValue,ref TValue output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
    }
    public void RMWCompletionCallback(ref TKey key,ref TValue input,ref TValue output,Empty ctx,Status status,RecordMetadata recordMetadata) { 
        //pending完了時に呼ばれる
    }
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{ 
        throw new NotSupportedException("RMWは呼ばれない");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref TValue input,ref TValue output,Empty ctx,Status status,RecordMetadata recordMetadata) { 
        throw new NotSupportedException("RMWは呼ばれない");
    }
    public int GetRMWModifiedValueLength(ref TValue value,ref TValue input) => Unsafe.SizeOf<TValue>();
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref TValue input) =>Unsafe.SizeOf<TValue>();
    public void ConvertOutputToHeap(ref TValue input,ref TValue output) { 
        //RMWでPending待ちで削除するとき呼ばれる
    }
    }
