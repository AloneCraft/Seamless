namespace Seamless.ScanIteratorFunctions;

using System.Diagnostics;

using Tsavorite.core;
#if TSAVORITE2_0BETA3
public class CounterIterator:IScanIteratorFunctions {
    public long Counter;
    private long Counter0;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) where TSourceLogRecord : ISourceLogRecord {
        Debug.Assert(!sourceLogRecord.Info.Tombstone);
        cursorRecordResult=CursorRecordResult.Accept;
        this.Counter0++;
        return true;
    }
    public bool OnStart(long beginAddress,long endAddress) {
        return true;
    }
    public void OnException(Exception exception,long numberOfRecords) { }
    public void OnStop(bool completed,long numberOfRecords) {
        this.Counter=numberOfRecords;
        Debug.Assert(this.Counter0==numberOfRecords);
    }
}
#elif TSAVORITE1_1_1
public class CounterIterator:IScanIteratorFunctions<SpanByte,SpanByte> {
    public long Counter;
    private long Counter0;
    public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        cursorRecordResult=CursorRecordResult.Accept;
        this.Counter0++;
        return true;
    }
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
    }
    public bool OnStart(long beginAddress,long endAddress) {
        return true;
    }
    public void OnException(Exception exception,long numberOfRecords) { }
    public void OnStop(bool completed,long numberOfRecords) {
        this.Counter=numberOfRecords;
        Debug.Assert(this.Counter0==numberOfRecords);
    }
}
#endif
#if TSAVORITE1_1_1
public class CounterIterator<TKey,TValue>
{
    public long Counter;
    private long Counter0;
    public bool SingleReader(ref TKey key,ref TValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        cursorRecordResult=CursorRecordResult.Accept;
        this.Counter0++;
        return true;
    }
    public bool ConcurrentReader(ref TKey key,ref TValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
    }
    public bool OnStart(long beginAddress,long endAddress) {
        return true;
    }
    public void OnException(Exception exception,long numberOfRecords) { }
    public void OnStop(bool completed,long numberOfRecords) {
        this.Counter=numberOfRecords;
        Debug.Assert(this.Counter0==numberOfRecords);
    }
}
#endif
