using System.Diagnostics;
using System.Threading;
namespace Seamless.ScanIteratorFunctions;

public abstract class ScanIteratorFunctions<T, TSessionKey, TSessionValue>:IScanIteratorFunctions<TSessionKey,TSessionValue> {
    public readonly T[] Chunk = new T[Const.Chunk_Length];
    public int Count;
    private protected abstract T Deserialize(ref TSessionKey key,ref TSessionValue value);
    public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
        var index = Interlocked.Increment(ref this.Count)-1;//InterlockedはPending非同期がConcurrentで呼ばれるため。v2 で Interlocked が不要な理由は「class インスタンスがコピーされる」からではなく、Reader が常に単一スレッドから順次呼ばれる設計になったからです。
        if(index>=Const.Chunk_Length) {
            Volatile.Write(ref this.Count,Const.Chunk_Length);
            cursorRecordResult=CursorRecordResult.EndBatch;
            return true;
        }
        this.Chunk[index]=this.Deserialize(ref key,ref value);
        if(index+1==Const.Chunk_Length) {
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
        Volatile.Write(ref this.Count,0);
        return true;
    }
    public void OnStop(bool completed,long numberOfRecords) {
    }
    public void OnException(Exception exception,long numberOfRecords) {
    }
}
