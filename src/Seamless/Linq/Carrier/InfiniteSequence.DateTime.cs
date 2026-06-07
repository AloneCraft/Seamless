namespace Seamless.Linq.Carrier
{
    public static partial class ValueEnumerable
    {
        public static ValueEnumerable<FromInfiniteSequenceDateTime, DateTime> InfiniteSequence(DateTime start, TimeSpan step)
        {
            return new(new(start, step));
        }

        public static ValueEnumerable<FromInfiniteSequenceDateTimeOffset, DateTimeOffset> InfiniteSequence(DateTimeOffset start, TimeSpan step)
        {
            return new(new(start, step));
        }
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInfiniteSequenceDateTime(DateTime start, TimeSpan step) : IValueEnumerator<DateTime>
    {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<DateTime> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<DateTime> destination, Index offset)
        {
            return false;
        }

        public bool TryGetNext(out DateTime current)
        {
            if (!this.calledGetNext)
            {
                this.calledGetNext = true;
                current = start;
                return true;
            }

            current = start += step;
            return true;
        }

        public void Dispose()
        {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInfiniteSequenceDateTimeOffset(DateTimeOffset start, TimeSpan step) : IValueEnumerator<DateTimeOffset>
    {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<DateTimeOffset> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<DateTimeOffset> destination, Index offset)
        {
            return false;
        }

        public bool TryGetNext(out DateTimeOffset current)
        {
            if (!this.calledGetNext)
            {
                this.calledGetNext = true;
                current = start;
                return true;
            }

            current = start += step;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
