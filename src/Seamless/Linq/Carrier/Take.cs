namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<Take<TEnumerator, TSource>, TSource> Take<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, Int32 count)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, count));

        public static ValueEnumerable<TakeRange<TEnumerator, TSource>, TSource> Take<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, Range range)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, range));

        // Optimize

        public static ValueEnumerable<TakeSkip<TEnumerator, TSource>, TSource> Skip<TEnumerator, TSource>(this ValueEnumerable<Take<TEnumerator, TSource>, TSource> source, int count)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(source.Enumerator.Skip(count));

        public static ValueEnumerable<TakeSkip<TEnumerator, TSource>, TSource> Skip<TEnumerator, TSource>(this ValueEnumerable<TakeSkip<TEnumerator, TSource>, TSource> source, int count)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(source.Enumerator.Skip(count));
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Take<TEnumerator, TSource>(TEnumerator source, Int32 count)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        internal readonly int takeCount = Math.Max(0, count); // allows negative count
        int index;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            if (this.source.TryGetNonEnumeratedCount(out count))
            {
                count = Math.Min(count,this.takeCount); // take smaller
                return true;
            }

            count = default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            if (this.source.TryGetSpan(out span))
            {
                span = span.Slice(0, Math.Min(span.Length,this.takeCount));
                return true;
            }

            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            if (this.source.TryGetNonEnumeratedCount(out var count))
            {
                var actualTakeCount = Math.Min(count,this.takeCount);

                // When offset IsFromEnd, calculate the source offset from take count.
                var sourceOffset = offset.GetOffset(actualTakeCount);

                if (sourceOffset < 0 || sourceOffset >= actualTakeCount)
                {
                    return false; // out of range mark as fail(for example ElementAt needs failed information).
                }

                // Remaining differs depending on whether it's from the beginning or from the end.
                var remainingElements = offset.IsFromEnd
                    ? offset.Value
                    : actualTakeCount - sourceOffset;

                var elementsToCopy = Math.Min(remainingElements, destination.Length);
                return this.source.TryCopyTo(destination.Slice(0, elementsToCopy), sourceOffset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current)
        {
            if (this.index++ <this.takeCount &&this.source.TryGetNext(out current))
            {
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.source.Dispose();
        }

        // Optimize
        internal TakeSkip<TEnumerator, TSource> Skip(int skipCount) => new(this.source, takeCount: this.takeCount, skipCount: skipCount);
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct RangeProcessor(Range range)
    {
        public readonly Range Range = range;
        public int SkipIndex = 0;
        public int Remains = -2; // -2: uninitialized, -1: unknown count, >=0: known count
        public int Index = 0;
        public int FromEndQueueSize = 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Initialize(int? knownCount = null)
        {
            if (this.Remains != -2) return;

            if (knownCount.HasValue)
            {
                this.InitializeWithKnownCount(knownCount.Value);
            }
            else if (!this.Range.Start.IsFromEnd && !this.Range.End.IsFromEnd)
            {
                this.InitializeBothFromStart();
            }
            else
            {
                this.InitializeWithUnknownCount();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void InitializeBothFromStart()
        {
            this.SkipIndex =this.Range.Start.Value;
            var length = this.Range.End.Value -this.Range.Start.Value;
            this.Remains = Math.Max(0, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void InitializeWithKnownCount(int count)
        {
            var start = this.Range.Start.IsFromEnd
                ? Math.Max(0, count -this.Range.Start.Value)
                : this.Range.Start.Value;
            var end = this.Range.End.IsFromEnd
                ? Math.Max(0, count -this.Range.End.Value)
                : this.Range.End.Value;

            this.SkipIndex = Math.Min(start, count);
            this.Remains = Math.Max(0, Math.Min(end, count) -this.SkipIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void InitializeWithUnknownCount()
        {
            if (!this.Range.Start.IsFromEnd &&this.Range.End.IsFromEnd)
            {
                this.SkipIndex =this.Range.Start.Value;
                this.Remains =this.Range.End.Value == 0 ? int.MaxValue : -1;

                if (this.Range.End.Value > 0)
                {
                    this.FromEndQueueSize =this.Range.End.Value;
                }
            }
            else if (this.Range.Start.IsFromEnd && !this.Range.End.IsFromEnd)
            {
                this.SkipIndex = 0;
                this.Remains = -1;
                this.FromEndQueueSize = Math.Max(1,this.Range.Start.Value);
            }
            else
            {
                this.SkipIndex = 0;
                var maxCount = this.Range.Start.Value -this.Range.End.Value;
                this.Remains = maxCount <= 0 ? 0 : -1;

                if (maxCount > 0)
                {
                    this.FromEndQueueSize = Math.Max(1,this.Range.Start.Value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CalculateRemainsFromQueue(int totalCount, int queueCount)
        {
            var start = this.Range.Start.IsFromEnd
                ? Math.Max(0, totalCount -this.Range.Start.Value)
                : this.Range.Start.Value;
            var end = this.Range.End.IsFromEnd
                ? Math.Max(0, totalCount -this.Range.End.Value)
                : this.Range.End.Value;

            this.Remains = Math.Max(0, end - start);

            var currentPosition = totalCount - queueCount;
            var toSkip = Math.Max(0, start - currentPosition);

            this.Remains = Math.Min(this.Remains - toSkip, queueCount - toSkip);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int GetQueueSkipCount(int totalCount, int queueCount)
        {
            var start = this.Range.Start.IsFromEnd
                ? Math.Max(0, totalCount -this.Range.Start.Value)
                : this.Range.Start.Value;

            var currentPosition = totalCount - queueCount;
            return Math.Max(0, start - currentPosition);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public (int offsetInRange, int elementsToCopy) CalculateCopyParameters(
            int totalCount, int destinationLength, Index offset)
        {
            var effectiveRemains = Math.Min(this.Remains, Math.Max(0, totalCount -this.SkipIndex));
            if (effectiveRemains <= 0)
                return (-1, 0);

            var offsetInRange = offset.GetOffset(effectiveRemains);
            if (offsetInRange < 0 || offsetInRange >= effectiveRemains)
                return (-1, 0);

            var elementsToCopy = Math.Min(effectiveRemains - offsetInRange, destinationLength);
            return (offsetInRange, elementsToCopy);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SkipQueue<TSource>(ref ValueQueue<TSource> queue)
        {
            this.CalculateRemainsFromQueue(this.Index, queue.Count);

            var toSkip = this.GetQueueSkipCount(this.Index, queue.Count);
            while (toSkip > 0 && queue.Count > 0)
            {
                queue.Dequeue();
                toSkip--;
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct TakeRange<TEnumerator, TSource>(TEnumerator source, Range range)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        RangeProcessor rangeProcessor = new(range);
        RefBox<ValueQueue<TSource>>? q;

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Init()
        {
            if (this.rangeProcessor.Remains != -2) return;

            if (this.source.TryGetNonEnumeratedCount(out var count))
            {
                this.rangeProcessor.Initialize(count);
            }
            else
            {
                this.rangeProcessor.Initialize();
            }

            if (this.rangeProcessor.FromEndQueueSize > 0)
            {
                this.q = new(new(Math.Min(this.rangeProcessor.FromEndQueueSize, 16)));
            }
        }

        public bool TryGetNonEnumeratedCount(out int count)
        {
            if (!this.source.TryGetNonEnumeratedCount(out count))
            {
                return false;
            }

            if (this.rangeProcessor.Remains == -2)
            {
                this.rangeProcessor.Initialize(count);
            }

            if (this.rangeProcessor.Remains >= 0)
            {
                count =this.rangeProcessor.Remains;
                return true;
            }

            count = default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            this.Init();

            if (this.rangeProcessor.Remains >= 0 &&this.source.TryGetSpan(out span))
            {
                span = span.Slice(this.rangeProcessor.SkipIndex,this.rangeProcessor.Remains);
                return true;
            }

            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            this.Init();

            if (this.rangeProcessor.Remains < 0 || !this.source.TryGetNonEnumeratedCount(out var totalCount))
                return false;

            var (offsetInRange, elementsToCopy) =this.rangeProcessor.CalculateCopyParameters(
                totalCount, destination.Length, offset);

            if (elementsToCopy <= 0)
                return false;

            return this.source.TryCopyTo(destination[..elementsToCopy],this.rangeProcessor.SkipIndex + offsetInRange);
        }

        public bool TryGetNext(out TSource current)
        {
            var remains = this.rangeProcessor.Remains;
            if (remains == -2)
            {
                this.Init();
            }

            if (remains == 0)
            {
                Unsafe.SkipInit(out current);
                return false;
            }

            return this.q == null
                ? this.TryGetNextSimple(out current)
                : this.TryGetNextWithQueue(out current);
        }

        bool TryGetNextSimple(out TSource current)
        {
            ref var rangeProcessor = ref this.rangeProcessor;
            while (rangeProcessor.Index < rangeProcessor.SkipIndex)
            {
                if (!this.source.TryGetNext(out _))
                {
                    rangeProcessor.Remains = 0;
                    Unsafe.SkipInit(out current);
                    return false;
                }

                rangeProcessor.Index++;
            }

            if (rangeProcessor.Remains > 0)
            {
                if (this.source.TryGetNext(out current))
                {
                    if (rangeProcessor.Remains != int.MaxValue)
                        rangeProcessor.Remains--;
                    return true;
                }
            }

            rangeProcessor.Remains = 0;
            Unsafe.SkipInit(out current);
            return false;
        }

        bool TryGetNextWithQueue(out TSource current)
        {
            ref var rangeProcessor = ref this.rangeProcessor;
            ref var queue = ref this.q!.GetValueRef();

            // Fast path: dequeue from processed queue
            if (rangeProcessor.Remains > 0 && queue.Count > 0)
            {
                goto DEQUEUE_RETURN;
            }

            // Fill queue from source
            var useSlidingWindow = !rangeProcessor.Range.Start.IsFromEnd && rangeProcessor.Range.End.IsFromEnd;

            while (this.source.TryGetNext(out current))
            {
                if (rangeProcessor.Index++ < rangeProcessor.SkipIndex)
                    continue;

                if (queue.Count == rangeProcessor.FromEndQueueSize)
                {
                    var result = queue.Dequeue();
                    if (useSlidingWindow)
                    {
                        // Sliding window: return oldest element immediately
                        queue.Enqueue(current);
                        current = result;
                        return true;
                    }
                }
                queue.Enqueue(current);
            }

            if (queue.Count == 0 || useSlidingWindow)
            {
                goto END;
            }

            if (rangeProcessor.Remains == -1)
                rangeProcessor.SkipQueue(ref queue);

        DEQUEUE_RETURN:
            if (rangeProcessor.Remains > 0)
            {
                current = queue.Dequeue();
                rangeProcessor.Remains--;
                return true;
            }

        END:
            rangeProcessor.Remains = 0;
            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.q?.Dispose();
            this.source.Dispose();
        }
    }


    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
        public struct TakeSkip<TEnumerator, TSource>(TEnumerator source, Int32 takeCount, Int32 skipCount)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        readonly int takeCount = Math.Max(0, takeCount); // ensure non-negative
        readonly int skipCount = Math.Max(0, skipCount); // ensure non-negative
        int taken;
        int skipped;
        bool reachedTakeLimit;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            if (this.source.TryGetNonEnumeratedCount(out count))
            {
                // Apply take limit first
                count = Math.Min(count,this.takeCount);

                // Then apply skip
                count = Math.Max(0, count -this.skipCount);

                return true;
            }

            count = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            if (this.source.TryGetNonEnumeratedCount(out var sourceCount))
            {
                // First limit by take
                var afterTake = Math.Min(sourceCount,this.takeCount);

                // Then apply skip
                var actualSkipCount = Math.Min(afterTake,this.skipCount);
                var actualCount = afterTake - actualSkipCount;

                if (actualCount <= 0)
                {
                    return false;
                }

                // Calculate offset within the resulting sequence
                var offsetInResult = offset.GetOffset(actualCount);

                if (offsetInResult < 0 || offsetInResult >= actualCount)
                {
                    return false;
                }

                // Calculate the source offset (within the take window, then skip)
                var sourceOffset = actualSkipCount + offsetInResult;

                // Calculate elements available after offset
                var elementsAvailable = actualCount - offsetInResult;

                // Calculate elements to copy
                var elementsToCopy = Math.Min(elementsAvailable, destination.Length);

                if (elementsToCopy <= 0)
                {
                    return false;
                }

                return this.source.TryCopyTo(destination.Slice(0, elementsToCopy), sourceOffset);
            }

            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            if (this.source.TryGetSpan(out span))
            {
                // First apply take limit
                if (span.Length >this.takeCount)
                {
                    span = span.Slice(0,this.takeCount);
                }

                // Then skip elements
                if (span.Length <=this.skipCount)
                {
                    span = default;
                    return true;
                }

                span = span.Slice(this.skipCount);
                return true;
            }

            span = default;
            return false;
        }

        public bool TryGetNext(out TSource current)
        {
            if (this.IsResultEmpty())
            {
                Unsafe.SkipInit(out current);
                return false;
            }

            // Once we've reached the take limit, we're done
            if (this.reachedTakeLimit)
            {
                Unsafe.SkipInit(out current);
                return false;
            }

            // Skip elements that have been emitted from the take portion
            while (this.skipped <this.skipCount)
            {
                // First ensure we haven't exceeded the take limit
                if (this.taken >=this.takeCount)
                {
                    this.reachedTakeLimit = true;
                    Unsafe.SkipInit(out current);
                    return false;
                }

                // Get next element from source
                if (!this.source.TryGetNext(out var _))
                {
                    Unsafe.SkipInit(out current);
                    return false;
                }

                this.taken++;

                // If we've reached the take limit before finishing skipping,
                // we won't be able to return any elements
                if (this.taken >=this.takeCount)
                {
                    this.reachedTakeLimit = true;
                    Unsafe.SkipInit(out current);
                    return false;
                }

                this.skipped++;
            }

            // Check if we've reached the take limit
            if (this.taken >=this.takeCount)
            {
                this.reachedTakeLimit = true;
                Unsafe.SkipInit(out current);
                return false;
            }

            // Return elements after taking and skipping
            if (this.source.TryGetNext(out current))
            {
                this.taken++;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        bool IsResultEmpty()
        {
            if (this.takeCount == 0)
            {
                return true;
            }

            if (this.skipCount >=this.takeCount)
            {
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            this.source.Dispose();
        }

        // Optimize

        internal TakeSkip<TEnumerator, TSource> Skip(int count)
        {
            if (count <= 0)
            {
                return this; // no changes.
            }

            // check overflow
            int newSkipCount;
            if (count > 0 &&this.skipCount > int.MaxValue - count)
            {
                newSkipCount = int.MaxValue;
            }
            else
            {
                newSkipCount =this.skipCount + count;
            }

            return new TakeSkip<TEnumerator, TSource>(this.source,this.takeCount, newSkipCount);
        }
    }
}
