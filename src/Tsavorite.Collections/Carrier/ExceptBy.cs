namespace Tsavorite.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<ExceptBy<TEnumerator,TEnumerator2,TSource,TKey>,TSource> ExceptBy<TEnumerator, TEnumerator2, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TKey> second,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TKey>
            => new(new(source.Enumerator,second,Throws.IfNull(keySelector),null));

        public static ValueEnumerable<ExceptBy<TEnumerator,TEnumerator2,TSource,TKey>,TSource> ExceptBy<TEnumerator, TEnumerator2, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TKey>
            => new(new(source.Enumerator,second,Throws.IfNull(keySelector),comparer));

        public static ValueEnumerable<ExceptBy<TEnumerator,FromEnumerable<TKey>,TSource,TKey>,TSource> ExceptBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TKey> second,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable(),Throws.IfNull(keySelector),null));

        public static ValueEnumerable<ExceptBy<TEnumerator,FromEnumerable<TKey>,TSource,TKey>,TSource> ExceptBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable(),Throws.IfNull(keySelector),comparer));
    }
}

namespace Tsavorite.Carrier.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ExceptBy<TEnumerator, TEnumerator2, TSource, TKey>(TEnumerator source,ValueEnumerable<TEnumerator2,TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TKey> {
        TEnumerator source = source;
        ValueEnumerable<TEnumerator2,TKey> second = second;
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
            if(this.set==null) {
                this.set=this.second.ToHashSetSlim(comparer??EqualityComparer<TKey>.Default);
            }

            while(this.source.TryGetNext(out var value)) {
                if(this.set.Add(keySelector(value))) {
                    current=value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.set?.Dispose();
            this.source.Dispose();
        }
    }

}
