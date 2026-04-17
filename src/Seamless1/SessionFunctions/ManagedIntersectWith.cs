using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct ManagedIntersectWith:
    ISessionFunctions<SpanByte,SpanByte,EqualityEmpty,EqualityEmpty,EqualityEmpty>{
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Src,ref SpanByte Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason){
        throw new NotSupportedException("Upsert");
    }
    public bool NeedInitialUpdate(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        // キーが存在しない場合は新規作成しない（NOTFOUND を返させる）。  
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Upsert");
    }
    public bool InPlaceUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("Upsert");
    }
    public bool NeedCopyUpdate(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
    public bool CopyUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref SpanByte Key,ref EqualityEmpty Input,ref SpanByte OldValue,ref SpanByte NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte Key,ref EqualityEmpty Input,ref RMWInfo RmwInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。

    }
    [ExcludeFromCodeCoverage]
    public void RMWCompletionCallback(ref SpanByte Key,ref EqualityEmpty Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata){
        throw new NotSupportedException("RMW の非同期I/O完了時に呼ばれる。");
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref SpanByte key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Read非同期で呼ばれる");
    }
    public int GetRMWModifiedValueLength(ref SpanByte Value,ref EqualityEmpty Input){
        return 4;//IntersectWithは更新後の値が存在しない（= 削除）ため、Value部分は空（長さ0）だが、RecordHeaderのDeletedフラグを立てるために4バイト必要。
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty Input){
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはないので、IntersectWith ではこのメソッドは呼ばれない。");
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref SpanByte Value,ref EqualityEmpty Input){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void ConvertOutputToHeap(ref EqualityEmpty Input,ref EqualityEmpty Output){
        throw new NotSupportedException(@"
            **なぜカバーできないか**
            `IntersectWith` は `GetKeyEnumerable()` → `IterateLookup` でスキャンしたキー（= 必ずメモリ上、HeadAddress以上）に対して RMW を呼ぶ。一方 `ConvertOutputToHeap` が呼ばれる条件は「RMW が IsPending になった時」だけ（= レコードがディスク上、HeadAddress未満）。この2条件は同一レコードに対して同時に成立できないため、`IntersectWith` 経由では構造的に到達不可能。
            `Delete` がカバーできる理由は `dict.Remove(knownKey)` が利用者指定のキーで直接 RMW を呼ぶため（IterateLookup を経由しない）。
        ");
    }
}
