using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

/// <summary>
/// Read でキーの存在確認（Contains）、RMW で Tombstone 挿入による論理削除（Delete）を行うセッション関数。
/// Tsavorite の Type-Based Dispatch により Read と RMW が独立してディスパッチされるため、
/// 非競合な Contains と Delete を 1 クラスに統合できる。
/// </summary>
public readonly struct UnmanagedContainsDelete<TKey,TValue>:
    ISessionFunctions<TKey,TValue,EqualityEmpty,bool,EqualityEmpty>,
    ICreate<UnmanagedContainsDelete<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>{
    public static UnmanagedContainsDelete<TKey,TValue> Create()=>new();

    // Contains: キー存在時に dst=true で応答
    public bool SingleReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool dst,ref ReadInfo readInfo){
        dst=true;
        return true;
    }

    // Contains: キー存在時に dst=true で応答
    public bool ConcurrentReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        dst=true;
        return true;
    }

    // Contains: ReadCompletionCallback でキーが見つかった場合に output=true で応答
    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        if(status.Found) output=true;
    }

    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason){
        throw new NotSupportedException("Upsert");
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
    }

    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }

    // Delete: キー未存在時は新規作成しない
    public bool NeedInitialUpdate(ref TKey key,ref EqualityEmpty input,ref bool output,ref RMWInfo rmwInfo)=>false;

    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("NeedInitialUpdate が false を返すので呼ばれない");
    }

    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("InitialUpdater が呼ばれないので呼ばれない");
    }

    public bool NeedCopyUpdate(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref bool output,ref RMWInfo rmwInfo)=>true;

    // Delete: キー存在時は Tombstone を挿入して削除。output=trueで成功を通知。
    public bool CopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }

    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref bool output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdater が false を返すのでここは呼ばれない");
    }

    // Delete: キー存在時は Tombstone を挿入して削除。output=trueで成功を通知。
    public bool InPlaceUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }

    public int GetRMWModifiedValueLength(ref TValue value,ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();

    [ExcludeFromCodeCoverage]
    public int GetRMWInitialValueLength(ref EqualityEmpty input){
        throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
    }

    [ExcludeFromCodeCoverage]
    public int GetUpsertValueLength(ref TValue value,ref EqualityEmpty input){
        throw new NotSupportedException("Upsert");
    }

    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }

    public void RMWCompletionCallback(ref TKey key,ref EqualityEmpty input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }

    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }

    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
    }

    public void ConvertOutputToHeap(ref EqualityEmpty input,ref bool output){
    }
}
