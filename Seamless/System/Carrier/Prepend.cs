namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Prepend<TEnumerator,TSource>,TSource> Prepend<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,TSource element)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,element));

    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Prepend<TEnumerator, TSource>(TEnumerator source,TSource element)
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

            var totalLength = srcCount+1; // +1 for the prepended element
            var start = offset.GetOffset(totalLength);

            if(start>=totalLength) {
                return false;
            }

            var includePrependedElement = (start==0);

            var sourceStart = start>0 ? start-1 : 0;
            var availableDestSpace = destination.Length-(includePrependedElement ? 1 : 0);
            var elementsToCopy = Math.Min(srcCount-sourceStart,availableDestSpace);

            if(elementsToCopy>0) {
                var destinationOffset = includePrependedElement ? 1 : 0;
                var destinationSlice = destination.Slice(destinationOffset,elementsToCopy);
                if(!source.TryCopyTo(destinationSlice,sourceStart)) {
                    return false;
                }
            }

            if(includePrependedElement) {
                destination[0]=element;
            }
            return true;
        }

        public bool TryGetNext(out TSource current) {
            if(state==0) {
                current=element;
                state=1;
                return true;
            }

            if(state==1) {
                if(source.TryGetNext(out current)) {
                    return true;
                }
                state=2;
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
