namespace Seamless.ScanIteratorFunctions;
public partial class CounterIteratorBase : IScanIteratorFunctions {
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
        where TSourceLogRecord : ISourceLogRecord {
        cursorRecordResult = CursorRecordResult.Accept;
        return true;
    }
}
