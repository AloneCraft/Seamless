namespace Seamless.Collections;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
public readonly struct Add_SessionFunctions():ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty> {
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref ReadInfo readInfo)
        where TSourceLogRecord : ISourceLogRecord {
        srcLogRecord.ValueSpan.CopyTo(ref output,this.memoryPool);
        return true;
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return logRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InPlaceWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }

    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo) {
        return dstLogRecord.TrySetValueObjectAndPrepareOptionals(srcValue,in sizeInfo);
    }
    public bool InitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return dstLogRecord.TryCopyFrom(in inputLogRecord,in sizeInfo);
    }
    public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,ReadOnlySpan<byte> valueSpan,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostUpsertOperation<TKey,TEpochAccessor>(TKey key,ref PinnedSpanByte input,IHeapObject valueObject,ref UpsertInfo upsertInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ReadOnlySpan<byte> srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,IHeapObject srcValue,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo){
    }
    public void PostInitialWriter<TSourceLogRecord>(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,in TSourceLogRecord inputLogRecord,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo)
        where TSourceLogRecord:ISourceLogRecord{
    }
    /// <summary>
    /// NeedInitialUpdate が true で、キー未存在時は InitialUpdater で値を書き込み
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="key"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="rmwInfo"></param>
    /// <returns></returns>
    public bool NeedInitialUpdate<TKey>(TKey key,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TKey : IKey, allows ref struct {
        return true;
    }
    public bool InitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        return dstLogRecord.TrySetValueSpanAndPrepareOptionals(input.ReadOnlySpan,in sizeInfo);
    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    /// <param name="logRecord"></param>
    /// <param name="sizeInfo"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="rmwInfo"></param>
    /// <returns></returns>
    public bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) {
        return false;
    }

    public void PostInitialUpdater(ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){

    }
    /// <summary>
    /// キー既存時は NeedCopyUpdate=false かつ InPlaceUpdater=false で更新を拒否
    /// </summary>
    /// <typeparam name="TSourceLogRecord"></typeparam>
    /// <param name="srcLogRecord"></param>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="rmwInfo"></param>
    /// <returns></returns>
    public bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        return false;
    }
    [ExcludeFromCodeCoverage]public bool CopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWで更新しない限りない");
    }

    [ExcludeFromCodeCoverage]public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)
        where TSourceLogRecord : ISourceLogRecord {
        throw new NotSupportedException("RMWで更新しない限りない");
    }

    public void PostRMWOperation<TKey, TEpochAccessor>(TKey key,ref PinnedSpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epochAccessor)
        where TKey : IKey, allows ref struct
        where TEpochAccessor : IEpochAccessor {
    }

    public void RMWCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
    }
    [ExcludeFromCodeCoverage]public bool InitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException();
    }


    [ExcludeFromCodeCoverage]public void PostInitialDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo){
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]public bool InPlaceDeleter(ref LogRecord dstLogRecord,ref DeleteInfo deleteInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey,TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)
        where TKey:IKey,allows ref struct
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException();
    }


    public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
    }
    public RecordFieldInfo GetRMWModifiedFieldInfo<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input)
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=srcLogRecord.KeyBytes.Length,ValueSize=input.ReadOnlySpan.Length };
    }

    public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
            where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=input.ReadOnlySpan.Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=value.Length };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=ObjectIdMap.ObjectIdSize,ValueIsObject=true };
    }

    public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
        where TKey : IKey, allows ref struct
        where TSourceLogRecord : ISourceLogRecord {
        return new() { KeySize=key.KeyBytes.Length,ValueSize=inputLogRecord.Info.ValueIsObject ? ObjectIdMap.ObjectIdSize : inputLogRecord.ValueSpan.Length,ValueIsObject=inputLogRecord.Info.ValueIsObject };
    }




    public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){
    }
}


