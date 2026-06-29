namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
        public static Boolean SequenceEqual<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second)
    where TEnumerator : struct, IValueEnumerator<TSource> {
            return SequenceEqual(source,Throws.IfNull(second).AsValueEnumerable(),null);
        }

        public static Boolean SequenceEqual<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            return SequenceEqual(source,Throws.IfNull(second).AsValueEnumerable(),comparer);
        }

        public static Boolean SequenceEqual<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource> {
            return SequenceEqual(source,second,null);
        }

        public static Boolean SequenceEqual<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource> {
            using var e1 = source.Enumerator;
            using var e2 = second.Enumerator;

            if(e1.TryGetNonEnumeratedCount(out var count1)&&e2.TryGetNonEnumeratedCount(out var count2)&&count1!=count2) {
                return false;
            }

#if NET8_0_OR_GREATER
            if(e1.TryGetSpan(out var sourceSpan)&&e2.TryGetSpan(out var secondSpan)) {
                return sourceSpan.SequenceEqual(secondSpan,comparer); // SIMD acceleration
            }
#endif

            comparer??=EqualityComparer<TSource>.Default;
            while(e1.TryGetNext(out var value1)) {
                if(!(e2.TryGetNext(out var value2)&&comparer.Equals(value1,value2))) {
                    return false;
                }
            }

            return !e2.TryGetNext(out _);
        }
    }
}
