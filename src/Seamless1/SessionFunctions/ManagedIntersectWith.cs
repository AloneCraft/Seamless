using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct ManagedIntersectWith:
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,EqualityEmpty,EqualityEmpty> {
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public bool NeedInitialUpdate(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // キーが存在しない場合は新規作成しない（NOTFOUND を返させる）。  
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        // NeedInitialUpdate が常に false のため、IntersectWith で新規レコード作成には進まない。
        throw new NotSupportedException();
    }
    public bool InPlaceUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public bool NeedCopyUpdate(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
    public bool CopyUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte Key,ref EqualityEmpty Input,ref RMWInfo RmwInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
        // RMW の成否にかかわらず呼ばれるが、IntersectWith では後処理は不要。

    }
    public void RMWCompletionCallback(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata) {
        // IterateLookup 走査中の RMW は read-only 領域やディスク上レコードで Pending 完了になりうる。
        // IntersectWith は完了通知の出力を参照しないため、待機側で完了させるだけでよい。
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
    public void ReadCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // ReadCompletionCallback は非同期 Read 時のみ呼ばれる。RMW のみ使用するため到達しない。
        throw new NotSupportedException();
    }
    public int GetRMWModifiedValueLength(ref SpanByte Value,ref EqualityEmpty Input) {
        return 4;//IntersectWithは更新後の値が存在しない（= 削除）ため、Value部分は空（長さ0）だが、RecordHeaderのDeletedフラグを立てるために4バイト必要。
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty Input) {
        // NeedInitialUpdate が false のため、初期値サイズ計算には進まない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte Value,ref EqualityEmpty Input) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref EqualityEmpty Input,ref EqualityEmpty Output) {
        // Pending 完了時に呼ばれうるが、EqualityEmpty はヒープ化不要。
    }
}
