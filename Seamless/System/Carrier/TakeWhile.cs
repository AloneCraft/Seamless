namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<TakeWhile<TEnumerator,TSource>,TSource> TakeWhile<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(predicate)));

        public static ValueEnumerable<TakeWhile2<TEnumerator,TSource>,TSource> TakeWhile<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Int32,Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(predicate)));

    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct TakeWhile<TEnumerator, TSource>(TEnumerator source,Func<TSource,Boolean> predicate)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;

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
            if(this.source.TryGetNext(out current)&&predicate(current)) {
                return true;
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
    public struct TakeWhile2<TEnumerator, TSource>(TEnumerator source,Func<TSource,Int32,Boolean> predicate)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;
        int index = 0;

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
            if(this.source.TryGetNext(out current)&&predicate(current,this.index++)) {
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
