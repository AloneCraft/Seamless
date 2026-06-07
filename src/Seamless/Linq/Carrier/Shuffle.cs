namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<Shuffle<TEnumerator, TSource>, TSource> Shuffle<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator));
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Shuffle<TEnumerator, TSource>(TEnumerator source)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        internal TEnumerator source = source; // for ShuffleSkipTake
        RentedArrayBox<TSource>? buffer;
        int index = 0;

        public bool TryGetNonEnumeratedCount(out int count) => this.source.TryGetNonEnumeratedCount(out count);

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            this.InitBuffer();
            span =this.buffer.Span;
            return true;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            // in-place shuffle needs full src buffer(no offset)
            if (this.source.TryGetNonEnumeratedCount(out var count) && offset.GetOffset(count) == 0)
            {
                // destination must be larger than source
                if (destination.Length >= count)
                {
                    // and ok to copy then shuffle.
                    if (this.source.TryCopyTo(destination, 0))
                    {
                        RandomShared.Shuffle(destination.Slice(0, count));
                        return true;
                    }
                }
            }

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
            this.InitBuffer();

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

        [MemberNotNull(nameof(buffer))]
        void InitBuffer()
        {
            if (this.buffer == null)
            {
                var (array, count) = new ValueEnumerable<TEnumerator, TSource>(this.source).ToArrayPool();
                this.buffer = new RentedArrayBox<TSource>(array, count);
                RandomShared.Shuffle(this.buffer.Span);
            }
        }
    }
}
