namespace Seamless.Collections;

using MemoryPack;
using Tsavorite.core;

internal sealed class ValueIterator<TValue>:ScanIteratorFunctions<TValue> {
    private protected override TValue Deserialize(ref SpanByte key,ref SpanByte value) =>
        MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
}
//internal class ValueIterator<TValue>:IScanIteratorFunctions<SpanByte, SpanByte>{
//	public TValue[] Chunk=new TValue[Const.Chunk_Length];
//	public int Count;

//	public bool SingleReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult){
//		var Count=this.Count;
//        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
//        var Value=MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
//        this.Chunk[Count]=Value;
//        this.Count=Count+1;
//        if(this.Count>=Const.Chunk_Length){
//            cursorRecordResult=CursorRecordResult.EndBatch;
//            return true;
//        }
//        cursorRecordResult=CursorRecordResult.Accept;
//        return true;

//	}

//	public bool ConcurrentReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult){
//		return this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);
//	}

//	public bool OnStart(long beginAddress,long endAddress){
//		this.Count=0;
//		return true;
//	}
//	public void OnException(Exception exception,long numberOfRecords){}
//	public void OnStop(bool completed,long numberOfRecords){}
//}
