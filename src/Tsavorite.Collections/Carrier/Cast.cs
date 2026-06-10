namespace Tsavorite.Carrier {
    // Cast has been moved to instance method

    //    partial class ValueEnumerableExtensions
    //    {
    //        public static ValueEnumerable<Cast<TEnumerator, TSource, TResult>, TResult> Cast<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator, TSource> source, TResult typeHint)
    //            where TEnumerator : struct, IValueEnumerator<TSource>
    //            => new(new(source.Enumerator));
    //    }
}

namespace Tsavorite.Carrier.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Cast<TEnumerator, TSource, TResult>(TEnumerator source)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;

        public bool TryGetNonEnumeratedCount(out int count) {
            return this.source.TryGetNonEnumeratedCount(out count);
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
            while(this.source.TryGetNext(out var value)) {
                current=(TResult)(object)value!;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }
}
