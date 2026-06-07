namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<ShuffleSkipTake<TEnumerator, TSource>, TSource> Take<TEnumerator, TSource>(this ValueEnumerable<Shuffle<TEnumerator, TSource>, TSource> source, int count)
            where TEnumerator : struct, IValueEnumerator<TSource>
        {
            return new(new(source.Enumerator.source, skipCount: 0, takeCount: Math.Max(0, count)));
        }

        public static ValueEnumerable<ShuffleSkipTake<TEnumerator, TSource>, TSource> Skip<TEnumerator, TSource>(this ValueEnumerable<Shuffle<TEnumerator, TSource>, TSource> source, int count)
            where TEnumerator : struct, IValueEnumerator<TSource>
        {
            return new(new(source.Enumerator.source, skipCount: Math.Max(0, count), takeCount: int.MaxValue));
        }

        // 'Last' variants are syntax sugar. just takes specified count of items randomly from source.

        public static ValueEnumerable<ShuffleSkipTake<TEnumerator, TSource>, TSource> TakeLast<TEnumerator, TSource>(this ValueEnumerable<Shuffle<TEnumerator, TSource>, TSource> source, int count)
            where TEnumerator : struct, IValueEnumerator<TSource>
        {
            return new(new(source.Enumerator.source, skipCount: 0, takeCount: Math.Max(0, count)));
        }

        public static ValueEnumerable<ShuffleSkipTake<TEnumerator, TSource>, TSource> SkipLast<TEnumerator, TSource>(this ValueEnumerable<Shuffle<TEnumerator, TSource>, TSource> source, int count)
            where TEnumerator : struct, IValueEnumerator<TSource>
        {
            return new(new(source.Enumerator.source, skipCount: Math.Max(0, count), takeCount: int.MaxValue));
        }
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ShuffleSkipTake<TEnumerator, TSource>(TEnumerator source, int skipCount, int takeCount)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;

        // To keep implementation simple and allow for future extensibility (such as Take.Take, etc.),
        // we will use a structure with two int fields.
        readonly int skipCount = skipCount;
        readonly int takeCount = takeCount;

        RentedArrayBox<TSource>? buffer;
        int index = 0;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            if (this.source.TryGetNonEnumeratedCount(out count))
            {
                count =this.GetLength(count);
                return true;
            }

            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            this.InitBuffer();
            span =this.buffer.Span;
            return true;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            this.InitBuffer();

            if (EnumeratorHelper.TryGetSlice<TSource>(this.buffer.Span, offset, destination.Length, out var slice))
            {
                slice.CopyTo(destination);
                return true;
            }

            return false;
        }

        public bool TryGetNext(out TSource current)
        {
            if (this.buffer == null)
            {
                this.InitBuffer();
            }

            if ((uint)this.index < (uint)this.buffer.Length)
            {
                current =this.buffer.UnsafeGetAt(this.index);
                this.index++;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.buffer?.Dispose();

            if (this.buffer != null)
            {
                // source already disposed by enumeration in the InitBuffer()
                return;
            }

            this.source.Dispose();
        }

        int GetLength(int count)
        {
            var length = Math.Max(0, count -this.skipCount);
            length = Math.Min(length,this.takeCount);
            return length;
        }

        [MemberNotNull(nameof(buffer))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        void InitBuffer()
        {
            if (this.buffer == null)
            {
                var (array, count) = new ValueEnumerable<TEnumerator, TSource>(this.source).ToArrayPool();

                // Extract only the counts and perform partial shuffling
                var resultCount = this.GetLength(count);
                if (resultCount == 0)
                {
                    ArrayPool<TSource>.Shared.Return(array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TSource>());
                    this.buffer = RentedArrayBox<TSource>.Empty;
                }
                else
                {
                    RandomShared.PartialShuffle(array.AsSpan(0, count), resultCount);
                    this.buffer = new RentedArrayBox<TSource>(array, resultCount);
                }
            }
        }
    }
}
