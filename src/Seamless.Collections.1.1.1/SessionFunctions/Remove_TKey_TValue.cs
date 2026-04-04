namespace Seamless.SessionFunctions;
using System.Buffers;
using Tsavorite.core;
/// <summary>
/// RMWで値の読み取りと削除を同時に行うセッション関数。
/// キーが存在する場合は値をOutputにコピーしてからExpireAndStopで論理削除する。
/// キーが存在しない場合はNeedInitialUpdate=falseでNotFoundを返す。
/// RMW
///     if(FoundMutable){
///         InPlaceUpdater → 値をOutputにコピーし、ExpireAndStopで論理削除
///     }else if(FoundReadOnly || FoundDisk){
///         NeedCopyUpdate → true
///         CopyUpdater → 値をOutputにコピーし、ExpireAndStopで論理削除
///     }else{
///         NeedInitialUpdate → false（キー未存在なので何もしない）
///     }
/// </summary>
internal readonly struct Remove_TKey_TValue():ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Collections.Empty>{
	private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
	public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo)=>throw new NotSupportedException("RMWは呼ばれない");
	public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo)=>throw new NotSupportedException("RMWは呼ばれない");
	public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
		throw new NotSupportedException("Readの非同期I/O完了時に呼ばれるので呼ばれない");
	}
	public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo){
		throw new NotSupportedException("RMWは呼ばれない");
	}
	public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason){
		throw new NotSupportedException("RMWは呼ばれない");
	}
	public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo){
		throw new NotSupportedException("RMWは呼ばれない");
	}
	public void PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
		throw new NotSupportedException("RMWは呼ばれない");
	}
	// キー未存在時は新規作成しない
	public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		return false;
	}
	public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
		throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	}
	public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		throw new NotSupportedException("InitialUpdaterが呼ばれないので呼ばれない");
	}
	public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		throw new NotSupportedException("CopyUpdaterでRMWAction.ExpireAndStop&&falseが返されるので呼ばれない");
	}
	// キー存在時（Mutable）は値をOutputにコピーしてからExpireAndStopで論理削除
	public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
		output.CopyFrom(value.AsReadOnlySpan(),this.memoryPool);//RemoveなのでoldValueをコピー
		rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	// キー存在時（ReadOnly/Disk）はコピー更新を許可して値を読み取る
	public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		return true;
	}
	public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
		output.CopyFrom(oldValue.AsReadOnlySpan(),this.memoryPool);//RemoveなのでoldValueをコピー
        rmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
	public int GetRMWInitialValueLength(ref SpanByte input){
		throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	}
	public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input){
		throw new NotSupportedException("RMWは呼ばれない");
	}
	public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
		//RMW 確定後に呼ばれる。
	}
	public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
		//CompletePendingWithOutputs
	}
	public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
		throw new NotSupportedException();
	}
	public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
		throw new NotSupportedException();
	}
	public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
		throw new NotSupportedException();
	}
	public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
		where TEpochAccessor:IEpochAccessor{
		throw new NotSupportedException();
	}
	public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output){ }
}
