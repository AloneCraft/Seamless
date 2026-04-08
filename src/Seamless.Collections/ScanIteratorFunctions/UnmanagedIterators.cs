using System.Diagnostics;
using Tsavorite.core;
namespace Seamless.ScanIteratorFunctions;

internal sealed class UnmanagedCounterIterator<TKey,TValue> : IScanIteratorFunctions<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    public long Counter;
    private long Counter0;

    public bool SingleReader(ref TKey key, ref TValue value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult) {
        cursorRecordResult = CursorRecordResult.Accept; // EndBatch を返さないので打ち切られない
        this.Counter0++;
        return true;
    }
    public bool ConcurrentReader(ref TKey key, ref TValue value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
        => this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);
    public bool OnStart(long beginAddress, long endAddress) => true;
    public void OnException(Exception exception, long numberOfRecords) { }
    public void OnStop(bool completed, long numberOfRecords) {
        this.Counter = numberOfRecords; // Tsavorite が渡す総件数をそのまま使う
        Debug.Assert(this.Counter0 == numberOfRecords);
    }
}

internal sealed class UnmanagedKeyIterator<TKey,TValue>:ScanIteratorFunctions<TKey,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TKey Deserialize(ref TKey key,ref TValue value) => key;
}

internal sealed class UnmanagedValueIterator<TKey,TValue>:ScanIteratorFunctions<TValue,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override TValue Deserialize(ref TKey key,ref TValue value) => value;
}

internal sealed class UnmanagedKeyValueIterator<TKey,TValue>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>,TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value) => new(key,value);
}
