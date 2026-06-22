namespace Seamless.Collections.Tsavorite.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Distinct<TEnumerator,TSource>,TSource> Distinct<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,null!));

        public static ValueEnumerable<Distinct<TEnumerator,TSource>,TSource> Distinct<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,comparer));

    }
}

namespace Seamless.Collections.Tsavorite.Carrier.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Distinct<TEnumerator, TSource>(TEnumerator source,IEqualityComparer<TSource>? comparer)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;
        HashSetSlim<TSource>? set;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) {
            if(destination.Length==1&&offset.Value==0) // as TryGetFirst
            {
                return this.source.TryCopyTo(destination,offset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current) {
            if(this.set==null) {
                var hint = EnumeratorHelper.GetCapacityHint<TEnumerator,TSource>(ref this.source);
                this.set=hint>0 ? new HashSetSlim<TSource>(hint,comparer) : new HashSetSlim<TSource>(comparer);
            }

            while(this.source.TryGetNext(out var value)) {
                if(this.set.Add(value)) {
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
