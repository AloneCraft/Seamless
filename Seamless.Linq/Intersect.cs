namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<Intersect<TEnumerator,TEnumerator2,TSource>,TSource> Intersect<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second,null));

        public static ValueEnumerable<Intersect<TEnumerator,TEnumerator2,TSource>,TSource> Intersect<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second,comparer));



        public static ValueEnumerable<Intersect<TEnumerator,FromEnumerable<TSource>,TSource>,TSource> Intersect<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable(),null));

        public static ValueEnumerable<Intersect<TEnumerator,FromEnumerable<TSource>,TSource>,TSource> Intersect<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable(),comparer));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Intersect<TEnumerator, TEnumerator2, TSource>(TEnumerator source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private ValueEnumerable<TEnumerator2,TSource> second = second;
        private HashSetSlim<TSource>? set;

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
                this.set=this.second.ToHashSetSlim(comparer);
            }

            while(this.source.TryGetNext(out var value)) {
                if(this.set.Remove(value)) {
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
