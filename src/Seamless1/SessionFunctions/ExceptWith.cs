using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct ExceptWith:
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedInitialUpdate(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool InPlaceUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool NeedCopyUpdate(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool CopyUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void RMWCompletionCallback(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata) {
        throw new NotSupportedException();
    }
    public bool SingleDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) {
        return true;
    }
    public void PostSingleDeleter(ref SpanByte Key,ref DeleteInfo DeleteInfo) {
    }
    public bool ConcurrentDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) {
        return true;
    }
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte Key,ref DeleteInfo DeleteInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWModifiedValueLength(ref SpanByte Value,ref EqualityEmpty Input) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty Input) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte Value,ref EqualityEmpty Input) {
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref EqualityEmpty Input,ref EqualityEmpty Output) {
    }
}
