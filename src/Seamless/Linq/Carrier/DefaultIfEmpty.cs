namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<DefaultIfEmpty<TEnumerator, TSource>, TSource> DefaultIfEmpty<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, default!));

        public static ValueEnumerable<DefaultIfEmpty<TEnumerator, TSource>, TSource> DefaultIfEmpty<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, TSource defaultValue)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, defaultValue));

    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct DefaultIfEmpty<TEnumerator, TSource>(TEnumerator source, TSource defaultValue)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        bool iterateDefault = true;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            if (this.source.TryGetNonEnumeratedCount(out count))
            {
                if (count == 0)
                {
                    // For empty collection, we'll return 1 item (the default value)
                    count = 1;
                }
                return true;
            }
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            if (this.source.TryGetSpan(out span))
            {
                if (span.Length == 0)
                {
                    // We can't create a span with custom default value on the fly
                    return false;
                }
                return true;
            }
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            if (destination.Length == 0) return true;

            // ignore offset when checking
            if (this.source.TryGetNonEnumeratedCount(out var sourceCount) && sourceCount == 0 && destination.Length >= 1)
            {
                // check offset
                if (offset.GetOffset(sourceCount) == 0)
                {
                    destination[0] = defaultValue;
                    return true;
                }
                return false;
            }
            else
            {
                return this.source.TryCopyTo(destination, offset);
            }
        }

        public bool TryGetNext(out TSource current)
        {
            if (this.source.TryGetNext(out current))
            {
                this.iterateDefault = false;
                return true;
            }

            if (this.iterateDefault)
            {
                this.iterateDefault = false;
                current = defaultValue;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.source.Dispose();
        }
    }
}
