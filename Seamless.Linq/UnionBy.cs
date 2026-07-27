namespace Seamless.Linq {
    partial class Enumerable {
        public static ValueEnumerable<UnionBy<TEnumerator,TEnumerator2,TSource,TKey>,TSource> UnionBy<TEnumerator, TEnumerator2, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second.Enumerator,Throws.IfNull(keySelector),null));

        public static ValueEnumerable<UnionBy<TEnumerator,TEnumerator2,TSource,TKey>,TSource> UnionBy<TEnumerator, TEnumerator2, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second.Enumerator,Throws.IfNull(keySelector),comparer));

        public static ValueEnumerable<UnionBy<TEnumerator,FromEnumerable<TSource>,TSource,TKey>,TSource> UnionBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(keySelector);
            return new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable().Enumerator,Throws.IfNull(keySelector),null));
        }

        public static ValueEnumerable<UnionBy<TEnumerator,FromEnumerable<TSource>,TSource,TKey>,TSource> UnionBy<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(keySelector);
            return new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable().Enumerator,Throws.IfNull(keySelector),comparer));
        }
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct UnionBy<TEnumerator, TEnumerator2, TSource, TKey>(TEnumerator source,TEnumerator2 second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private TEnumerator2 second = second;
        private HashSetSlim<TKey>? set;
        private byte state = 0;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) => false;

        public bool TryGetNext(out TSource current) {
            if(this.state==0) {
                var hint = EnumeratorHelper.GetCapacityHint<TEnumerator,TSource>(ref this.source)
                    +EnumeratorHelper.GetCapacityHint<TEnumerator2,TSource>(ref this.second);
                this.set=hint>0 ? new HashSetSlim<TKey>(hint,comparer) : new HashSetSlim<TKey>(comparer);
                this.state=1;
            }

            if(this.state==1) {
                while(this.source.TryGetNext(out var value)) {
                    if(this.set!.Add(keySelector(value))) {
                        current=value;
                        return true;
                    }
                }
                this.state=2;
            }

            if(this.state==2) {
                while(this.second.TryGetNext(out var value)) {
                    if(this.set!.Add(keySelector(value))) {
                        current=value;
                        return true;
                    }
                }

                this.state=3;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            this.state=3;
            this.set?.Dispose();
            this.source.Dispose();
            this.second.Dispose();
        }
    }

}
