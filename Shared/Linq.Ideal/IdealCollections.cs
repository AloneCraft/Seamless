using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Linq.Ideal;

/// <summary>IGrouping の最小実装。要素は共有配列の区間を指す(結果ダイジェストは Key と要素列で正規化される)。</summary>
public sealed class IdealGrouping<TKey,TElement>(TKey key,TElement[] elements,int start,int count):IGrouping<TKey,TElement> {
    public TKey Key => key;

    public IEnumerator<TElement> GetEnumerator() {
        for(var index = 0;index<count;index++) {
            yield return elements[start+index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}

/// <summary>グループ化・結合の補助。キー列をグループ番号へ振り分け、要素をグループ順へ安定に並べる。</summary>
internal static class IdealGroups {
    /// <summary>
    /// keys[i] のグループ番号を groupOf[i] へ、初出順のキーを distinctKeys へ、各グループの件数を counts へ入れ、グループ数を返す。
    /// map にはキーからグループ番号への対応が残る(結合の探索に使う)。
    /// </summary>
    public static int Assign<TKey>(Dictionary<TKey,int> map,ReadOnlySpan<TKey> keys,Span<int> groupOf,Span<TKey> distinctKeys,Span<int> counts) where TKey : notnull {
        var groupCount = 0;
        for(var index = 0;index<keys.Length;index++) {
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(map,keys[index],out var exists);
            if(!exists) {
                slot=groupCount;
                distinctKeys[groupCount]=keys[index];
                counts[groupCount]=0;
                groupCount++;
            }
            groupOf[index]=slot;
            counts[slot]++;
        }
        return groupCount;
    }

    /// <summary>グループ番号の順に要素を並べる(グループ内は元の順)。starts には各グループの先頭位置が入る。cursor は作業用。</summary>
    public static void Scatter<TElement>(ReadOnlySpan<int> groupOf,ReadOnlySpan<TElement> elements,ReadOnlySpan<int> counts,int groupCount,Span<int> starts,Span<int> cursor,Span<TElement> sorted) {
        var offset = 0;
        for(var group = 0;group<groupCount;group++) {
            starts[group]=offset;
            cursor[group]=offset;
            offset+=counts[group];
        }
        for(var index = 0;index<groupOf.Length;index++) {
            sorted[cursor[groupOf[index]]++]=elements[index];
        }
    }
}

/// <summary>
/// グループ化の作業領域。キー列と要素列を受け取り、グループ番号付けと安定な振り分けを行う。
/// 一時配列は ArrayPool から借り、Dispose で返す。結果の要素配列(Sorted)は呼び出し側が用意する。
/// </summary>
internal sealed class PooledGroups<TKey,TElement>:IDisposable where TKey : notnull {
    public readonly TKey[] Keys;
    public readonly TElement[] Elements;
    public readonly int[] Counts;
    public readonly int[] Starts;
    public readonly TKey[] Distinct;
    public readonly Dictionary<TKey,int> Map;
    private readonly int[] groupOf;
    private readonly int[] cursor;
    public int GroupCount;

    public PooledGroups(int length) {
        this.Keys=System.Buffers.ArrayPool<TKey>.Shared.Rent(length);
        this.Elements=System.Buffers.ArrayPool<TElement>.Shared.Rent(length);
        this.Counts=System.Buffers.ArrayPool<int>.Shared.Rent(length);
        this.Starts=System.Buffers.ArrayPool<int>.Shared.Rent(length);
        this.Distinct=System.Buffers.ArrayPool<TKey>.Shared.Rent(length);
        this.groupOf=System.Buffers.ArrayPool<int>.Shared.Rent(length);
        this.cursor=System.Buffers.ArrayPool<int>.Shared.Rent(length);
        this.Map=new Dictionary<TKey,int>(length);
        this.GroupCount=0;
    }

    /// <summary>Keys/Elements の先頭 length 件をグループ化し、sorted へグループ順に並べる。</summary>
    public void Group(int length,Span<TElement> sorted) {
        this.GroupCount=IdealGroups.Assign(this.Map,this.Keys.AsSpan(0,length),this.groupOf,this.Distinct,this.Counts);
        IdealGroups.Scatter(this.groupOf.AsSpan(0,length),this.Elements.AsSpan(0,length),this.Counts,this.GroupCount,this.Starts,this.cursor,sorted);
    }

    public void Dispose() {
        System.Buffers.ArrayPool<TKey>.Shared.Return(this.Keys);
        System.Buffers.ArrayPool<TElement>.Shared.Return(this.Elements);
        System.Buffers.ArrayPool<int>.Shared.Return(this.Counts);
        System.Buffers.ArrayPool<int>.Shared.Return(this.Starts);
        System.Buffers.ArrayPool<TKey>.Shared.Return(this.Distinct);
        System.Buffers.ArrayPool<int>.Shared.Return(this.groupOf);
        System.Buffers.ArrayPool<int>.Shared.Return(this.cursor);
    }
}

/// <summary>長さの分からない列を ArrayPool の配列へ溜める。足りなくなったら倍の配列へ移す。</summary>
internal sealed class PooledBuffer<TItem>(int capacity):IDisposable {
    private TItem[] items = System.Buffers.ArrayPool<TItem>.Shared.Rent(capacity);

    public int Count { get; private set; }

    public ReadOnlySpan<TItem> Span => this.items.AsSpan(0,this.Count);

    public void Add(TItem item) {
        if(this.Count==this.items.Length) {
            var next = System.Buffers.ArrayPool<TItem>.Shared.Rent(this.items.Length*2);
            this.items.AsSpan(0,this.Count).CopyTo(next);
            System.Buffers.ArrayPool<TItem>.Shared.Return(this.items);
            this.items=next;
        }
        this.items[this.Count++]=item;
    }

    public void Dispose() => System.Buffers.ArrayPool<TItem>.Shared.Return(this.items);
}

/// <summary>値と元の位置。安定ソートのための組。</summary>
internal readonly record struct Indexed<T>(T Value,int Index);

/// <summary>昇順の安定比較(同値は元の位置の順)。</summary>
internal readonly struct AscendingStable<T>:IComparer<Indexed<T>> {
    public int Compare(Indexed<T> left,Indexed<T> right) {
        var result = Comparer<T>.Default.Compare(left.Value,right.Value);
        if(result!=0) {
            return result;
        }
        return left.Index-right.Index;
    }
}

/// <summary>降順の安定比較(同値は元の位置の順)。</summary>
internal readonly struct DescendingStable<T>:IComparer<Indexed<T>> {
    public int Compare(Indexed<T> left,Indexed<T> right) {
        var result = Comparer<T>.Default.Compare(right.Value,left.Value);
        if(result!=0) {
            return result;
        }
        return left.Index-right.Index;
    }
}

/// <summary>int 列のベクトル化した最小・最大。</summary>
internal static class IdealInt32 {
    public static int Min(ReadOnlySpan<int> span) {
        ref var first = ref MemoryMarshal.GetReference(span);
        var index = 0;
        var result = span[0];
        if(Vector.IsHardwareAccelerated&&span.Length>=Vector<int>.Count*2) {
            var best = Vector.LoadUnsafe(ref first);
            index=Vector<int>.Count;
            var last = span.Length-Vector<int>.Count;
            for(;index<=last;index+=Vector<int>.Count) {
                best=Vector.Min(best,Vector.LoadUnsafe(ref first,(nuint)index));
            }
            result=best[0];
            for(var lane = 1;lane<Vector<int>.Count;lane++) {
                if(best[lane]<result) {
                    result=best[lane];
                }
            }
        }
        for(;index<span.Length;index++) {
            if(Unsafe.Add(ref first,index)<result) {
                result=Unsafe.Add(ref first,index);
            }
        }
        return result;
    }

    public static int Max(ReadOnlySpan<int> span) {
        ref var first = ref MemoryMarshal.GetReference(span);
        var index = 0;
        var result = span[0];
        if(Vector.IsHardwareAccelerated&&span.Length>=Vector<int>.Count*2) {
            var best = Vector.LoadUnsafe(ref first);
            index=Vector<int>.Count;
            var last = span.Length-Vector<int>.Count;
            for(;index<=last;index+=Vector<int>.Count) {
                best=Vector.Max(best,Vector.LoadUnsafe(ref first,(nuint)index));
            }
            result=best[0];
            for(var lane = 1;lane<Vector<int>.Count;lane++) {
                if(best[lane]>result) {
                    result=best[lane];
                }
            }
        }
        for(;index<span.Length;index++) {
            if(Unsafe.Add(ref first,index)>result) {
                result=Unsafe.Add(ref first,index);
            }
        }
        return result;
    }
}

internal static class IdealErrors {
    public static InvalidOperationException NoElements() => new("Sequence contains no elements");
    public static InvalidOperationException NoMatch() => new("Sequence contains no matching element");
    public static InvalidOperationException MoreThanOneElement() => new("Sequence contains more than one element");
    public static InvalidOperationException MoreThanOneMatch() => new("Sequence contains more than one matching element");
    public static ArgumentOutOfRangeException IndexOutOfRange() => new("index");
}
