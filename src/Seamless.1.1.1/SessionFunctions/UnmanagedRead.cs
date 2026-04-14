using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Seamless.Stores;
namespace Seamless.SessionFunctions;
//public readonly partial struct ManagedRead():
//    ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,EqualityEmpty>{
//    private readonly MemoryPool<byte> memoryPool=MemoryPool<byte>.Shared;
/// <summary>
/// Read(TKey,out TValue) のみをサポートするセッション関数。Add は Upsert と RMW の両方で呼ばれるため、両方の処理を実装する必要がある。
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public readonly struct UnmanagedRead<TKey,TValue>:
    ISessionFunctions<TKey,TValue,EqualityEmpty,TValue,EqualityEmpty>,
    ICreate<UnmanagedRead<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>{
    public static UnmanagedRead<TKey,TValue> Create()=>new();
    public bool SingleReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref TValue dst,ref ReadInfo readInfo){
        dst=value;
        return true;
    }

    public bool ConcurrentReader(ref TKey key,ref EqualityEmpty input,ref TValue value,ref TValue dst,ref ReadInfo readInfo,ref RecordInfo recordInfo){
        dst=value;
        return true;
    }

    public void ReadCompletionCallback(ref TKey key,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }

    public bool SingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        dst=src;
        return true;
    }

    public void PostSingleWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,WriteReason reason){
    }

    public bool ConcurrentWriter(ref TKey key,ref EqualityEmpty input,ref TValue src,ref TValue dst,ref TValue output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        dst=src;
        return true;
    }

    public void PostUpsertOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref TValue src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }

    public bool NeedInitialUpdate(ref TKey key,ref EqualityEmpty input,ref TValue output,ref RMWInfo rmwInfo)=>true;

    public bool InitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("UnmagedReadクラスのReadでのみ使われる");
    }

    public void PostInitialUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo){
    }

    public bool NeedCopyUpdate(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue output,ref RMWInfo rmwInfo)=>false;

    [ExcludeFromCodeCoverage]
    public bool CopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("NeedCopyUpdate が false を返すので呼ばれない");
    }

    [ExcludeFromCodeCoverage]
    public bool PostCopyUpdater(ref TKey key,ref EqualityEmpty input,ref TValue oldValue,ref TValue newValue,ref TValue output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdater が false を返すので呼ばれない");
    }

    public bool InPlaceUpdater(ref TKey key,ref EqualityEmpty input,ref TValue value,ref TValue output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo)=>false;

    public int GetRMWModifiedValueLength(ref TValue value,ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();
    public int GetRMWInitialValueLength(ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();
    public int GetUpsertValueLength(ref TValue value,ref EqualityEmpty input)=>Unsafe.SizeOf<TValue>();

    public void PostRMWOperation<TEpochAccessor>(ref TKey key,ref EqualityEmpty input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
    }

    public void RMWCompletionCallback(ref TKey key,ref EqualityEmpty input,ref TValue output,EqualityEmpty ctx,Status status,RecordMetadata recordMetadata){
    }

    [ExcludeFromCodeCoverage]
    public bool SingleDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Add は Delete を使用しない");
    }

    [ExcludeFromCodeCoverage]
    public void PostSingleDeleter(ref TKey key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Add は Delete を使用しない");
    }

    [ExcludeFromCodeCoverage]
    public bool ConcurrentDeleter(ref TKey key,ref TValue value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Add は Delete を使用しない");
    }

    [ExcludeFromCodeCoverage]
    public void PostDeleteOperation<TEpochAccessor>(ref TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Add は Delete を使用しない");
    }

    public void ConvertOutputToHeap(ref EqualityEmpty input,ref TValue output){
    }
}
