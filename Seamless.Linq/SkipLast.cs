namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<SkipLast<TEnumerator,TSource>,TSource> SkipLast<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Int32 count)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,count));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SkipLast<TEnumerator, TSource>(TEnumerator source,Int32 count)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private readonly int skipCount = Math.Max(0,count);
        private RefBox<ValueQueue<TSource>>? buffer;

        public bool TryGetNonEnumeratedCount(out int count) {
            if(this.source.TryGetNonEnumeratedCount(out count)) {
                count=Math.Max(0,count-this.skipCount);
                return true;
            }

            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            if(this.source.TryGetSpan(out span)) {
                if(span.Length<=this.skipCount) {
                    span=default;
                    return true;
                }
                span=span[..^this.skipCount]; // Slice from start to (length - skipCount)
                return true;
            }

            span=default;
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

                var sourceOffset = offsetInSkipped;

                var elementsAvailable = remainingCount-offsetInSkipped;

                var elementsToCopy = Math.Min(elementsAvailable,destination.Length);

                if(elementsToCopy<=0) {
                    return false;
                }

                return this.source.TryCopyTo(destination[..elementsToCopy],sourceOffset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current) {
            if(this.skipCount==0) {
                return this.source.TryGetNext(out current);
            }

            if(this.buffer==null) {
                this.buffer=new RefBox<ValueQueue<TSource>>(new ValueQueue<TSource>(4));

                // Fill the buffer with initial items
                while(this.buffer.GetValueRef().Count<this.skipCount&&this.source.TryGetNext(out var item)) {
                    this.buffer.GetValueRef().Enqueue(item);
                }

                // If we couldn't fill the buffer, there aren't enough items
                if(this.buffer.GetValueRef().Count<this.skipCount) {
                    Unsafe.SkipInit(out current);
                    return false;
                }
            }

            // Try to get the next item from the source
            if(this.source.TryGetNext(out var next)) {
                // Return the oldest item from buffer and add the new one
                current=this.buffer.GetValueRef().Dequeue();
                this.buffer.GetValueRef().Enqueue(next);
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.buffer?.Dispose();
            this.source.Dispose();
        }
    }
}
