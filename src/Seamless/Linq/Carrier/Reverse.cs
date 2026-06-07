namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<Reverse<TEnumerator, TSource>, TSource> Reverse<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator));

    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Reverse<TEnumerator, TSource>(TEnumerator source)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        RentedArrayBox<TSource>? buffer;
        int index;

        public bool TryGetNonEnumeratedCount(out int count) => this.source.TryGetNonEnumeratedCount(out count);

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            this.InitBuffer();
            span =this.buffer.Span;
            return true;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            // in-place reverse needs full src buffer(no offset)
            if (this.source.TryGetNonEnumeratedCount(out var count) && offset.GetOffset(count) == 0)
            {
                // destination must be larger than source
                if (destination.Length >= count)
                {
                    // and ok to copy then reverse.
                    if (this.source.TryCopyTo(destination, 0))
                    {
                        MemoryExtensions.Reverse(destination.Slice(0, count));
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
                current =this.buffer.UnsafeGetAt(this.index++);
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (this.buffer != null)
            {
                this.buffer.Dispose();
                // source already disposed by enumeration in InitBuffer()
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
                MemoryExtensions.Reverse(this.buffer.Span);
            }
        }
    }

}
