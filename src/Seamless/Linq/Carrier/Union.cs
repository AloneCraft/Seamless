namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<Union<TEnumerator, TEnumerator2, TSource>, TSource> Union<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator, TSource> source, ValueEnumerable<TEnumerator2, TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, second.Enumerator, null));

        public static ValueEnumerable<Union<TEnumerator, TEnumerator2, TSource>, TSource> Union<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator, TSource> source, ValueEnumerable<TEnumerator2, TSource> second, IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, second.Enumerator, comparer));


        public static ValueEnumerable<Union<TEnumerator, FromEnumerable<TSource>, TSource>, TSource> Union<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, IEnumerable<TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(second).AsValueEnumerable().Enumerator, null));

        public static ValueEnumerable<Union<TEnumerator, FromEnumerable<TSource>, TSource>, TSource> Union<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(second).AsValueEnumerable().Enumerator, comparer));
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
   public struct Union<TEnumerator, TEnumerator2, TSource>(TEnumerator source, TEnumerator2 second, IEqualityComparer<TSource>? comparer)
       : IValueEnumerator<TSource>
       where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        TEnumerator2 second = second;
        HashSetSlim<TSource>? set;
        byte state = 0;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            if (destination.Length == 1 && offset.Value == 0) // as TryGetFirst
            {
                return source.TryCopyTo(destination, offset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current)
        {
            if (state == 0)
            {
                var hint = EnumeratorHelper.GetCapacityHint<TEnumerator, TSource>(ref source)
                    + EnumeratorHelper.GetCapacityHint<TEnumerator2, TSource>(ref second);
                set = hint > 0 ? new HashSetSlim<TSource>(hint, comparer) : new HashSetSlim<TSource>(comparer);
                state = 1;
            }

            if (state == 1)
            {
                while (source.TryGetNext(out var value))
                {
                    if (set!.Add(value))
                    {
                        current = value;
                        return true;
                    }
                }
                state = 2;
            }

            if (state == 2)
            {
                while (second.TryGetNext(out var value))
                {
                    if (set!.Add(value))
                    {
                        current = value;
                        return true;
                    }
                }

                state = 3;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            state = 3;
            set?.Dispose();
            source.Dispose();
            second.Dispose();
        }
    }
}
