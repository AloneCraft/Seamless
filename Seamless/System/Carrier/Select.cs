namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Select<TEnumerator,TSource,TResult>,TResult> Select<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TResult> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<Select2<TEnumerator,TSource,TResult>,TResult> Select<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,Int32,TResult> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<RangeSelect<TResult>,TResult> Select<TResult>(this ValueEnumerable<FromRange,int> source,Func<int,TResult> selector)
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<SelectWhere<TEnumerator,TSource,TResult>,TResult> Where<TEnumerator, TSource, TResult>(this ValueEnumerable<Select<TEnumerator,TSource,TResult>,TResult> source,Func<TResult,bool> predicate)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(source.Enumerator.Where(Throws.IfNull(predicate)));

        public static ValueEnumerable<ArraySelect<TSource,TResult>,TResult> Select<TSource, TResult>(this ValueEnumerable<FromArray<TSource>,TSource> source,Func<TSource,TResult> selector)
            => new(new(source.Enumerator.GetSource(),Throws.IfNull(selector)));

        public static ValueEnumerable<ArraySelectWhere<TSource,TResult>,TResult> Where<TSource, TResult>(this ValueEnumerable<ArraySelect<TSource,TResult>,TResult> source,Func<TResult,bool> predicate)
            => new(source.Enumerator.Where(Throws.IfNull(predicate)));

        public static ValueEnumerable<ListSelect<TSource,TResult>,TResult> Select<TSource, TResult>(this ValueEnumerable<FromList<TSource>,TSource> source,Func<TSource,TResult> selector)
            => new(new(source.Enumerator.GetSource(),Throws.IfNull(selector)));

        public static ValueEnumerable<ListSelectWhere<TSource,TResult>,TResult> Where<TSource, TResult>(this ValueEnumerable<ListSelect<TSource,TResult>,TResult> source,Func<TResult,bool> predicate)
            => new(source.Enumerator.Where(Throws.IfNull(predicate)));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Select<TEnumerator, TSource, TResult>(TEnumerator source,Func<TSource,TResult> selector):IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        internal TEnumerator source = source;
        internal readonly Func<TSource,TResult> selector = selector;

        public bool TryGetNonEnumeratedCount(out int count) => this.source.TryGetNonEnumeratedCount(out count);

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) {
            // Iterate inlining
            if(this.source.TryGetSpan(out var span)) {
                if(EnumeratorHelper.TryGetSlice(span,offset,destination.Length,out var slice)) {
                    for(var i = 0; (uint)i<(uint)slice.Length; i++) {
                        destination[i]=this.selector(slice[i]);
                    }
                    return true;
                }
            }

            //  First/ElementAt/Last
            if(destination.Length==1) {
#if NETSTANDARD2_0
                var singleSpan = SingleSpan.Create<TSource>();
                if (source.TryCopyTo(singleSpan, offset))
                {
                    try
                    {
                        destination[0] = selector(singleSpan[0]);
                    }
                    finally
                    {
                        singleSpan.Clear();
                    }
                    return true;
                }
#else
                var current = default(TSource)!;
                if(this.source.TryCopyTo(SingleSpan.Create(ref current),offset)) {
                    destination[0]=this.selector(current);
                    return true;
                }
#endif

                if(EnumeratorHelper.TryConsumeGetAt(ref this.source,offset,out TSource value)) {
                    destination[0]=this.selector(value);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNext(out TResult current) {
            if(this.source.TryGetNext(out var value)) {
                current=this.selector(value);
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }

        internal SelectWhere<TEnumerator,TSource,TResult> Where(Func<TResult,bool> predicate)
            => new(this.source,this.selector,predicate);
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Select2<TEnumerator, TSource, TResult>(TEnumerator source,Func<TSource,Int32,TResult> selector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private int index = 0;

        public bool TryGetNonEnumeratedCount(out int count) => this.source.TryGetNonEnumeratedCount(out count);

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
            if(this.source.TryGetNext(out var value)) {
                current=selector(value,this.index++);
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct RangeSelect<TResult>(FromRange source,Func<int,TResult> selector):IValueEnumerator<TResult> {
        // Range
        internal readonly int count = source.count;
        internal readonly int start = source.start;
        internal readonly int to = source.to;
        private int value = source.start;

        // Select
        internal readonly Func<int,TResult> selector = selector;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=this.count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) {
            if(EnumeratorHelper.TryGetSliceRange(this.count,offset,destination.Length,out var fillStart,out var fillCount)) {
                var value = this.start+fillStart;
                for(int i = 0; i<fillCount; i++) {
                    destination[i]=this.selector(value);
                    value++;
                }
                return true;
            }

            return false;
        }

        public bool TryGetNext(out TResult current) {
            if(this.value<this.to) {
                current=this.selector(this.value);
                this.value++;
                return true;
            }

            current=default!;
            return false;
        }

        public void Dispose() {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectWhere<TEnumerator, TSource, TResult>(TEnumerator source,Func<TSource,TResult> selector,Func<TResult,bool> predicate) // no in TEnumerator
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=default;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
            while(this.source.TryGetNext(out var value)) {
                var result = selector(value);
                if(predicate(result)) {
                    current=result;
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ArraySelect<TSource, TResult>(TSource[] source,Func<TSource,TResult> selector):IValueEnumerator<TResult> {
        internal TSource[] source = source;
        internal readonly Func<TSource,TResult> selector = selector;
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=this.source.Length;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) {
            // AsSpan is failed by array variance
            if(!typeof(TSource).IsValueType&&this.source.GetType()!=typeof(TSource[])) {
                return false;
            }

            if(EnumeratorHelper.TryGetSlice<TSource>(this.source.AsSpan(),offset,destination.Length,out var slice)) {
                for(var i = 0; (uint)i<(uint)slice.Length; i++) {
                    destination[i]=this.selector(slice[i]);
                }
                return true;
            }

            return false;
        }

        public bool TryGetNext(out TResult current) {
            if((uint)this.index<(uint)this.source.Length) {
                current=this.selector(this.source[this.index++]); // must be index++
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }

        internal ArraySelectWhere<TSource,TResult> Where(Func<TResult,bool> predicate) {
            return new ArraySelectWhere<TSource,TResult>(this.source,this.selector,predicate);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ArraySelectWhere<TSource, TResult>(TSource[] source,Func<TSource,TResult> selector,Func<TResult,bool> predicate):IValueEnumerator<TResult> {
        private TSource[] source = source;
        private readonly Func<TSource,TResult> selector = selector;
        private Func<TResult,bool> predicate = predicate;
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out TResult current) {
            while((uint)this.index<(uint)this.source.Length) {
                current=this.selector(this.source[this.index++]);
                if(this.predicate(current)) {
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ListSelect<TSource, TResult>(List<TSource> source,Func<TSource,TResult> selector):IValueEnumerator<TResult> {
        internal List<TSource> source = source;
        internal readonly Func<TSource,TResult> selector = selector;
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=this.source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) {
            if(EnumeratorHelper.TryGetSlice<TSource>(CollectionsMarshal.AsSpan(this.source),offset,destination.Length,out var slice)) {
                for(var i = 0; (uint)i<(uint)slice.Length; i++) {
                    destination[i]=this.selector(slice[i]);
                }
                return true;
            }

            return false;
        }

        public bool TryGetNext(out TResult current) {
            if((uint)this.index<(uint)this.source.Count) {
                current=this.selector(this.source[this.index++]);
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }

        internal ListSelectWhere<TSource,TResult> Where(Func<TResult,bool> predicate) {
            return new ListSelectWhere<TSource,TResult>(this.source,this.selector,predicate);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ListSelectWhere<TSource, TResult>(List<TSource> source,Func<TSource,TResult> selector,Func<TResult,bool> predicate):IValueEnumerator<TResult> {
        private List<TSource> source = source;
        private readonly Func<TSource,TResult> selector = selector;
        private Func<TResult,bool> predicate = predicate;
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out TResult current) {
            var span = CollectionsMarshal.AsSpan(this.source);
            while((uint)this.index<(uint)span.Length) {
                current=this.selector(span[this.index++]);
                if(this.predicate(current)) {
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }
}
