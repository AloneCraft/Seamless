using System.Buffers;
using System.Runtime.CompilerServices;

namespace Seamless.Linq.Internal;

public interface ITopKCombinedComparer<TPrimaryKey, TSecondaryKey> {
    int Compare(TPrimaryKey leftPrimary, TSecondaryKey leftSecondary, TPrimaryKey rightPrimary, TSecondaryKey rightSecondary);
}

/// <summary>
/// OrderBy / OrderByDescending + Take(k) のストリーミング Top-K 抽出用バッファ。
/// 全件バッファリングを行わず、k 要素の固定バッファのみで安定ソート（Stable Sort）を維持する。
/// </summary>
public struct TopKBuffer<TElement, TKey, TComparer> : IValueEnumerator<TElement>
    where TComparer : struct, IComparer<TKey> {
    private TElement[]? elements;
    private TKey[]? keys;
    private int count;
    private readonly int k;
    private readonly TComparer comparer;
    private int position;

    public TopKBuffer(int k, TComparer comparer) {
        this.k = Math.Max(0, k);
        this.comparer = comparer;
        this.count = 0;
        this.position = -1;
        if (this.k > 0) {
            this.elements = ArrayPool<TElement>.Shared.Rent(this.k);
            this.keys = ArrayPool<TKey>.Shared.Rent(this.k);
        } else {
            this.elements = null;
            this.keys = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(TElement element, TKey key) {
        if (this.k <= 0) return;
        var keysSpan = this.keys!;
        var elementsSpan = this.elements!;
        if (this.count < this.k) {
            int pos = this.count;
            while (pos > 0 && this.comparer.Compare(key, keysSpan[pos - 1]) < 0) {
                elementsSpan[pos] = elementsSpan[pos - 1];
                keysSpan[pos] = keysSpan[pos - 1];
                pos--;
            }
            elementsSpan[pos] = element;
            keysSpan[pos] = key;
            this.count++;
        } else {
            if (this.comparer.Compare(key, keysSpan[this.k - 1]) >= 0) {
                return;
            }
            int pos = this.k - 1;
            while (pos > 0 && this.comparer.Compare(key, keysSpan[pos - 1]) < 0) {
                elementsSpan[pos] = elementsSpan[pos - 1];
                keysSpan[pos] = keysSpan[pos - 1];
                pos--;
            }
            elementsSpan[pos] = element;
            keysSpan[pos] = key;
        }
    }

    public int Count => this.count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TElement> GetSpan() =>
        this.elements is null || this.count == 0
            ? ReadOnlySpan<TElement>.Empty
            : this.elements.AsSpan(this.position + 1, this.count - (this.position + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNext(out TElement current) {
        if (this.position + 1 < this.count) {
            this.position++;
            current = this.elements![this.position];
            return true;
        }
        current = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNonEnumeratedCount(out int remaining) {
        remaining = this.count - (this.position + 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetSpan(out ReadOnlySpan<TElement> span) {
        span = this.GetSpan();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(scoped Span<TElement> destination, Index offset) {
        var span = this.GetSpan();
        if (span.IsEmpty) return false;
        var (start, length) = GeneratedCodeHelpers.GetClampedOffsetAndLength(new Range(offset, Index.End), span.Length);
        if (length <= 0) return false;
        var toCopy = Math.Min(length, destination.Length);
        span.Slice(start, toCopy).CopyTo(destination);
        return true;
    }

    public void Dispose() {
        if (this.elements is not null) {
            ArrayPool<TElement>.Shared.Return(this.elements, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
            this.elements = null;
        }
        if (this.keys is not null) {
            ArrayPool<TKey>.Shared.Return(this.keys, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
            this.keys = null;
        }
        this.count = 0;
        this.position = -1;
    }
}

/// <summary>
/// OrderBy + ThenBy + Take(k) のストリーミング Top-K 抽出用バッファ（複合キー対応）。
/// </summary>
public struct TopKCombinedBuffer<TElement, TPrimaryKey, TSecondaryKey, TComparer> : IValueEnumerator<TElement>
    where TComparer : struct, ITopKCombinedComparer<TPrimaryKey, TSecondaryKey> {
    private TElement[]? elements;
    private TPrimaryKey[]? primaryKeys;
    private TSecondaryKey[]? secondaryKeys;
    private int count;
    private readonly int k;
    private readonly TComparer comparer;
    private int position;

    public TopKCombinedBuffer(int k, TComparer comparer) {
        this.k = Math.Max(0, k);
        this.comparer = comparer;
        this.count = 0;
        this.position = -1;
        if (this.k > 0) {
            this.elements = ArrayPool<TElement>.Shared.Rent(this.k);
            this.primaryKeys = ArrayPool<TPrimaryKey>.Shared.Rent(this.k);
            this.secondaryKeys = ArrayPool<TSecondaryKey>.Shared.Rent(this.k);
        } else {
            this.elements = null;
            this.primaryKeys = null;
            this.secondaryKeys = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(TElement element, TPrimaryKey primaryKey, TSecondaryKey secondaryKey) {
        if (this.k <= 0) return;
        var pSpan = this.primaryKeys!;
        var sSpan = this.secondaryKeys!;
        var elementsSpan = this.elements!;
        if (this.count < this.k) {
            int pos = this.count;
            while (pos > 0 && this.comparer.Compare(primaryKey, secondaryKey, pSpan[pos - 1], sSpan[pos - 1]) < 0) {
                elementsSpan[pos] = elementsSpan[pos - 1];
                pSpan[pos] = pSpan[pos - 1];
                sSpan[pos] = sSpan[pos - 1];
                pos--;
            }
            elementsSpan[pos] = element;
            pSpan[pos] = primaryKey;
            sSpan[pos] = secondaryKey;
            this.count++;
        } else {
            if (this.comparer.Compare(primaryKey, secondaryKey, pSpan[this.k - 1], sSpan[this.k - 1]) >= 0) {
                return;
            }
            int pos = this.k - 1;
            while (pos > 0 && this.comparer.Compare(primaryKey, secondaryKey, pSpan[pos - 1], sSpan[pos - 1]) < 0) {
                elementsSpan[pos] = elementsSpan[pos - 1];
                pSpan[pos] = pSpan[pos - 1];
                sSpan[pos] = sSpan[pos - 1];
                pos--;
            }
            elementsSpan[pos] = element;
            pSpan[pos] = primaryKey;
            sSpan[pos] = secondaryKey;
        }
    }

    public int Count => this.count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TElement> GetSpan() =>
        this.elements is null || this.count == 0
            ? ReadOnlySpan<TElement>.Empty
            : this.elements.AsSpan(this.position + 1, this.count - (this.position + 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNext(out TElement current) {
        if (this.position + 1 < this.count) {
            this.position++;
            current = this.elements![this.position];
            return true;
        }
        current = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNonEnumeratedCount(out int remaining) {
        remaining = this.count - (this.position + 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetSpan(out ReadOnlySpan<TElement> span) {
        span = this.GetSpan();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(scoped Span<TElement> destination, Index offset) {
        var span = this.GetSpan();
        if (span.IsEmpty) return false;
        var (start, length) = GeneratedCodeHelpers.GetClampedOffsetAndLength(new Range(offset, Index.End), span.Length);
        if (length <= 0) return false;
        var toCopy = Math.Min(length, destination.Length);
        span.Slice(start, toCopy).CopyTo(destination);
        return true;
    }

    public void Dispose() {
        if (this.elements is not null) {
            ArrayPool<TElement>.Shared.Return(this.elements, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
            this.elements = null;
        }
        if (this.primaryKeys is not null) {
            ArrayPool<TPrimaryKey>.Shared.Return(this.primaryKeys, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TPrimaryKey>());
            this.primaryKeys = null;
        }
        if (this.secondaryKeys is not null) {
            ArrayPool<TSecondaryKey>.Shared.Return(this.secondaryKeys, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TSecondaryKey>());
            this.secondaryKeys = null;
        }
        this.count = 0;
        this.position = -1;
    }
}
