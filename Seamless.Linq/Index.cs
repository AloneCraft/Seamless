namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<Index<TEnumerator,TSource>,(int Index,TSource Item)> Index<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator));

    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Index<TEnumerator, TSource>(TEnumerator source)
        :IValueEnumerator<(int Index,TSource Item)>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private int index = -1;

        public bool TryGetNonEnumeratedCount(out int count) {
            return this.source.TryGetNonEnumeratedCount(out count);
        }

        public bool TryGetSpan(out ReadOnlySpan<(int Index,TSource Item)> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<(int Index,TSource Item)> destination,Index offset) {
            // Iterate inlining
            if(this.source.TryGetSpan(out var span)) {
                if(EnumeratorHelper.TryGetSlice(span,offset,destination.Length,out var slice)) {
                    var index = offset.GetOffset(span.Length);
                    for(var i = 0;(uint)i<(uint)slice.Length;i++) {
                        destination[i]=(index,slice[i]);
                        checked { index++; }
                    }
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNext(out (int Index,TSource Item) current) {
            if(this.source.TryGetNext(out var value)) {
                checked { this.index++; }
                current=(this.index,value);
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
