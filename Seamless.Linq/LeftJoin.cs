namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<LeftJoin<TEnumerator,TEnumerator2,TOuter,TInner,TKey,TResult>,TResult> LeftJoin<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,ValueEnumerable<TEnumerator2,TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner>
            => new(new(source.Enumerator,inner.Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<LeftJoin<TEnumerator,TEnumerator2,TOuter,TInner,TKey,TResult>,TResult> LeftJoin<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,ValueEnumerable<TEnumerator2,TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner>
            => new(new(source.Enumerator,inner.Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),comparer));


        public static ValueEnumerable<LeftJoin<TEnumerator,FromEnumerable<TInner>,TOuter,TInner,TKey,TResult>,TResult> LeftJoin<TEnumerator, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            => new(new(source.Enumerator,Throws.IfNull(inner).AsValueEnumerable().Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<LeftJoin<TEnumerator,FromEnumerable<TInner>,TOuter,TInner,TKey,TResult>,TResult> LeftJoin<TEnumerator, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            => new(new(source.Enumerator,Throws.IfNull(inner).AsValueEnumerable().Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),comparer));


    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct LeftJoin<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(TEnumerator source,TEnumerator2 inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TResult>
        where TEnumerator : struct, IValueEnumerator<TOuter>
        where TEnumerator2 : struct, IValueEnumerator<TInner> {
        private TEnumerator source = source;
        private TEnumerator2 inner = inner;
        private Lookup<TKey,TInner>? innerLookup;
        private Grouping<TKey,TInner>? currentGroup;
        private int currentGroupIndex;
        private TOuter currentOuter = default!;

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
            if(this.innerLookup==null) {
                try {
                    // CreateForJoin excludes null.
                    // If we want to accept null keys, we should create a lookup with null keys and join them.
                    // However, this behavior matches the official dotnet implementation.
                    this.innerLookup=Lookup.CreateForJoin(ref this.inner,innerKeySelector,comparer);
                } finally {
                    this.inner.Dispose();
                }
            }

            // iterating group
        ITERATE:
            if(this.currentGroup!=null) {
                if(this.currentGroupIndex<this.currentGroup.Count) {
                    current=resultSelector(this.currentOuter,this.currentGroup[this.currentGroupIndex]);
                    this.currentGroupIndex++;
                    return true;
                } else {
                    this.currentGroup=null;
                }
            }

            while(this.source.TryGetNext(out var value)) {
                var key = outerKeySelector(value);
                var group = this.innerLookup.GetGroup(key);
                if(group!=null) {
                    this.currentOuter=value;
                    this.currentGroup=group;
                    this.currentGroupIndex=0;
                    goto ITERATE;
                } else {
                    // The difference with Join is how nulls are handled.
                    current=resultSelector(value,default);
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.innerLookup==null) {
                this.inner.Dispose();
            }
            this.source.Dispose();
        }
    }
}
