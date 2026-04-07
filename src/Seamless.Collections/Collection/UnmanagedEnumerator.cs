#if TSAVORITE1_1_1
using System.Collections;
using System.Diagnostics;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Tsavorite.core;

namespace Seamless.Collections;

/// <summary>
/// 固定長 BlittableAllocator 用列挙子（TSAVORITE1_1_1 専用）。
/// チャンク単位でバッファリングし、全件を List に蓄積しない。
/// </summary>
public struct UnmanagedEnumerator<TKey, TValue, TStoreFunctions, TAllocator, TIterator, T>(
    ClientSession<TKey, TValue, TValue, TValue, Empty, UnmanagedAdd<TKey, TValue>, TStoreFunctions, TAllocator> Session,
    TIterator Iterator) : IEnumerator<T>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TStoreFunctions : IStoreFunctions<TKey, TValue>
    where TAllocator : IAllocator<TKey, TValue, TStoreFunctions>
    where TIterator : UnmanagedScanIteratorFunctions<TKey, TValue, T>
{
    private TIterator Iterator = Iterator;
    private long Cursor = 0, Count;
    private int Index = Const.Chunk_Length - 1;

    public T Current => this.Iterator.Chunk[this.Index];
    object? IEnumerator.Current => this.Current;

    public bool MoveNext()
    {
        if (++this.Index < this.Count) return true;
        var r = Session.IterateLookup(ref this.Iterator, ref this.Cursor, resetCursor: false);
        Debug.Assert(!r);
        var count = this.Iterator.Count;
        this.Count = count;
        if (count > 0)
        {
            this.Index = 0;
            return true;
        }
        this.Index = -1;
        return false;
    }

    public void Reset()
    {
        this.Cursor = 0;
        this.Index = Const.Chunk_Length - 1;
    }

    public void Dispose() { }
}
#endif
