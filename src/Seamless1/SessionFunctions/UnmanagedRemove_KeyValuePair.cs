using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;


namespace Seamless.SessionFunctions;

public readonly  struct UnmanagedRemove_KeyValuePair<TKey,TValue>:
    ISessionFunctions<TKey,TValue,TValue,bool,EqualityEmpty>
    where TKey:unmanaged
    where TValue:unmanaged{

    [ExcludeFromCodeCoverage]
    public bool SingleReader(ref TKey key,ref TValue input,ref TValue value,ref bool dst,ref ReadInfo readInfo){
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentReader(ref TKey key,ref TValue input,ref TValue value,ref bool dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void ReadCompletionCallback(ref TKey key,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
        // Read 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool SingleWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,WriteReason reason){
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentWriter(ref TKey key,ref TValue input,ref TValue src,ref TValue dst,ref bool output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref TValue input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        // Upsert 操作は使用しない。
        throw new NotSupportedException();
    }

    public bool NeedInitialUpdate(ref TKey key,ref TValue input,ref bool output,ref RMWInfo rmwInfo)=>false;

    [ExcludeFromCodeCoverage]
    public bool InitialUpdater(ref TKey key,ref TValue input,ref TValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        // NeedInitialUpdate が false を返すため到達しない。
        throw new NotSupportedException();
    }

    [ExcludeFromCodeCoverage]
    public void PostInitialUpdater(ref TKey key,ref TValue input,ref TValue value,ref bool output,ref RMWInfo rmwInfo){
        // NeedInitialUpdate が false のため InitialUpdater も呼ばれず、到達しない。
        throw new NotSupportedException();
    }

    // 値が一致する場合のみ CopyUpdater に進む
    public bool NeedCopyUpdate(ref TKey key,ref TValue input,ref TValue oldValue,ref bool output,ref RMWInfo rmwInfo)=>oldValue.Equals(input);

    public bool CopyUpdater(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue newValue,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output=true;
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }

    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref TValue input,ref TValue oldValue,ref TValue newValue,ref bool output,ref RMWInfo rmwInfo){
        // CopyUpdater が false を返して処理を打ち切るため、到達しない。
        throw new NotSupportedException();
    }

    // 値が一致する場合のみ Tombstone を挿入して論理削除
    public bool InPlaceUpdater(ref TKey key,ref TValue input,ref TValue value,ref bool output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        if(value.Equals(input)){
            output=true;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
    }

    public int GetRMWModifiedValueLength(ref TValue value,ref TValue input)=>Unsafe.SizeOf<TValue>();
    public int GetRMWInitialValueLength(ref TValue input)=>Unsafe.SizeOf<TValue>();
    public int GetUpsertValueLength(ref TValue value,ref TValue input)=>Unsafe.SizeOf<TValue>();

    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref TValue input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }

    public void RMWCompletionCallback(ref TKey key,ref TValue input,ref bool output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
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

    public void ConvertOutputToHeap(ref TValue input,ref bool output){
    }
}
