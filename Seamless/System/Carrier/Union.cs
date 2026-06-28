namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Union<TEnumerator,TEnumerator2,TSource>,TSource> Union<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second.Enumerator,null));

        public static ValueEnumerable<Union<TEnumerator,TEnumerator2,TSource>,TSource> Union<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second.Enumerator,comparer));


        public static ValueEnumerable<Union<TEnumerator,FromEnumerable<TSource>,TSource>,TSource> Union<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable().Enumerator,null));

        public static ValueEnumerable<Union<TEnumerator,FromEnumerable<TSource>,TSource>,TSource> Union<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,Throws.IfNull(second).AsValueEnumerable().Enumerator,comparer));
    }
}

namespace Seamless.System.Carrier {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Union<TEnumerator, TEnumerator2, TSource>(TEnumerator source,TEnumerator2 second,IEqualityComparer<TSource>? comparer)
       :IValueEnumerator<TSource>
       where TEnumerator : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TSource> {
        private TEnumerator source = source;
        private TEnumerator2 second = second;
        private HashSetSlim<TSource>? set;
        private byte state = 0;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) {
            if(destination.Length==1&&offset.Value==0) // as TryGetFirst
            {
                return this.source.TryCopyTo(destination,offset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current) {
            if(this.state==0) {
                var hint = EnumeratorHelper.GetCapacityHint<TEnumerator,TSource>(ref this.source)
                    +EnumeratorHelper.GetCapacityHint<TEnumerator2,TSource>(ref this.second);
                this.set=hint>0 ? new HashSetSlim<TSource>(hint,comparer) : new HashSetSlim<TSource>(comparer);
                this.state=1;
            }

            if(this.state==1) {
                while(this.source.TryGetNext(out var value)) {
                    if(this.set!.Add(value)) {
                        current=value;
                        return true;
                    }
                }
                this.state=2;
            }

            if(this.state==2) {
                while(this.second.TryGetNext(out var value)) {
                    if(this.set!.Add(value)) {
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
