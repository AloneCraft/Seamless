using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
namespace Seamless.SessionFunctions;

internal readonly struct UnmanagedIntersectWith<TKey>:
    ISessionFunctions<TKey,EqualityEmpty,EqualityEmpty,EqualityEmpty,EqualityEmpty>
    where TKey:unmanaged,IEquatable<TKey>{
    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref EqualityEmpty Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref EqualityEmpty Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Src,ref EqualityEmpty Dst,ref EqualityEmpty Output,ref UpsertInfo UpsertInfo,WriteReason Reason){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public bool NeedInitialUpdate(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはないので、このメソッドは呼ばれない。");
    }
    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはない。");
    }
    public bool InPlaceUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("InitialUpdater が呼ばれないので呼ばれない");
    }
    public bool NeedCopyUpdate(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Value,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        return true;//ReadOnly領域に見つかった際Mutableにコピーして更新プロセスを続行するか
    }
    public bool CopyUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty OldValue,ref EqualityEmpty NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
        RmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty OldValue,ref EqualityEmpty NewValue,ref EqualityEmpty Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
    }
    public void PostRMWOperation<TEpochAccessor>(ref TKey Key,ref EqualityEmpty Input,ref RMWInfo RmwInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
    }
    [ExcludeFromCodeCoverage]
    public void RMWCompletionCallback(ref TKey Key,ref EqualityEmpty Input,ref EqualityEmpty Output,EqualityEmpty Ctx,Status Status,RecordMetadata RecordMetadata){
        throw new NotSupportedException("RMW の非同期I/O完了時に呼ばれる。");
    }
    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref TKey key,ref EqualityEmpty value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref TKey key,ref EqualityEmpty value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Read非同期で呼ばれる");
    }
    public int GetRMWModifiedValueLength(ref EqualityEmpty Value,ref EqualityEmpty Input){
        return Unsafe.SizeOf<EqualityEmpty>();
    }
    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty Input){
        throw new NotSupportedException("IntersectWith RMWで新規レコードを作ることはないので、このメソッドは呼ばれない。");
    }
    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref EqualityEmpty Value,ref EqualityEmpty Input){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]
    public void ConvertOutputToHeap(ref EqualityEmpty Input,ref EqualityEmpty Output){
        throw new NotSupportedException(@"
            **なぜカバーできないか**
            `UnmanagedIntersectWith` は `GetKeyEnumerable()` → `IterateLookup` でスキャンしたキー（= 必ずメモリ上、HeadAddress以上）に対して RMW を呼ぶ。一方 `ConvertOutputToHeap` が呼ばれる条件は「RMW が IsPending になった時」だけ（= レコードがディスク上、HeadAddress未満）。この2条件は同一レコードに対して同時に成立できないため、`UnmanagedIntersectWith` 経由では構造的に到達不可能。
        ");
    }
}
