namespace Seamless.Linq.Carrier
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<Where<TEnumerator, TSource>, TSource> Where<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(predicate)));

        public static ValueEnumerable<Where2<TEnumerator, TSource>, TSource> Where<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, Func<TSource, Int32, Boolean> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator, Throws.IfNull(predicate)));

        public static ValueEnumerable<WhereSelect<TEnumerator, TSource, TResult>, TResult> Select<TEnumerator, TSource, TResult>(this ValueEnumerable<Where<TEnumerator, TSource>, TSource> source, Func<TSource, TResult> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(source.Enumerator.Select(Throws.IfNull(selector)));

        // Optimize variations

        public static ValueEnumerable<ArrayWhere<TSource>, TSource> Where<TSource>(this ValueEnumerable<FromArray<TSource>, TSource> source, Func<TSource, Boolean> predicate)
            => new(new(source.Enumerator, Throws.IfNull(predicate)));

        public static ValueEnumerable<ArrayWhereSelect<TSource, TResult>, TResult> Select<TSource, TResult>(this ValueEnumerable<ArrayWhere<TSource>, TSource> source, Func<TSource, TResult> selector)
            => new(source.Enumerator.Select(Throws.IfNull(selector)));

        public static ValueEnumerable<ListWhere<TSource>, TSource> Where<TSource>(this ValueEnumerable<FromList<TSource>, TSource> source, Func<TSource, Boolean> predicate)
            => new(new(source.Enumerator, Throws.IfNull(predicate)));

        public static ValueEnumerable<ListWhereSelect<TSource, TResult>, TResult> Select<TSource, TResult>(this ValueEnumerable<ListWhere<TSource>, TSource> source, Func<TSource, TResult> selector)
            => new(source.Enumerator.Select(Throws.IfNull(selector)));
    }
}

namespace Seamless.Linq.Carrier.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Where<TEnumerator, TSource>(TEnumerator source, Func<TSource, Boolean> predicate)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;

        internal TEnumerator GetSource() => this.source;
        internal Func<TSource, bool> Predicate => predicate;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset) => false;

        public bool TryGetNext(out TSource current)
        {
            while (this.source.TryGetNext(out var value))
            {
                if (predicate(value))
                {
                    current = value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.source.Dispose();
        }

        // Optimize for common pattern: Where().Select()
        internal WhereSelect<TEnumerator, TSource, TResult> Select<TResult>(Func<TSource, TResult> selector)
            => new(this.source, predicate, selector);
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Where2<TEnumerator, TSource>(TEnumerator source, Func<TSource, Int32, Boolean> predicate)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        int index = 0;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset) => false;

        public bool TryGetNext(out TSource current)
        {
            while (this.source.TryGetNext(out var value))
            {
                if (predicate(value,this.index++))
                {
                    current = value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct WhereSelect<TEnumerator, TSource, TResult>(TEnumerator source, Func<TSource, Boolean> predicate, Func<TSource, TResult> selector) // no in TEnumerator
        : IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
    {
        TEnumerator source = source;
        internal TEnumerator GetSource() => this.source;
        internal Func<TSource, bool> Predicate => predicate;
        internal Func<TSource, TResult> Selector => selector;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
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
            while (this.source.TryGetNext(out var value))
            {
                if (predicate(value))
                {
                    current = selector(value);
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ArrayWhere<TSource>(FromArray<TSource> source, Func<TSource, Boolean> predicate) : IValueEnumerator<TSource>
    {
        TSource[] source = source.GetSource();
        internal TSource[] GetSource() => this.source;
        internal Func<TSource, bool> Predicate => predicate;
        int index;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(Span<TSource> destination, Index offset) => false;

        public bool TryGetNext(out TSource current)
        {
            while ((uint)this.index < (uint)this.source.Length)
            {
                var value = this.source[this.index];
                this.index++;
                if (predicate(value))
                {
                    current = value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
        }

        // Optimize for common pattern: Where().Select()
        public ArrayWhereSelect<TSource, TResult> Select<TResult>(Func<TSource, TResult> selector)
            => new(this.source, predicate, selector);
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ArrayWhereSelect<TSource, TResult>(TSource[] source, Func<TSource, Boolean> predicate, Func<TSource, TResult> selector)
        : IValueEnumerator<TResult>
    {
        int index;
        TSource[] source = source;
        internal TSource[] GetSource() => this.source;
        internal Func<TSource, bool> Predicate => predicate;
        internal Func<TSource, TResult> Selector => selector;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
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
            while ((uint)this.index < (uint)this.source.Length)
            {
                var value = this.source[this.index];
                this.index++;
                if (predicate(value))
                {
                    current = selector(value);
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ListWhere<TSource>(FromList<TSource> source, Func<TSource, Boolean> predicate) : IValueEnumerator<TSource>
    {
        List<TSource> source = source.GetSource();
        internal List<TSource> GetSource() => this.source;
        internal Func<TSource, bool> Predicate => predicate;
        int index;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            span = default;
            return false;
        }

        public bool TryCopyTo(Span<TSource> destination, Index offset) => false;

        public bool TryGetNext(out TSource current)
        {
            var span = CollectionsMarshal.AsSpan(this.source);
            while ((uint)this.index < (uint)span.Length)
            {
                var value = span[this.index];
                this.index++;
                if (predicate(value))
                {
                    current = value;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
        }

        // Optimize for common pattern: Where().Select()
        public ListWhereSelect<TSource, TResult> Select<TResult>(Func<TSource, TResult> selector)
            => new(this.source, predicate, selector);
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ListWhereSelect<TSource, TResult>(List<TSource> source, Func<TSource, Boolean> predicate, Func<TSource, TResult> selector)
        : IValueEnumerator<TResult>
    {
        int index;
        List<TSource> source = source;
        internal List<TSource> GetSource() => this.source;
        internal Func<TSource, bool> Predicate => predicate;
        internal Func<TSource, TResult> Selector => selector;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            count = default;
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
            var span = CollectionsMarshal.AsSpan(this.source);
            while ((uint)this.index < (uint)span.Length)
            {
                var value = span[this.index];
                this.index++;
                if (predicate(value))
                {
                    current = selector(value);
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
        }
    }
}
