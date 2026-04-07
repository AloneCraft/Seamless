#if TSAVORITE1_1_1
using System.Diagnostics;
using Tsavorite.core;

namespace Seamless.ScanIteratorFunctions;

/// <summary>
/// 固定長 BlittableAllocator 用スキャンイテレータ基底クラス（TSAVORITE1_1_1 専用）。
/// チャンク単位でバッファリングし、全件を List に蓄積しない。
/// </summary>
public abstract class UnmanagedScanIteratorFunctions<TKey, TValue, T> : IScanIteratorFunctions<TKey, TValue>
    where TKey : unmanaged
    where TValue : unmanaged
{
    public readonly T[] Chunk = new T[Const.Chunk_Length];
    public int Count;

    private protected abstract T Deserialize(ref TKey key, ref TValue value);

    public bool SingleReader(ref TKey key, ref TValue value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
    {
        Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
        var count = this.Count;
        this.Chunk[count] = this.Deserialize(ref key, ref value);
        count++;
        this.Count = count;
        if (count == Const.Chunk_Length)
        {
            cursorRecordResult = CursorRecordResult.EndBatch;
            return false;
        }
        cursorRecordResult = CursorRecordResult.Accept;
        return true;
    }

    public bool ConcurrentReader(ref TKey key, ref TValue value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult)
        => this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);

    public bool OnStart(long beginAddress, long endAddress)
    {
        this.Count = 0;
        return true;
    }

    public void OnStop(bool completed, long numberOfRecords) { }
    public void OnException(Exception exception, long numberOfRecords) { }
}
#endif
