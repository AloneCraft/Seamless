using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
namespace Seamless.SessionFunctions;

internal readonly struct UnmanagedIntersectWith<TKey>:
    ISessionFunctions<TKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,EqualityEmpty>
    where TKey : unmanaged {
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo) {
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref EqualityEmpty Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref EqualityEmpty Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref EqualityEmpty Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public bool NeedInitialUpdate(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // キーが存在しない場合は新規作成しない（NOTFOUND を返させる）。
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        // NeedInitialUpdate が常に false のため、IntersectWith で新規レコード作成には進まない。
        throw new NotSupportedException();
    }
    public bool InPlaceUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // NeedInitialUpdate が false のため InitialUpdater も呼ばれず、到達しない。
        throw new NotSupportedException();
    }
    public bool NeedCopyUpdate(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
    public bool CopyUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty OldValue,ref EqualityEmpty NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty OldValue,ref EqualityEmpty NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo) {
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TEpochAccessor>(ref TKey Key,ref EqualityEmpty Input,ref RMWInfo RmwInfo,TEpochAccessor Epoch)
        where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata) {
        // IterateLookup 走査中の RMW は read-only 領域やディスク上レコードで Pending 完了になりうる。
        // IntersectWith は完了通知の出力を参照しないため、待機側で完了させるだけでよい。
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref TKey key,ref EqualityEmpty value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref TKey key,ref EqualityEmpty value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo) {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor : IEpochAccessor {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        // このセッション関数は Read 完了通知を使わない。
        throw new NotSupportedException();
    }
    public int GetRMWModifiedValueLength(ref EqualityEmpty Value,ref EqualityEmpty Input) {
        return Unsafe.SizeOf<EqualityEmpty>();
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty Input) {
        // NeedInitialUpdate が false のため、初期値サイズ計算には進まない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref EqualityEmpty Value,ref EqualityEmpty Input) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref EqualityEmpty Input,ref EqualityEmpty Output) {
        // Pending 完了時に呼ばれうるが、EqualityEmpty はヒープ化不要。
    }
}
