namespace Seamless.Linq {
    partial class ValueEnumerableExtensions {
        public static ValueEnumerable<Concat<TEnumerator1,TEnumerator2,TSource>,TSource> Concat<TEnumerator1, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator1,TSource> source,ValueEnumerable<TEnumerator2,TSource> second)
            where TEnumerator1 : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource>
            => new(new(source.Enumerator,second.Enumerator));


        public static ValueEnumerable<Concat<TEnumerator1,FromEnumerable<TSource>,TSource>,TSource> Concat<TEnumerator1, TSource>(this ValueEnumerable<TEnumerator1,TSource> source,IEnumerable<TSource> second)
            where TEnumerator1 : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(second);
            return Concat(source,second.AsValueEnumerable());
        }
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Concat<TEnumerator1, TEnumerator2, TSource>(TEnumerator1 first,TEnumerator2 second)
        :IValueEnumerator<TSource>
        where TEnumerator1 : struct, IValueEnumerator<TSource>
        where TEnumerator2 : struct, IValueEnumerator<TSource> {
        TEnumerator1 first = first;
        TEnumerator2 second = second;
        bool firstCompleted;

        public bool TryGetNonEnumeratedCount(out int count) {
            if(this.first.TryGetNonEnumeratedCount(out var count1)&&this.second.TryGetNonEnumeratedCount(out var count2)) {
                count=checked(count1+count2);
                return true;
            }
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination,Index offset) {
            // return false because source1 succeeded but source2 failed, it is high-cost operation
            // Also, if source1 succeeds and source2 fails, there is a possibility that source1's TryCopyTo completes and TryGetNext stops working.
            // ZLinq's semantics do not guarantee the operation of other operations if TryCopyTo succeeds.
            // Therefore, it must be set to false.
            return false;
        }

        public bool TryGetNext(out TSource current) {
            if(!this.firstCompleted) {
                // iterate first
                if(this.first.TryGetNext(out current)) {
                    return true;
                }
                this.first.Dispose();
                this.firstCompleted=true;
            }

            // iterate second
            if(this.second.TryGetNext(out current)) {
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(!this.firstCompleted) {
                this.first.Dispose();
            }
            this.second.Dispose();
        }
    }
}
