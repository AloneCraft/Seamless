using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;
using Tsavorite.core;

#if TSAVORITE2_0BETA3
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
            return false;
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
public abstract class ScanIteratorFunctions<T,TSessionKey,TSessionValue>:IScanIteratorFunctions{
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
            return false;
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
#elif TSAVORITE1_1_1
public abstract class ScanIteratorFunctions<T>:IScanIteratorFunctions<SpanByte, SpanByte>{
    public readonly T[] Chunk=new T[Const.Chunk_Length];
    public int Count;
    private protected abstract T Deserialize(ref SpanByte key,ref SpanByte value);
    public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult){
        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
        var Count=this.Count;
        this.Chunk[Count]=this.Deserialize(ref key,ref value);
        Count++;
        this.Count=Count;
        if(Count==Const.Chunk_Length){
            cursorRecordResult=CursorRecordResult.EndBatch;
            return false;
        } else{
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
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
public abstract class ScanIteratorFunctions<T,TSessionKey,TSessionValue>:IScanIteratorFunctions<TSessionKey,TSessionValue>{
    public readonly T[] Chunk=new T[Const.Chunk_Length];
    public int Count;
    private protected abstract T Deserialize(ref TSessionKey key,ref TSessionValue value);
    public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult){
        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
        var Count=this.Count;
        this.Chunk[Count]=this.Deserialize(ref key,ref value);
        Count++;
        this.Count=Count;
        if(Count==Const.Chunk_Length){
            cursorRecordResult=CursorRecordResult.EndBatch;
            return false;
        } else{
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
    }
    public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
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
#endif
