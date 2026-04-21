using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;
/// <summary>
/// RMWでKey,Value両方が一致するエントリのみ削除するセッション関数。
/// </summary>
internal readonly struct ManagedRemove_KeyValuePair():
    ISessionFunctions<SpanByte,SpanByte,SpanByte,bool,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref bool dst,ref ReadInfo readInfo) =>
        // Read 操作は使用しない。
        throw new NotSupportedException();
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) =>
        // Read 操作は使用しない。
        throw new NotSupportedException();
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref bool output,ref RMWInfo rmwInfo) {
        return false;// キー未存在時は新規作成しない
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        // キー存在時（Mutable）は値をOutputにコピーしてからExpireAndStopで論理削除
        if(value.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan())) {
            output=true;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate が false のため InitialUpdater も呼ばれず、到達しない。
        throw new NotSupportedException();
    }
    // キー存在時（ReadOnly/Disk）は値が一致したらMutableに削除レコードを追加する
    public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref bool output,ref RMWInfo rmwInfo) {
        return oldValue.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan());
    }
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref bool output,ref RMWInfo rmwInfo) {
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        //RMW 確定後に呼ばれる。
    }
    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        //CompletePendingWithOutputs
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // 非同期 Read 完了コールバックは使用しない（RMW のみ使用）。
        throw new NotSupportedException();
    }
    public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref SpanByte input) {
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref SpanByte input,ref bool output) {
        //RMWでPending待ちで削除するとき呼ばれる
    }
}
