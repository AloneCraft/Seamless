namespace Seamless.Linq {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Skip<TEnumerator,TSource>,TSource> Skip<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Int32 count)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,count));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Skip<TEnumerator, TSource>(TEnumerator source,Int32 count)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;
        readonly int skipCount = Math.Max(0,count); // ensure non-negative
        int skipped;

        public bool TryGetNonEnumeratedCount(out int count) {
            if(this.source.TryGetNonEnumeratedCount(out count)) {
                count=Math.Max(0,count-this.skipCount); // subtract skip count, ensure non-negative
                return true;
            }

            count=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) {
            if(this.source.TryGetNonEnumeratedCount(out var count)) {
                var actualSkipCount = Math.Min(count,this.skipCount);

                var remainingCount = count-actualSkipCount;

                if(remainingCount<=0) {
                    return false;
                }

                var offsetInSkipped = offset.GetOffset(remainingCount);

                if(offsetInSkipped<0||offsetInSkipped>=remainingCount) {
                    return false;
                }

                var sourceOffset = actualSkipCount+offsetInSkipped;

                var elementsAvailable = remainingCount-offsetInSkipped;

                var elementsToCopy = Math.Min(elementsAvailable,destination.Length);

                if(elementsToCopy<=0) {
                    return false;
                }

                return this.source.TryCopyTo(destination.Slice(0,elementsToCopy),sourceOffset);
            }

            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            if(this.source.TryGetSpan(out span)) {
                if(span.Length<=this.skipCount) {
                    span=default;
                    return true;
                }
                span=span.Slice(this.skipCount);
                return true;
            }

            span=default;
            return false;
        }

        public bool TryGetNext(out TSource current) {
            // Skip elements if not already skipped
            while(this.skipped<this.skipCount) {
                if(!this.source.TryGetNext(out var _)) {
                    Unsafe.SkipInit(out current);
                    return false;
                }
                this.skipped++;
            }

            // Return elements after skipping
            if(this.source.TryGetNext(out current)) {
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
