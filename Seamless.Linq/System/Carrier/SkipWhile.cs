namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<SkipWhile<TEnumerator,TSource>,TSource> SkipWhile<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(predicate)));

        public static ValueEnumerable<SkipWhile2<TEnumerator,TSource>,TSource> SkipWhile<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Int32,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(predicate)));

    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SkipWhile<TEnumerator, TSource>(TEnumerator source,Func<TSource,Boolean> predicate)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private bool skippingDone = false;

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
            // If we've already found an element that doesn't match the predicate,
            // we can just return elements directly from the source
            if(this.skippingDone) {
                return this.source.TryGetNext(out current);
            }

            // Skip all elements that match the predicate
            while(this.source.TryGetNext(out current)) {
                if(!predicate(current)) {
                    this.skippingDone=true;
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

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SkipWhile2<TEnumerator, TSource>(TEnumerator source,Func<TSource,Int32,Boolean> predicate)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private bool skippingDone = false;
        private int index = 0;

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
            // If we've already found an element that doesn't match the predicate,
            // we can just return elements directly from the source
            if(this.skippingDone) {
                if(this.source.TryGetNext(out current)) {
                    this.index++;
                    return true;
                }

                Unsafe.SkipInit(out current);
                return false;
            }

            // Skip all elements that match the predicate
            while(this.source.TryGetNext(out current)) {
                if(!predicate(current,this.index)) {
                    this.skippingDone=true;
                    this.index++;
                    return true;
                }
                this.index++;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }

}
