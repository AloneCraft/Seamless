#if NET8_0_OR_GREATER

namespace Seamless.Linq;

partial class ValueEnumerableExtensions {
    // FrozenDictionary

    public static FrozenDictionary<TKey,TValue> ToFrozenDictionary<TEnumerator, TKey, TValue>(this ValueEnumerable<TEnumerator,KeyValuePair<TKey,TValue>> source,IEqualityComparer<TKey>? comparer = null)
       where TEnumerator : struct, IValueEnumerator<KeyValuePair<TKey,TValue>>
       where TKey : notnull {
        Dictionary<TKey,TValue> dict;

        using var e = source.Enumerator;

        if(e.TryGetSpan(out var span)) {
            dict=new Dictionary<TKey,TValue>(span.Length,comparer);

            foreach(var pair in span) {
                // dotnet ToFrozenDictionary(kvp) uses indexer to avoid duplicate throw
                dict[pair.Key]=pair.Value;
            }
        }
        else {
            dict=e.TryGetNonEnumeratedCount(out var count)
                ? new Dictionary<TKey,TValue>(count,comparer)
                : new Dictionary<TKey,TValue>(comparer);

            while(e.TryGetNext(out var pair)) {
                dict[pair.Key]=pair.Value;
            }
        }

        return dict.ToFrozenDictionary(comparer);
    }

    public static FrozenDictionary<TKey,TSource> ToFrozenDictionary<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer = null)
       where TEnumerator : struct, IValueEnumerator<TSource>
       where TKey : notnull {
        return source.ToDictionary(keySelector,comparer).ToFrozenDictionary(comparer);
    }

    public static FrozenDictionary<TKey,TElement> ToFrozenDictionary<TEnumerator, TSource, TKey, TElement>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer = null)
       where TEnumerator : struct, IValueEnumerator<TSource>
       where TKey : notnull {

        return source.ToDictionary(keySelector,elementSelector,comparer).ToFrozenDictionary(comparer);
    }

    // FrozenSet

    public static FrozenSet<TSource> ToFrozenSet<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEqualityComparer<TSource>? comparer = null)
       where TEnumerator : struct, IValueEnumerator<TSource> {
        return source.ToHashSet(comparer).ToFrozenSet(comparer);
    }
}

#endif
