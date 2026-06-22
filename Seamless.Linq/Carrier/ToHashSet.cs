namespace Seamless.Linq {
    partial class ValueEnumerableExtensions {
        public static global::System.Collections.Generic.HashSet<TSource> ToHashSet<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            return ToHashSet<TEnumerator,TSource>(source,null);
        }

        public static global::System.Collections.Generic.HashSet<TSource> ToHashSet<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            using var enumerator = source.Enumerator;
            if(enumerator.TryGetSpan(out var span)) {
#if NETSTANDARD2_0
                var hashSet = new global::System.Collections.Generic.HashSet<TSource>(comparer);
#else
                var hashSet = new global::System.Collections.Generic.HashSet<TSource>(span.Length,comparer);
#endif
                foreach(var item in span) {
                    hashSet.Add(item);
                }
                return hashSet;
            }
            else {
#if NETSTANDARD2_0
                var hashSet = new global::System.Collections.Generic.HashSet<TSource>(comparer);
#else
                var hashSet = enumerator.TryGetNonEnumeratedCount(out var count)
                    ? new global::System.Collections.Generic.HashSet<TSource>(count,comparer)
                    : new global::System.Collections.Generic.HashSet<TSource>(comparer);
#endif
                while(enumerator.TryGetNext(out var item)) {
                    hashSet.Add(item);
                }
                return hashSet;
            }
        }

        internal static HashSetSlim<TSource> ToHashSetSlim<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            using var enumerator = source.Enumerator;
            if(enumerator.TryGetSpan(out var span)) {
                var hashSet = new HashSetSlim<TSource>(span.Length,comparer);
                foreach(var item in span) {
                    hashSet.Add(item);
                }
                return hashSet;
            }
            else {
                var hashSet = enumerator.TryGetNonEnumeratedCount(out var count)
                    ? new HashSetSlim<TSource>(count,comparer)
                    : new HashSetSlim<TSource>(comparer);
                while(enumerator.TryGetNext(out var item)) {
                    hashSet.Add(item);
                }
                return hashSet;
            }
        }
    }
}
