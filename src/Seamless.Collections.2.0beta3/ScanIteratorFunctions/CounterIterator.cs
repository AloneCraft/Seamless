namespace Seamless.ScanIteratorFunctions;

using System.Diagnostics;

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
