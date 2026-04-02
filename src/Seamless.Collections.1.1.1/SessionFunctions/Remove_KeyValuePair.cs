namespace Seamless.SessionFunctions;
using System.Buffers;
using Tsavorite.core;
/// <summary>
/// RMWでKey,Value両方が一致するエントリのみ削除するセッション関数。
/// Inputにシリアライズ済みのValue bytesを渡し、ストア上のValueSpanとバイト比較する。
/// 一致すればOutputにマーカー(1byte)を書き込みExpireAndStopで論理削除。
/// 不一致またはキー未存在ならOutputは空のまま。
/// RMW
///     if(FoundMutable){
///         InPlaceUpdater → ValueSpan==Input ? マーカー出力+ExpireAndStop : 何もしない(false)
///     }else if(FoundReadOnly || FoundDisk){
///         NeedCopyUpdate → ValueSpan==Input ? true : false
///         CopyUpdater → マーカー出力+ExpireAndStop
///     }else{
///         NeedInitialUpdate → false（キー未存在なので何もしない）
///     }
/// </summary>
internal readonly struct Remove_KeyValuePair():ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Collections.Empty>{
	private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
	private void WriteMarker(ref SpanByteAndMemory output) {
		output.Memory=this.memoryPool.Rent(1);
		output.Length=1;
		output.Memory.Memory.Span[0]=1;
	}
    public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo)=>throw new NotSupportedException("RMWは呼ばれない");
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo)=>throw new NotSupportedException("RMWは呼ばれない");
    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
        throw new NotSupportedException("Read の非同期I/O完了時に呼ばれるので呼ばれない");
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
        throw new NotImplementedException();
    }
    // キー存在時（Mutable）はValueが一致する場合のみExpireAndStopで論理削除
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        if(value.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan())) {
            this.WriteMarker(ref output);
            rmwInfo.Action=RMWAction.ExpireAndStop;
        }
        return false;
    }
	// キー存在時（ReadOnly/Disk）はValueが一致する場合のみコピー更新を許可
	public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
		return oldValue.AsReadOnlySpan().SequenceEqual(input.AsReadOnlySpan());
	}
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        this.WriteMarker(ref output);
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
 //   [ExcludeFromCodeCoverage]public bool PostCopyUpdater<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref LogRecord dstLogRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo)where TSourceLogRecord : ISourceLogRecord {
 //       throw new NotSupportedException("CopyUpdaterでRMWAction.ExpireAndStop&&falseが返されるので呼ばれない");
	//}
	////Delete (not used)
 //   [ExcludeFromCodeCoverage]public void PostDeleteOperation<TKey, TEpochAccessor>(TKey key,ref DeleteInfo deleteInfo,TEpochAccessor epochAccessor)where TKey : IKey, allows ref struct where TEpochAccessor : IEpochAccessor {
 //       throw new NotSupportedException("RMWは呼ばれない");
	//}
 //   [ExcludeFromCodeCoverage]public void ReadCompletionCallback(ref DiskLogRecord diskLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
 //       throw new NotSupportedException("Read の非同期I/O完了時に呼ばれるので呼ばれない");
 //   }
	public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
    public int GetRMWInitialValueLength(ref SpanByte input){
        throw new NotImplementedException();
    }
    public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input){
        throw new NotImplementedException();
    }
    public void PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        //RMW 確定後に呼ばれる。
    }
    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Collections.Empty ctx,Status status,RecordMetadata recordMetadata){
    }
    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException();
    }
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo){
        throw new NotImplementedException();
    }
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotImplementedException();
    }
    public void PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch)
        where TEpochAccessor:IEpochAccessor{
        throw new NotImplementedException();
    }
    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output){ }
 //   [ExcludeFromCodeCoverage]public RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref PinnedSpanByte input)
	//	where TKey : IKey, allows ref struct {
 //       throw new NotSupportedException("NeedInitialUpdateがfalseを返すので呼ばれない");
	//}
 //   [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,ReadOnlySpan<byte> value,ref PinnedSpanByte input)
	//	where TKey : IKey, allows ref struct {
	//	throw new NotSupportedException();
	//}
 //   [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey>(TKey key,IHeapObject value,ref PinnedSpanByte input)
	//	where TKey : IKey, allows ref struct {
	//	throw new NotSupportedException();
	//}
 //   [ExcludeFromCodeCoverage]public RecordFieldInfo GetUpsertFieldInfo<TKey, TSourceLogRecord>(TKey key,in TSourceLogRecord inputLogRecord,ref PinnedSpanByte input)
	//	where TKey : IKey, allows ref struct
	//	where TSourceLogRecord : ISourceLogRecord {
	//	throw new NotSupportedException();
	//}
 //   public void ConvertOutputToHeap(ref PinnedSpanByte input,ref SpanByteAndMemory output){

 //   }
}
