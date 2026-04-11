namespace Seamless.ScanIteratorFunctions;
using System.Diagnostics;
/// <summary>
/// IteratorLookup(ref CounterIterator) で使う構造体。スキャンしたレコード数を数えるだけ。
/// refで渡しているのでstructで問題なさそうだが実際にはclassで無いとだめ。実装の問題か？
/// </summary>
public class CounterIterator:CounterIteratorBase,IScanIteratorFunctions{
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) where TSourceLogRecord : ISourceLogRecord {
        Debug.Assert(!sourceLogRecord.Info.Tombstone);
        cursorRecordResult=CursorRecordResult.Accept;
        return true;
    }
}
//public partial struct CounterIterator:IScanIteratorFunctions{
//    public long Counter;
//    public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) where TSourceLogRecord : ISourceLogRecord {
//        Debug.Assert(!sourceLogRecord.Info.Tombstone);
//        cursorRecordResult=CursorRecordResult.Accept;
//        return true;
//    }
//    public bool OnStart(long beginAddress,long endAddress) {
//        return true;
//    }
//    public void OnException(Exception exception,long numberOfRecords) { }
//    public void OnStop(bool completed,long numberOfRecords) {
//        this.Counter=numberOfRecords;
//    }
//}
