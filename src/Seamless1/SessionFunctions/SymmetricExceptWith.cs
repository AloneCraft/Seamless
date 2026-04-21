using System.Diagnostics.CodeAnalysis;
//using EqualityEmpty=Seamless.Collections.Empty;
namespace Seamless.SessionFunctions;

internal readonly struct SymmetricExceptWith:
    ISessionFunctions<SpanByte,SpanByte,SpanByte,EqualityEmpty,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public bool NeedInitialUpdate(ref SpanByte Key,ref SpanByte Input,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        return true;// キー未存在時は新規作成する
    }
    public bool InitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        //NeedInitialUpdateがtrueを返すときに呼ばれる
        RmwInfo.Action=RMWAction.Default;
        Input.CopyTo(ref Value);
        return true;
    }
    public bool InPlaceUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        RmwInfo.Action=RMWAction.ExpireAndStop;//キーが存在する場合は削除する。SymmetricExceptWithなので、更新はせずに削除のみ行う。
        return false;
    }
    public void PostInitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {

    }
    public bool NeedCopyUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) => true;
    public bool CopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。
    }
    public void RMWCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata) {
        //CompletePendingWithOutputs
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref SpanByte Key,ref DeleteInfo DeleteInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte Key,ref DeleteInfo DeleteInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata) {
        // ReadCompletionCallback は非同期 Read 時のみ呼ばれる。RMW のみ使用するため到達しない。
        throw new NotSupportedException();
    }
    public int GetRMWModifiedValueLength(ref SpanByte Value,ref SpanByte Input) {
        return Value.Length+4;
    }
    public int GetRMWInitialValueLength(ref SpanByte Input) {
        return Input.Length+4;
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte Value,ref SpanByte Input) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref SpanByte Input,ref EqualityEmpty Output) {
    }
}
