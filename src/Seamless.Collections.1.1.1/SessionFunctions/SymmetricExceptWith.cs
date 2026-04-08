using System.Diagnostics.CodeAnalysis;
namespace Seamless.SessionFunctions;

internal readonly struct SymmetricExceptWith:
	ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Seamless.Collections.Empty>
{
    [ExcludeFromCodeCoverage]public bool SingleReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool ConcurrentReader(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Dst,ref ReadInfo ReadInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Read");
    }
    [ExcludeFromCodeCoverage]public bool SingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason,ref RecordInfo RecordInfo) {
        throw new NotSupportedException("Upsert");
    }
	[ExcludeFromCodeCoverage]public bool ConcurrentWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,ref RecordInfo RecordInfo) {
        throw new NotSupportedException("Upsert");
	}
    [ExcludeFromCodeCoverage]public void PostSingleWriter(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref SpanByte Dst,ref SpanByteAndMemory Output,ref UpsertInfo UpsertInfo,WriteReason Reason){
        throw new NotSupportedException("Upsert");
    }
    [ExcludeFromCodeCoverage]public void PostUpsertOperation<TEpochAccessor>(ref SpanByte Key,ref SpanByte Input,ref SpanByte Src,ref UpsertInfo UpsertInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Upsert");
    }
	public bool NeedInitialUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo){
        return true;// キー未存在時は新規作成する
    }
    public bool InitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
        //NeedInitialUpdateがtrueを返すときに呼ばれる
		RmwInfo.Action = RMWAction.Default;
		Input.CopyTo(ref Value);
		return true;
	}
    public bool InPlaceUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo) {
		RmwInfo.Action=RMWAction.ExpireAndStop;//キーが存在する場合は削除する。SymmetricExceptWithなので、更新はせずに削除のみ行う。
		return false;
	}
    public void PostInitialUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo){

    }
	public bool NeedCopyUpdate(ref SpanByte Key,ref SpanByte Input,ref SpanByte Value,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo) => true;
	public bool CopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo,ref RecordInfo RecordInfo){
		RmwInfo.Action=RMWAction.ExpireAndStop;
		return false;
	}
    [ExcludeFromCodeCoverage]public bool PostCopyUpdater(ref SpanByte Key,ref SpanByte Input,ref SpanByte OldValue,ref SpanByte NewValue,ref SpanByteAndMemory Output,ref RMWInfo RmwInfo){
        throw new NotSupportedException("CopyUpdaterがfalseを返すのでここは呼ばれない");
	}
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        //RMWが成功しても失敗しても、両方のケースで呼ばれる。SymmetricExceptWith では RMW の結果に関わらず何もしない。
    }
    public void RMWCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Collections.Empty Ctx,Status Status,RecordMetadata RecordMetadata){
        //CompletePendingWithOutputs
    }
    [ExcludeFromCodeCoverage]public bool SingleDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostSingleDeleter(ref SpanByte Key,ref DeleteInfo DeleteInfo){
        throw new NotSupportedException("Delete");
    }
	[ExcludeFromCodeCoverage]public bool ConcurrentDeleter(ref SpanByte Key,ref SpanByte Value,ref DeleteInfo DeleteInfo,ref RecordInfo RecordInfo){
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void PostDeleteOperation<TEpochAccessor>(ref SpanByte Key,ref DeleteInfo DeleteInfo,TEpochAccessor Epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotSupportedException("Delete");
    }
    [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref SpanByte Key,ref SpanByte Input,ref SpanByteAndMemory Output,Collections.Empty Ctx,Status Status,RecordMetadata RecordMetadata){
        throw new NotSupportedException("Read非同期で呼ばれる");
    }
	public int GetRMWModifiedValueLength(ref SpanByte Value,ref SpanByte Input){
        return Value.Length+4;
    }
	public int GetRMWInitialValueLength(ref SpanByte Input){
        return Input.Length+4;
    }
    [ExcludeFromCodeCoverage]public int GetUpsertValueLength(ref SpanByte Value,ref SpanByte Input){
        throw new NotSupportedException("Upsert");
    }
    public void ConvertOutputToHeap(ref SpanByte Input,ref SpanByteAndMemory Output){
    }
}
