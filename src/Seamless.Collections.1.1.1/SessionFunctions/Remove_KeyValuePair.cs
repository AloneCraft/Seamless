using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;
using System.Buffers;
/// <summary>
/// RMWでKey,Value両方が一致するエントリのみ削除するセッション関数。
/// </summary>
internal readonly struct Remove_KeyValuePair():
    ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
    {
	private readonly MemoryPool<byte> MemoryPool = MemoryPool<byte>.Shared;
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo)=>
        throw new NotSupportedException("Read");
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo)=>
        throw new NotSupportedException("Read");
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason){
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
	}
	public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;// キー未存在時は新規作成しない
	}
    [ExcludeFromCodeCoverage]public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
		throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	}
	public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
    	// キー存在時（Mutable）は値をOutputにコピーしてからExpireAndStopで論理削除
        if(value.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan())){
            output.Length=1;
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
	}
    [ExcludeFromCodeCoverage]public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		throw new NotSupportedException("InitialUpdaterが呼ばれないので呼ばれない");
	}
	// キー存在時（ReadOnly/Disk）は値が一致したらMutableに削除レコードを追加する
	public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return oldValue.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan());
	}
	public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        output.Length=1;
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
    [ExcludeFromCodeCoverage]
	public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
	}
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
		//RMW 確定後に呼ばれる。
	}
	public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
		//CompletePendingWithOutputs
	}
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
	}
    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata){
		throw new NotSupportedException("Readの非同期I/O完了時に呼ばれるので呼ばれない");
	}
    public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
    [ExcludeFromCodeCoverage]public int GetRMWInitialValueLength(ref SpanByte input){
	    throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	}
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input){
        throw new NotSupportedException("Upsert");
    }
    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { 
        //RMWでPending待ちで削除するとき呼ばれる
    }
}
