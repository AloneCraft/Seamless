using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;
//todo 将来struct
public abstract class ScanIteratorFunctions<T>:IScanIteratorFunctions{
    public readonly T[] Chunk=new T[Const.Chunk_Length];
    public int Count;
    private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) where TSourceLogRecord:ISourceLogRecord;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) where TSourceLogRecord:ISourceLogRecord{
        Debug.Assert(!logRecord.Info.Tombstone);
        var Count=this.Count;
        // EndBatch返却後にConditionalScanPush経由(pending IO完了)でOnStartを経ずに
        // Readerが再呼出されることがある。Count>=Chunk_LengthならEnumeratorが
        // 既にChunk[0..Count-1]を読み終えていないため、このレコードは無視してEndBatchを返す。
        if(Count>=Const.Chunk_Length){
            cursorRecordResult=CursorRecordResult.EndBatch;
            return true;
        }
        this.Chunk[Count]=this.Deserialize(logRecord);
        Count++;
        this.Count=Count;
        if(Count==Const.Chunk_Length){
            cursorRecordResult=CursorRecordResult.EndBatch;
        }else{
            cursorRecordResult=CursorRecordResult.Accept;
        }
        return true;
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
