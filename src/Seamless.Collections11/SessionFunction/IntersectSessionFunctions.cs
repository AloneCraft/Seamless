namespace Seamless.Collections;

using Tsavorite.core;

internal readonly struct IntersectSessionFunctions:ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty> {
	public bool SingleReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo) => true;
	public bool ConcurrentReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo) => true;
	public void ReadCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Empty Ctx,Status Status,RecordMetadata RecordMetadata) { }
	public bool SingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
		Src.CopyTo(ref Dst);
		return true;
	}
	public void PostSingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason) { }
	public bool ConcurrentWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
		Src.CopyTo(ref Dst);
		return true;
	}
	void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch) { }
	public bool NeedInitialUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => false;
	public bool InitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		return false;
	}
	public void PostInitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) { }
	public bool NeedCopyUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => true;
	public bool CopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		RmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	public bool PostCopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => true;
	public bool InPlaceUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		RmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
	public int GetRMWModifiedValueLength(ref SpanByte Value,ref SpanByte Input) => Value.Length;
	public int GetRMWInitialValueLength(ref SpanByte Input) => Input.Length+4;
	public int GetUpsertValueLength(ref SpanByte Value,ref SpanByte Input) => Value.Length+4;
	void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostRMWOperation<TEpochAccessor>(ref SpanByte Key,ref SpanByte Input,ref RMWInfo RmwInfo,TEpochAccessor Epoch) { }
	public void RMWCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Empty Ctx,Status Status,RecordMetadata RecordMetadata) { }
	public bool SingleDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) => true;
	public void PostSingleDeleter(ref SpanByte Key,ref DeleteInfo DeleteInfo) { }
	public bool ConcurrentDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo) => true;
	void ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>.PostDeleteOperation<TEpochAccessor>(ref SpanByte Key,ref DeleteInfo DeleteInfo,TEpochAccessor Epoch) { }
	public void ConvertOutputToHeap(ref SpanByte Input,ref SpanByteAndMemory Output) { }
}
