namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Append<TEnumerator,TSource>,TSource> Append<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,TSource element)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,element));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Append<TEnumerator, TSource>(TEnumerator source,TSource element)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;
        byte state;

        public bool TryGetNonEnumeratedCount(out int count) {
            if(source.TryGetNonEnumeratedCount(out count)&&count<int.MaxValue) {
                count++;
                return true;
            }
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) {
            if(destination.Length==0) return false;
            if(!source.TryGetNonEnumeratedCount(out var srcCount)) return false;

            var totalLength = srcCount+1;
            var start = offset.GetOffset(totalLength);

            if(start>=totalLength) {
                return false;
            }

            var destinationOffset = 0;
            if(start<srcCount) {
                if(!source.TryCopyTo(destination,start)) {
                    return false;
                }
                destinationOffset=Math.Min(srcCount-start,destination.Length);
            }

            if(destinationOffset<destination.Length&&start<=srcCount) {
                destination[destinationOffset]=element;
            }

            return true;
        }

        public bool TryGetNext(out TSource current) {
            if(state==0) {
                if(source.TryGetNext(out current)) {
                    return true;
                }
                state=1;
            }

            if(state==1) {
                current=element;
                state=2;
                return true;
            }

            current=default!;
            return false;
        }

        public void Dispose() {
            state=2;
            source.Dispose();
        }
    }

}
