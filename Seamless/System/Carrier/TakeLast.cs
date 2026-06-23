namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<TakeLast<TEnumerator,TSource>,TSource> TakeLast<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,Int32 count)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,count));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct TakeLast<TEnumerator, TSource>(TEnumerator source,Int32 count)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        TEnumerator source = source;
        readonly int takeCount = Math.Max(0,count);
        int state = 0; // 0: Initial, 1: Enumerating, 2: Dequeue, 3: Completed
        RentedRingBuffer<TSource>? ringBuffer;


        public bool TryGetNonEnumeratedCount(out int count) {
            if(this.source.TryGetNonEnumeratedCount(out count)) {
                count=Math.Min(count,this.takeCount);
                return true;
            }

            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            if(this.source.TryGetSpan(out span)) {
                if(span.Length>this.takeCount) {
                    span=span[^this.takeCount..];
                }
                return true;
            }
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) {
            if(this.source.TryGetNonEnumeratedCount(out var count)) {
                var actualTakeCount = Math.Min(count,this.takeCount);
                if(actualTakeCount<=0) {
                    return false;
                }

                var takeLastStartIndex = count-actualTakeCount;
                var offsetInTakeLast = offset.GetOffset(actualTakeCount);

                if(offsetInTakeLast<0||offsetInTakeLast>=actualTakeCount) {
                    return false;
                }

                var sourceOffset = takeLastStartIndex+offsetInTakeLast;
                var remainingElements = actualTakeCount-offsetInTakeLast;
                var elementsToCopy = Math.Min(remainingElements,destination.Length);

                if(elementsToCopy<=0) {
                    return false;
                }

                return this.source.TryCopyTo(destination.Slice(0,elementsToCopy),sourceOffset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current) {
            switch(this.state) {
                case 0:
                    return this.TryGetNextFirstPath(out current);
                case 1:
                    return this.source.TryGetNext(out current);
                case 2:
                    return this.ringBuffer!.TryDequeue(out current);
                default:
                    Unsafe.SkipInit(out current);
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool TryGetNextFirstPath(out TSource current) {
            Unsafe.SkipInit(out current);
            if(this.takeCount==0) {
                this.state=3;
                return false;
            }

            if(this.source.TryGetNonEnumeratedCount(out var totalCount)) {
                var skipCount = Math.Max(0,totalCount-this.takeCount);
                while(this.source.TryGetNext(out current)) {
                    if(--skipCount>=0) continue;
                    this.state=1; // Mark as enumerating
                    return true;
                }

                // Source exhausted
                this.state=3;
                return false;
            }
            else {

                var buffer = this.ringBuffer=new(this.takeCount); // Use a reasonable default capacity;

                while(this.source.TryGetNext(out var item)) {
                    buffer.Enqueue(item);
                }

                this.state=2; // Mark as collected

                if(buffer.TryDequeue(out current)) {
                    return true;
                }

                this.state=3;
                return false;
            }
        }

        public void Dispose() {
            this.ringBuffer?.Dispose();
            this.source.Dispose();
        }
    }
}
