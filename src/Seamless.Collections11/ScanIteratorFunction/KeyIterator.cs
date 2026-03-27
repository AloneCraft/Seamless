namespace Seamless.Collections;

using MemoryPack;
using Tsavorite.core;
internal class KeyIterator<TKey>:ScanIteratorFunctions<TKey> {
    private protected override TKey Deserialize(ref SpanByte key,ref SpanByte value) =>
        MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
}

//internal class KeyIterator<TKey>:IScanIteratorFunctions<SpanByte, SpanByte>{
//	public TKey[] Chunk=new TKey[Const.Chunk_Length];
//	public int Count;

//	public bool SingleReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult){
//        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
//		var Count=this.Count;
//        var Key=MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
//        this.Chunk[Count++]=Key;
//        this.Count=Count;
//        if(Count==Const.Chunk_Length){
//            cursorRecordResult=CursorRecordResult.EndBatch;
//            return false;
//        }else{
//            cursorRecordResult=CursorRecordResult.Accept;
//            return true;
//        }
//    }

//	public bool ConcurrentReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult){
//		return this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);
//	}

//	public bool OnStart(long beginAddress,long endAddress){
//		this.Count=0;
//		return true;
//	}
//	public void OnException(Exception exception,long numberOfRecords){}
//    public void OnStop(bool completed,long numberOfRecords){

//    }
//}
