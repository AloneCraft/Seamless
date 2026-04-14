using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;

public abstract class ScanIteratorFunctions<T, TSessionKey, TSessionValue>:IScanIteratorFunctions<TSessionKey,TSessionValue> {
    public readonly T[] Chunk = new T[Const.Chunk_Length];
    public int Count;
    private protected abstract T Deserialize(ref TSessionKey key,ref TSessionValue value);
    public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
        var Count = this.Count;
        this.Chunk[Count]=this.Deserialize(ref key,ref value);
        Count++;
        this.Count=Count;
        if(Count==Const.Chunk_Length) {
            cursorRecordResult=CursorRecordResult.EndBatch;
            return true;
        }
        else {
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
    }
    public bool OnStart(long beginAddress,long endAddress) {
        this.Count=0;
        return true;
    }
    public void OnStop(bool completed,long numberOfRecords) {
    }
    public void OnException(Exception exception,long numberOfRecords) {
    }
}
