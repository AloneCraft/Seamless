namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        // Cast has been moved to instance method

        //        public static ValueEnumerable<OfType<TEnumerator, TSource, TResult>, TResult> OfType<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator, TSource> source, TResult typeHint)
        //            where TEnumerator : struct, IValueEnumerator<TSource>
        //            => new(new(source.Enumerator));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct OfType<TEnumerator, TSource, TResult>(TEnumerator source)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;

        internal TEnumerator GetSource() => source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
            while(source.TryGetNext(out var value)) {
                if(value is TResult v) {
                    current=v;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            source.Dispose();
        }
    }
}
