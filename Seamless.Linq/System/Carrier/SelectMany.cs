namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<SelectMany<TEnumerator,TEnumerator2,TSource,TResult>,TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,ValueEnumerable<TEnumerator2,TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TResult>
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany2<TEnumerator,TEnumerator2,TSource,TResult>,TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,int,ValueEnumerable<TEnumerator2,TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TResult>
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany3<TEnumerator,TEnumerator2,TSource,TCollection,TResult>,TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,ValueEnumerable<TEnumerator2,TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TCollection>
            => new(new(source.Enumerator,Throws.IfNull(collectionSelector),Throws.IfNull(resultSelector)));

        public static ValueEnumerable<SelectMany4<TEnumerator,TEnumerator2,TSource,TCollection,TResult>,TResult> SelectMany<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,int,ValueEnumerable<TEnumerator2,TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TCollection>
            => new(new(source.Enumerator,Throws.IfNull(collectionSelector),Throws.IfNull(resultSelector)));

        // IEnumerable<T> validation

        public static ValueEnumerable<SelectMany<TEnumerator,TSource,TResult>,TResult> SelectMany<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,IEnumerable<TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany2<TEnumerator,TSource,TResult>,TResult> SelectMany<TEnumerator, TSource, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,int,IEnumerable<TResult>> selector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(selector)));

        public static ValueEnumerable<SelectMany3<TEnumerator,TSource,TCollection,TResult>,TResult> SelectMany<TEnumerator, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(collectionSelector),Throws.IfNull(resultSelector)));

        public static ValueEnumerable<SelectMany4<TEnumerator,TSource,TCollection,TResult>,TResult> SelectMany<TEnumerator, TSource, TCollection, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,int,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(collectionSelector),Throws.IfNull(resultSelector)));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany<TEnumerator, TSource, TResult>(TEnumerator source,Func<TSource,IEnumerable<TResult>> selector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private FromEnumerable<TResult> innerEnumerator;
        private bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out current)) {
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.innerEnumerator=selector(value).AsValueEnumerable().Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany2<TEnumerator, TEnumerator2, TSource, TResult>(TEnumerator source,Func<TSource,int,ValueEnumerable<TEnumerator2,TResult>> selector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TResult> {
        private TEnumerator source = source;
        private TEnumerator2 innerEnumerator;
        private bool hasInner = false;
        private int index = 0;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out current)) {
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.innerEnumerator=selector(value,this.index++).Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany3<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(TEnumerator source,Func<TSource,ValueEnumerable<TEnumerator2,TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TCollection> {
        private TEnumerator source = source;
        private TEnumerator2 innerEnumerator;
        private TSource currentSource = default!;
        private bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out var innerCurrent)) {
                    current=resultSelector(this.currentSource,innerCurrent);
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.currentSource=value;
                this.innerEnumerator=collectionSelector(value).Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany4<TEnumerator, TEnumerator2, TSource, TCollection, TResult>(TEnumerator source,Func<TSource,int,ValueEnumerable<TEnumerator2,TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TCollection> {
        private TEnumerator source = source;
        private TEnumerator2 innerEnumerator;
        private TSource currentSource = default!;
        private int index;
        private bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out var innerCurrent)) {
                    current=resultSelector(this.currentSource,innerCurrent);
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.currentSource=value;
                this.innerEnumerator=collectionSelector(value,this.index++).Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    #region IEnumerable valiation

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany<TEnumerator, TEnumerator2, TSource, TResult>(TEnumerator source,Func<TSource,ValueEnumerable<TEnumerator2,TResult>> selector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TResult> {
        private TEnumerator source = source;
        private TEnumerator2 innerEnumerator;
        private bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out current)) {
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.innerEnumerator=selector(value).Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany2<TEnumerator, TSource, TResult>(TEnumerator source,Func<TSource,int,IEnumerable<TResult>> selector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private FromEnumerable<TResult> innerEnumerator;
        private bool hasInner = false;
        private int index = 0;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out current)) {
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.innerEnumerator=selector(value,this.index++).AsValueEnumerable().Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany3<TEnumerator, TSource, TCollection, TResult>(TEnumerator source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private FromEnumerable<TCollection> innerEnumerator;
        private TSource currentSource = default!;
        private bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out var innerCurrent)) {
                    current=resultSelector(this.currentSource,innerCurrent);
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.currentSource=value;
                this.innerEnumerator=collectionSelector(value).AsValueEnumerable().Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct SelectMany4<TEnumerator, TSource, TCollection, TResult>(TEnumerator source,Func<TSource,int,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private FromEnumerable<TCollection> innerEnumerator;
        private TSource currentSource = default!;
        private int index;
        private bool hasInner = false;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
        BEGIN:
            if(this.hasInner) {
                if(this.innerEnumerator.TryGetNext(out var innerCurrent)) {
                    current=resultSelector(this.currentSource,innerCurrent);
                    return true;
                }

                this.innerEnumerator.Dispose();
                this.hasInner=false;
            }

            if(this.source.TryGetNext(out var value)) {
                this.currentSource=value;
                this.innerEnumerator=collectionSelector(value,this.index++).AsValueEnumerable().Enumerator;
                this.hasInner=true;
                goto BEGIN;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.hasInner) {
                this.innerEnumerator.Dispose();
            }
            this.source.Dispose();
        }
    }

    #endregion
}
