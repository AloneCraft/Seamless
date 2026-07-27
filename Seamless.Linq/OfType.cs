namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<OfType<TEnumerator,TSource,TResult>,TResult> OfType<TEnumerator,TSource,TResult>(
            this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct,IValueEnumerator<TSource>
            => new(new(source.Enumerator));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct OfType<TEnumerator, TSource, TResult>(TEnumerator source)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;

        internal TEnumerator GetSource() => this.source;

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
            while(this.source.TryGetNext(out var value)) {
                if(value is TResult v) {
                    current=v;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }
}
