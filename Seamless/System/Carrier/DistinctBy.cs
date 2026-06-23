namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<DistinctBy<TEnumerator,TSource,TKey>,TSource> DistinctBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),null));

        public static ValueEnumerable<DistinctBy<TEnumerator,TSource,TKey>,TSource> DistinctBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),comparer));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct DistinctBy<TEnumerator, TSource, TKey>(TEnumerator source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;
        HashSetSlim<TKey>? set;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) => false;

        public bool TryGetNext(out TSource current) {
            if(set==null) {
                var hint = EnumeratorHelper.GetCapacityHint<TEnumerator,TSource>(ref source);
                set=hint>0 ? new HashSetSlim<TKey>(hint,comparer) : new HashSetSlim<TKey>(comparer);
            }

            while(source.TryGetNext(out var value)) {
                if(set.Add(keySelector(value))) {
                    current=value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            set?.Dispose();
            source.Dispose();
        }
    }
}
