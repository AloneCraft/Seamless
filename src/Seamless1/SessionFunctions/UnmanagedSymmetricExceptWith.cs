using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;

public readonly  struct UnmanagedSymmetricExceptWith<TKey,TValue>:
    ISessionFunctions<TKey,TValue,EqualityEmpty,EqualityEmpty,EqualityEmpty>,
    ICreate<UnmanagedSymmetricExceptWith<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>{
    public static UnmanagedSymmetricExceptWith<TKey,TValue> Create()=>new();

    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref EqualityEmpty dst,ref ReadInfo readInfo){
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref EqualityEmpty dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref EqualityEmpty output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref EqualityEmpty output,ref UpsertInfo upsertInfo,WriteReason reason){
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref EqualityEmpty output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    public bool NeedInitialUpdate(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,ref RMWInfo rmwInfo)=>true;

    public bool InitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref EqualityEmpty output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        value=default;
        return true;
    }

    public void PostInitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref EqualityEmpty output,ref RMWInfo rmwInfo){
    }

    public bool NeedCopyUpdate(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref EqualityEmpty output,ref RMWInfo rmwInfo)=>true;

    public bool CopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref EqualityEmpty output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }

    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref EqualityEmpty output,ref RMWInfo rmwInfo){
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }

    public bool InPlaceUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref EqualityEmpty output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }

    public int GetRMWModifiedValueLength(ref TValue value,ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();
    public int GetRMWInitialValueLength(ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();
    public int GetUpsertValueLength(ref TValue value,ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();

    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }

    public void RMWCompletionCallback(ref TKey key,ref EqualityEmpty input,ref EqualityEmpty output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }

    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        // Tsavorite Delete API は使用しない。RMW による Tombstone 挿入で削除する。
        throw new NotSupportedException();
    }

    public void ConvertOutputToHeap(ref EqualityEmpty input,ref EqualityEmpty output){
    }
}
