using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Linq.Ideal;

/// <summary>配列・List・IEnumerable の理想ループ版で共有する補助。</summary>
public abstract partial class IdealBase<T> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<int> AsInt32(ReadOnlySpan<T> span) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T,int>(ref MemoryMarshal.GetReference(span)),span.Length);

    private static bool SpanContains(ReadOnlySpan<T> span,T value) {
        if(typeof(T)==typeof(int)) {
            return AsInt32(span).Contains(Unsafe.As<T,int>(ref value));
        }
        foreach(var item in span) {
            if(EqualityComparer<T>.Default.Equals(item,value)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>System.Linq の Min と同じ意味(参照型は null を飛ばし、空なら既定値。値型は空なら例外)。</summary>
    private static T SpanMin(ReadOnlySpan<T> span) {
        if(typeof(T)==typeof(int)) {
            if(span.Length==0) {
                throw IdealErrors.NoElements();
            }
            var result = IdealInt32.Min(AsInt32(span));
            return Unsafe.As<int,T>(ref result);
        }
        var index = 0;
        T value = default!;
        if(default(T) is null) {
            for(;index<span.Length;index++) {
                value=span[index];
                if(value is not null) {
                    break;
                }
            }
            if(index==span.Length) {
                return default!;
            }
        } else {
            if(span.Length==0) {
                throw IdealErrors.NoElements();
            }
            value=span[0];
        }
        for(index++;index<span.Length;index++) {
            var item = span[index];
            if(item is not null&&Comparer<T>.Default.Compare(item,value)<0) {
                value=item;
            }
        }
        return value!;
    }

    /// <summary>System.Linq の Max と同じ意味。</summary>
    private static T SpanMax(ReadOnlySpan<T> span) {
        if(typeof(T)==typeof(int)) {
            if(span.Length==0) {
                throw IdealErrors.NoElements();
            }
            var result = IdealInt32.Max(AsInt32(span));
            return Unsafe.As<int,T>(ref result);
        }
        var index = 0;
        T value = default!;
        if(default(T) is null) {
            for(;index<span.Length;index++) {
                value=span[index];
                if(value is not null) {
                    break;
                }
            }
            if(index==span.Length) {
                return default!;
            }
        } else {
            if(span.Length==0) {
                throw IdealErrors.NoElements();
            }
            value=span[0];
        }
        for(index++;index<span.Length;index++) {
            var item = span[index];
            if(item is not null&&Comparer<T>.Default.Compare(item,value)>0) {
                value=item;
            }
        }
        return value!;
    }

    /// <summary>昇順・降順の安定な並べ替えをして consumer へ流す。int は同値が区別できないので通常の並べ替えでよい。</summary>
    private static void ConsumeSorted(ReadOnlySpan<T> span,bool descending,global::Common.Consumer consumer) {
        if(typeof(T)==typeof(int)) {
            var buffer = ArrayPool<int>.Shared.Rent(span.Length);
            try {
                var values = buffer.AsSpan(0,span.Length);
                AsInt32(span).CopyTo(values);
                values.Sort();
                if(descending) {
                    for(var index = values.Length-1;index>=0;index--) {
                        consumer.Consume(Unsafe.As<int,T>(ref values[index]));
                    }
                } else {
                    for(var index = 0;index<values.Length;index++) {
                        consumer.Consume(Unsafe.As<int,T>(ref values[index]));
                    }
                }
            } finally {
                ArrayPool<int>.Shared.Return(buffer);
            }
            return;
        }
        var indexed = ArrayPool<Indexed<T>>.Shared.Rent(span.Length);
        try {
            var items = indexed.AsSpan(0,span.Length);
            for(var index = 0;index<span.Length;index++) {
                items[index]=new Indexed<T>(span[index],index);
            }
            if(descending) {
                items.Sort(new DescendingStable<T>());
            } else {
                items.Sort(new AscendingStable<T>());
            }
            foreach(var item in items) {
                consumer.Consume(item.Value);
            }
        } finally {
            ArrayPool<Indexed<T>>.Shared.Return(indexed);
        }
    }

    private static T SinglePredicate(ReadOnlySpan<T> source,int number) {
        T result = default!;
        var found = false;
        foreach(var x in source) {
            if(EqualNumber(x,number)) {
                if(found) {
                    throw IdealErrors.MoreThanOneMatch();
                }
                found=true;
                result=x;
            }
        }
        if(!found) {
            throw IdealErrors.NoMatch();
        }
        return result;
    }
}
