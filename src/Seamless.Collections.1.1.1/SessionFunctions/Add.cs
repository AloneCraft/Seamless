namespace Seamless.SessionFunctions;

using System.Buffers;
using Tsavorite.core;

public struct Add:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Seamless.Collections.Empty> {
    public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo) {
        var span = value.AsReadOnlySpan();
        if(!span.IsEmpty) dst.CopyFrom(span,MemoryPool<byte>.Shared);
        return true;
    }
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        var span = value.AsReadOnlySpan();
        if(!span.IsEmpty) dst.CopyFrom(span,MemoryPool<byte>.Shared);
        return true;
    }
    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata) { }

    public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }

    public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason) { }
    public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        if(src.Length>dst.Length) return false;
        src.CopyTo(ref dst);
        return true;
    }


    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor{

    }

    public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => true;
    public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        input.CopyTo(ref value);
        return true;
    }
    public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { }
    public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => false;
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        oldValue.CopyTo(ref newValue);
        return true;
    }

    public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) => true;
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) => true;
    public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
    public int GetRMWInitialValueLength(ref SpanByte input) => input.Length+4;
    public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;



    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor { }

    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata) { }

    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => true;
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) { }
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) => true;

    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)where TEpochAccessor : IEpochAccessor { }

    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { }
}
