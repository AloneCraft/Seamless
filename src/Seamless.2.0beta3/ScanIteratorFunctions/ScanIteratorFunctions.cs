using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;
//AIへの指示:UnmanagedCollection,ManagedCollectionで使っている
//2.0その為Collection.csに移動させる
public abstract class ScanIteratorFunctions<T>:IScanIteratorFunctions{
    public readonly T[] Chunk=new T[Const.Chunk_Length];
    public int Count;
    private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord)where TSourceLogRecord:ISourceLogRecord;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)where TSourceLogRecord:ISourceLogRecord{
        Debug.Assert(!logRecord.Info.Tombstone);
        var Count=this.Count;
        this.Chunk[Count]=this.Deserialize(logRecord);
        Count++;
        this.Count=Count;
        if(Count==Const.Chunk_Length){
            cursorRecordResult=CursorRecordResult.EndBatch;
            return true;
        }else{
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    public bool OnStart(long beginAddress,long endAddress){
        this.Count=0;
        return true;
    }
    public void OnStop(bool completed,long numberOfRecords){
    }
    public void OnException(Exception exception,long numberOfRecords){
    }
}