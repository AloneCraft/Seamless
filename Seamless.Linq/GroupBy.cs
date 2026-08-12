namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<GroupBy<TEnumerator,TSource,TKey>,IGrouping<TKey,TSource>> GroupBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),null));

        public static ValueEnumerable<GroupBy<TEnumerator,TSource,TKey>,IGrouping<TKey,TSource>> GroupBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),comparer));

        public static ValueEnumerable<GroupBy2<TEnumerator,TSource,TKey,TElement>,IGrouping<TKey,TElement>> GroupBy<TEnumerator, TSource, TKey, TElement>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(elementSelector),null));

        public static ValueEnumerable<GroupBy2<TEnumerator,TSource,TKey,TElement>,IGrouping<TKey,TElement>> GroupBy<TEnumerator, TSource, TKey, TElement>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(elementSelector),comparer));

        public static ValueEnumerable<GroupBy3<TEnumerator,TSource,TKey,TResult>,TResult> GroupBy<TEnumerator, TSource, TKey, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<GroupBy3<TEnumerator,TSource,TKey,TResult>,TResult> GroupBy<TEnumerator, TSource, TKey, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(resultSelector),comparer));

        public static ValueEnumerable<GroupBy4<TEnumerator,TSource,TKey,TElement,TResult>,TResult> GroupBy<TEnumerator, TSource, TKey, TElement, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(elementSelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<GroupBy4<TEnumerator,TSource,TKey,TElement,TResult>,TResult> GroupBy<TEnumerator, TSource, TKey, TElement, TResult>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(keySelector),Throws.IfNull(elementSelector),Throws.IfNull(resultSelector),comparer));

    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct GroupBy<TEnumerator, TSource, TKey>(TEnumerator source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<IGrouping<TKey,TSource>>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private bool init;
        private Grouping<TKey,TSource>? rootGrouping;
        private Grouping<TKey,TSource>? currentGrouping;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<IGrouping<TKey,TSource>> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<IGrouping<TKey,TSource>> destination,Index offset) => false;

        public bool TryGetNext(out IGrouping<TKey,TSource> current) {
            if(!this.init) {
                this.init=true;
                this.rootGrouping=this.BuildRoot();
                if(this.rootGrouping!=null) {
                    current=this.rootGrouping;
                    this.currentGrouping=this.rootGrouping;
                    return true;
                }
            }

            this.currentGrouping=this.currentGrouping?.NextGroupInAddOrder;

            if(this.currentGrouping==null||this.currentGrouping==this.rootGrouping) {
                current=default!;
                return false;
            } else {
                current=this.currentGrouping;
                return true;
            }
        }

        public void Dispose() {
            this.source.Dispose();
        }
        private Grouping<TKey,TSource>? BuildRoot() {
            var lookupBuilder = new LookupBuilder<TKey,TSource>(comparer??EqualityComparer<TKey>.Default);

            if(this.source.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    lookupBuilder.AddUnpooled(keySelector(item),item);
                }
            } else {
                using(this.source) {
                    while(this.source.TryGetNext(out var item)) {
                        lookupBuilder.AddUnpooled(keySelector(item),item);
                    }
                }
            }

            return lookupBuilder.GetRootGroupAndClear();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct GroupBy2<TEnumerator, TSource, TKey, TElement>(TEnumerator source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<IGrouping<TKey,TElement>>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private bool init;
        private Grouping<TKey,TElement>? rootGrouping;
        private Grouping<TKey,TElement>? currentGrouping;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<IGrouping<TKey,TElement>> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<IGrouping<TKey,TElement>> destination,Index offset) => false;

        public bool TryGetNext(out IGrouping<TKey,TElement> current) {
            if(!this.init) {
                this.init=true;
                this.rootGrouping=this.BuildRoot();
                if(this.rootGrouping!=null) {
                    current=this.rootGrouping;
                    this.currentGrouping=this.rootGrouping;
                    return true;
                }
            }

            this.currentGrouping=this.currentGrouping?.NextGroupInAddOrder;

            if(this.currentGrouping==null||this.currentGrouping==this.rootGrouping) {
                current=default!;
                return false;
            } else {
                current=this.currentGrouping;
                return true;
            }
        }

        public void Dispose() {
            this.source.Dispose();
        }
        private Grouping<TKey,TElement>? BuildRoot() {
            var lookupBuilder = new LookupBuilder<TKey,TElement>(comparer??EqualityComparer<TKey>.Default);

            if(this.source.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    lookupBuilder.AddUnpooled(keySelector(item),elementSelector(item));
                }
            } else {
                using(this.source) {
                    while(this.source.TryGetNext(out var item)) {
                        lookupBuilder.AddUnpooled(keySelector(item),elementSelector(item));
                    }
                }
            }

            return lookupBuilder.GetRootGroupAndClear();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct GroupBy3<TEnumerator, TSource, TKey, TResult>(TEnumerator source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private bool init;
        private Grouping<TKey,TSource>? rootGrouping;
        private Grouping<TKey,TSource>? currentGrouping;

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
            if(!this.init) {
                this.init=true;
                this.rootGrouping=this.BuildRoot();
                if(this.rootGrouping!=null) {
                    current=resultSelector(this.rootGrouping.Key,this.rootGrouping);
                    this.currentGrouping=this.rootGrouping;
                    return true;
                }
            }

            this.currentGrouping=this.currentGrouping?.NextGroupInAddOrder;

            if(this.currentGrouping==null||this.currentGrouping==this.rootGrouping) {
                current=default!;
                return false;
            } else {
                current=resultSelector(this.currentGrouping.Key,this.currentGrouping);
                return true;
            }
        }

        public void Dispose() {
            this.source.Dispose();
        }
        private Grouping<TKey,TSource>? BuildRoot() {
            var lookupBuilder = new LookupBuilder<TKey,TSource>(comparer??EqualityComparer<TKey>.Default);

            if(this.source.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    lookupBuilder.AddUnpooled(keySelector(item),item);
                }
            } else {
                using(this.source) {
                    while(this.source.TryGetNext(out var item)) {
                        lookupBuilder.AddUnpooled(keySelector(item),item);
                    }
                }
            }

            return lookupBuilder.GetRootGroupAndClear();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct GroupBy4<TEnumerator, TSource, TKey, TElement, TResult>(TEnumerator source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private bool init;
        private Grouping<TKey,TElement>? rootGrouping;
        private Grouping<TKey,TElement>? currentGrouping;

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
            if(!this.init) {
                this.init=true;
                this.rootGrouping=this.BuildRoot();
                if(this.rootGrouping!=null) {
                    current=resultSelector(this.rootGrouping.Key,this.rootGrouping);
                    this.currentGrouping=this.rootGrouping;
                    return true;
                }
            }

            this.currentGrouping=this.currentGrouping?.NextGroupInAddOrder;

            if(this.currentGrouping==null||this.currentGrouping==this.rootGrouping) {
                current=default!;
                return false;
            } else {
                current=resultSelector(this.currentGrouping.Key,this.currentGrouping);
                return true;
            }
        }

        public void Dispose() {
            this.source.Dispose();
        }
        private Grouping<TKey,TElement>? BuildRoot() {
            var lookupBuilder = new LookupBuilder<TKey,TElement>(comparer??EqualityComparer<TKey>.Default);

            if(this.source.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    lookupBuilder.AddUnpooled(keySelector(item),elementSelector(item));
                }
            } else {
                using(this.source) {
                    while(this.source.TryGetNext(out var item)) {
                        lookupBuilder.AddUnpooled(keySelector(item),elementSelector(item));
                    }
                }
            }

            return lookupBuilder.GetRootGroupAndClear();
        }
    }
}
