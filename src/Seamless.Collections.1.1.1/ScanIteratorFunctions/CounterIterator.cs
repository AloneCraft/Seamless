namespace Seamless.ScanIteratorFunctions;
/// <summary>
/// IteratorLookup(ref CounterIterator) で使う構造体。スキャンしたレコード数を数えるだけ。
/// refで渡しているのでstructで問題なさそうだが実際にはclassで無いとだめ。実装の問題か？
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class CounterIterator<TKey,TValue>:CounterIteratorBase,IScanIteratorFunctions<TKey,TValue>{
    public bool SingleReader(ref TKey key,ref TValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        cursorRecordResult=CursorRecordResult.Accept;
        return true;
    }
    public bool ConcurrentReader(ref TKey key,ref TValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
        cursorRecordResult=CursorRecordResult.Accept;
        return true;
    }
}
