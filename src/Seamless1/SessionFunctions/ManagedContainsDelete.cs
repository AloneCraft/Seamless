using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

/// <summary>
/// RMW で Tombstone 挿入による論理削除（Delete）を行うセッション関数。
/// Contains は ReadSession に分離済みのため、この型で Read は扱わない。
/// </summary>
public struct ManagedContainsDelete:
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,bool,EqualityEmpty>,
    ICreate<ManagedContainsDelete> {
    public static ManagedContainsDelete Create() => new();
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool dst,ref ReadInfo readInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) {
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte value,ref EqualityEmpty input) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref SpanByte dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason) {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) where TEpochAccessor : IEpochAccessor {
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }
    // Delete: キー未存在時は新規作成しない
    public bool NeedInitialUpdate(ref SpanByte key,ref EqualityEmpty input,ref bool output,ref RMWInfo rmwInfo) {
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    // キーが存在する場合は Tombstone を挿入して削除。output=trueで成功を通知。
    public bool InPlaceUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo) {
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public bool NeedCopyUpdate(ref SpanByte key,ref EqualityEmpty input,ref SpanByte value,ref bool output,ref RMWInfo rmwInfo) {
        return true;
    }
    public bool CopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte key,ref EqualityEmpty input,ref SpanByte oldValue,ref SpanByte newValue,ref bool output,ref RMWInfo rmwInfo) {
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch) where TEpochAccessor : IEpochAccessor {
    }
    public void RMWCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
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
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch) where TEpochAccessor : IEpochAccessor {
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata) {
        throw new NotSupportedException();
    }
    public int GetRMWModifiedValueLength(ref SpanByte value,ref EqualityEmpty input) => value.Length+4;
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty input) {
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }
    public void ConvertOutputToHeap(ref EqualityEmpty input,ref bool output) {
    }
}
