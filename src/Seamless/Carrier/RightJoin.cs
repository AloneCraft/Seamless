namespace Seamless.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<RightJoin<TEnumerator,TEnumerator2,TOuter,TInner,TKey,TResult>,TResult> RightJoin<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,ValueEnumerable<TEnumerator2,TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner>
            => new(new(source.Enumerator,inner.Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<RightJoin<TEnumerator,TEnumerator2,TOuter,TInner,TKey,TResult>,TResult> RightJoin<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,ValueEnumerable<TEnumerator2,TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner>
            => new(new(source.Enumerator,inner.Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),comparer));


        public static ValueEnumerable<RightJoin<TEnumerator,FromEnumerable<TInner>,TOuter,TInner,TKey,TResult>,TResult> RightJoin<TEnumerator, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector)
    where TEnumerator : struct, IValueEnumerator<TOuter>
    => new(new(source.Enumerator,Throws.IfNull(inner).AsValueEnumerable().Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<RightJoin<TEnumerator,FromEnumerable<TInner>,TOuter,TInner,TKey,TResult>,TResult> RightJoin<TEnumerator, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            => new(new(source.Enumerator,Throws.IfNull(inner).AsValueEnumerable().Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),comparer));
    }
}

namespace Seamless.Carrier.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct RightJoin<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(TEnumerator source,TEnumerator2 inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TResult>
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner> {
        TEnumerator source = source;
        TEnumerator2 inner = inner;

        Lookup<TKey,TOuter>? outerLookup;
        Grouping<TKey,TOuter>? currentGroup;
        int currentGroupIndex;
        TInner currentInner = default!;

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
            if(this.outerLookup==null) {
                try {
                    // CreateForJoin excludes null.
                    // If we want to accept null keys, we should create a lookup with null keys and join them.
                    // However, this behavior matches the official dotnet implementation.
                    this.outerLookup=Lookup.CreateForJoin(ref this.source,outerKeySelector,comparer);
                }
                finally {
                    this.source.Dispose();
                }
            }

            // iterating group
        ITERATE:
            if(this.currentGroup!=null) {
                if(this.currentGroupIndex<this.currentGroup.Count) {
                    current=resultSelector(this.currentGroup[this.currentGroupIndex],this.currentInner);
                    this.currentGroupIndex++;
                    return true;
                }
                else {
                    this.currentGroup=null;
                }
            }

            while(this.inner.TryGetNext(out var value)) {
                var key = innerKeySelector(value);
                var group = this.outerLookup.GetGroup(key);
                if(group!=null) {
                    this.currentInner=value;
                    this.currentGroup=group;
                    this.currentGroupIndex=0;
                    goto ITERATE;
                }
                else {
                    // The difference with Join is how nulls are handled.
                    current=resultSelector(default,value);
                    return true;
                }
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.outerLookup==null) // source is outer
            {
                this.source.Dispose();
            }
            this.inner.Dispose();
        }
    }
}
