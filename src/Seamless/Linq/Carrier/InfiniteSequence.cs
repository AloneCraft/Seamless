#if NET8_0_OR_GREATER

namespace Seamless.Linq.Carrier
{
    public static partial class ValueEnumerable
    {
        public static ValueEnumerable<FromInfiniteSequence<T>, T> InfiniteSequence<T>(T start, T step)
            where T : IAdditionOperators<T, T, T>
        {
            if (start is null) Throws.Null(nameof(start));
            if (step is null) Throws.Null(nameof(step));

            return new(new(start, step));
        }
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromInfiniteSequence<T>(T start, T step) : IValueEnumerator<T>
        where T : IAdditionOperators<T, T, T>
    {
        bool calledGetNext;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<T> destination, Index offset)
        {
            return false;
        }

        public bool TryGetNext(out T current)
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

#endif
