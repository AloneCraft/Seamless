namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<SelectMany<TEnumerator, TEnumerator2, TSource, TResult>, TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, ValueEnumerable<TEnumerator2, TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TResult>
            => new(new(source.Enumerator, Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany2<TEnumerator, TEnumerator2, TSource, TResult>, TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, int, ValueEnumerable<TEnumerator2, TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TResult>
            => new(new(source.Enumerator, Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany3<TEnumerator, TEnumerator2, TSource, TCollection, TResult>, TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, ValueEnumerable<TEnumerator2, TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TCollection>
            => new(new(source.Enumerator, Throws.IfNull(collectionSelector), Throws.IfNull(resultSelector)));

        public static ValueEnumerable<SelectMany4<TEnumerator, TEnumerator2, TSource, TCollection, TResult>, TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, int, ValueEnumerable<TEnumerator2, TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TCollection>
            => new(new(source.Enumerator, Throws.IfNull(collectionSelector), Throws.IfNull(resultSelector)));

        // IEnumerable<T> validation

        public static ValueEnumerable<SelectMany<TEnumerator, TSource, TResult>, TResult> SelectMany<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, IEnumerable<TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany2<TEnumerator, TSource, TResult>, TResult> SelectMany<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, int, IEnumerable<TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany3<TEnumerator, TSource, TCollection, TResult>, TResult> SelectMany<TEnumerator, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(collectionSelector), Throws.IfNull(resultSelector)));

        public static ValueEnumerable<SelectMany4<TEnumerator, TSource, TCollection, TResult>, TResult> SelectMany<TEnumerator, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, int, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(collectionSelector), Throws.IfNull(resultSelector)));
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany<TEnumerator, TSource, TResult>(TEnumerator source, Func<TSource, IEnumerable<TResult>> selector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        FromEnumerable<TResult> innerEnumerator;
        bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out current))
                {
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                innerEnumerator = selector(value).AsValueEnumerable().Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany2<TEnumerator, TEnumerator2, TSource, TResult>(TEnumerator source, Func<TSource, int, ValueEnumerable<TEnumerator2, TResult>> selector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TResult>
    {
        TEnumerator source = source;
        TEnumerator2 innerEnumerator;
        bool hasInner = false;
        int index = 0;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out current))
                {
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                innerEnumerator = selector(value, index++).Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany3<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(TEnumerator source, Func<TSource, ValueEnumerable<TEnumerator2, TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TCollection>
    {
        TEnumerator source = source;
        TEnumerator2 innerEnumerator;
        TSource currentSource = default!;
        bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out var innerCurrent))
                {
                    current = resultSelector(currentSource, innerCurrent);
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                currentSource = value;
                innerEnumerator = collectionSelector(value).Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany4<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(TEnumerator source, Func<TSource, int, ValueEnumerable<TEnumerator2, TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TCollection>
    {
        TEnumerator source = source;
        TEnumerator2 innerEnumerator;
        TSource currentSource = default!;
        int index;
        bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out var innerCurrent))
                {
                    current = resultSelector(currentSource, innerCurrent);
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                currentSource = value;
                innerEnumerator = collectionSelector(value, index++).Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    #region IEnumerable valiation

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany<TEnumerator, TEnumerator2, TSource, TResult>(TEnumerator source, Func<TSource, ValueEnumerable<TEnumerator2, TResult>> selector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TResult>
    {
        TEnumerator source = source;
        TEnumerator2 innerEnumerator;
        bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out current))
                {
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                innerEnumerator = selector(value).Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany2<TEnumerator, TSource, TResult>(TEnumerator source, Func<TSource, int, IEnumerable<TResult>> selector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        FromEnumerable<TResult> innerEnumerator;
        bool hasInner = false;
        int index = 0;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out current))
                {
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                innerEnumerator = selector(value, index++).AsValueEnumerable().Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany3<TEnumerator, TSource, TCollection, TResult>(TEnumerator source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        FromEnumerable<TCollection> innerEnumerator;
        TSource currentSource = default!;
        bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out var innerCurrent))
                {
                    current = resultSelector(currentSource, innerCurrent);
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                currentSource = value;
                innerEnumerator = collectionSelector(value).AsValueEnumerable().Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany4<TEnumerator, TSource, TCollection, TResult>(TEnumerator source, Func<TSource, int, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        FromEnumerable<TCollection> innerEnumerator;
        TSource currentSource = default!;
        int index;
        bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination, Index offset) => false;

        public bool TryGetNext(out TResult current)
        {
        BEGIN:
            if (hasInner)
            {
                if (innerEnumerator.TryGetNext(out var innerCurrent))
                {
                    current = resultSelector(currentSource, innerCurrent);
                    return true;
                }

                innerEnumerator.Dispose();
                hasInner = false;
            }

            if (source.TryGetNext(out var value))
            {
                currentSource = value;
                innerEnumerator = collectionSelector(value, index++).AsValueEnumerable().Enumerator;
                hasInner = true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            if (hasInner)
            {
                innerEnumerator.Dispose();
            }
            source.Dispose();
        }
    }

    #endregion
}
