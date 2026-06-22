#if NET8_0_OR_GREATER

using GC = System.GC;

namespace Seamless.Collections.Tsavorite.Carrier;

partial class ValueEnumerableExtensions {
    // ImmutableArray
    // ImmutableList
    // ImmutableDictionary
    // ImmutableSortedDictionary
    // ImmutableHashSet
    // ImmutableSortedSet

    public static ImmutableArray<TSource> ToImmutableArray<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        using var e = source.Enumerator;

        if(e.TryGetSpan(out var span)) {
            return [.. span];
        }
        else {
            if(e.TryGetNonEnumeratedCount(out var count)) {
                var array = GC.AllocateUninitializedArray<TSource>(count);

                if(e.TryCopyTo(array,offset: 0)) {
                    return ImmutableCollectionsMarshal.AsImmutableArray(array);
                }
                else {
                    var i = 0;
                    while(e.TryGetNext(out var current)) {
                        array[i]=current;
                        i++;
                    }
                    return ImmutableCollectionsMarshal.AsImmutableArray(array);
                }
            }
            else {
                var builder = ImmutableArray.CreateBuilder<TSource>();
                while(e.TryGetNext(out var current)) {
                    builder.Add(current);
                }
                return builder.ToImmutable();
            }
        }
    }

    public static ImmutableList<TSource> ToImmutableList<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        using var e = source.Enumerator;

        if(e.TryGetSpan(out var span)) {
            return [.. span];
        }
        else {
            var builder = ImmutableList.CreateBuilder<TSource>();
            while(e.TryGetNext(out var current)) {
                builder.Add(current);
            }
            return builder.ToImmutable();
        }
    }

    public static ImmutableDictionary<TKey,TSource> ToImmutableDictionary<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer = null)
       where TEnumerator : struct, IValueEnumerator<TSource>
       where TKey : notnull {
        using var e = source.Enumerator;

        var builder = ImmutableDictionary.CreateBuilder<TKey,TSource>(comparer);

        if(e.TryGetSpan(out var span)) {
            foreach(var current in span) {
                builder.Add(keySelector(current),current);
            }
        }
        else {
            while(e.TryGetNext(out var current)) {
                builder.Add(keySelector(current),current);
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableDictionary<TKey,TValue> ToImmutableDictionary<TEnumerator, TSource, TKey, TValue>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TValue> elementSelector,IEqualityComparer<TKey>? keyComparer = null,IEqualityComparer<TValue>? valueComparer = null)
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TKey : notnull {
        using var e = source.Enumerator;

        var builder = ImmutableDictionary.CreateBuilder<TKey,TValue>(keyComparer,valueComparer);

        if(e.TryGetSpan(out var span)) {
            foreach(var current in span) {
                builder.Add(keySelector(current),elementSelector(current));
            }
        }
        else {
            while(e.TryGetNext(out var current)) {
                builder.Add(keySelector(current),elementSelector(current));
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableDictionary<TKey,TValue> ToImmutableDictionary<TEnumerator, TKey, TValue>(this ValueEnumerable<TEnumerator,KeyValuePair<TKey,TValue>> source,IEqualityComparer<TKey>? keyComparer = null,IEqualityComparer<TValue>? valueComparer = null)
        where TEnumerator : struct, IValueEnumerator<KeyValuePair<TKey,TValue>>
        where TKey : notnull {
        using var e = source.Enumerator;

        var builder = ImmutableDictionary.CreateBuilder<TKey,TValue>(keyComparer,valueComparer);

        if(e.TryGetSpan(out var span)) {
            foreach(var current in span) {
                builder.Add(current.Key,current.Value);
            }
        }
        else {
            while(e.TryGetNext(out var current)) {
                builder.Add(current.Key,current.Value);
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableSortedDictionary<TKey,TSource> ToImmutableSortedDictionary<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer = null)
       where TEnumerator : struct, IValueEnumerator<TSource>
       where TKey : notnull {
        using var e = source.Enumerator;

        var builder = ImmutableSortedDictionary.CreateBuilder<TKey,TSource>(comparer);

        if(e.TryGetSpan(out var span)) {
            foreach(var current in span) {
                builder.Add(keySelector(current),current);
            }
        }
        else {
            while(e.TryGetNext(out var current)) {
                builder.Add(keySelector(current),current);
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableSortedDictionary<TKey,TValue> ToImmutableSortedDictionary<TEnumerator, TSource, TKey, TValue>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TValue> elementSelector,IComparer<TKey>? keyComparer = null,IEqualityComparer<TValue>? valueComparer = null)
        where TEnumerator : struct, IValueEnumerator<TSource>
        where TKey : notnull {
        using var e = source.Enumerator;

        var builder = ImmutableSortedDictionary.CreateBuilder<TKey,TValue>(keyComparer,valueComparer);

        if(e.TryGetSpan(out var span)) {
            foreach(var current in span) {
                builder.Add(keySelector(current),elementSelector(current));
            }
        }
        else {
            while(e.TryGetNext(out var current)) {
                builder.Add(keySelector(current),elementSelector(current));
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableSortedDictionary<TKey,TValue> ToImmutableSortedDictionary<TEnumerator, TKey, TValue>(this ValueEnumerable<TEnumerator,KeyValuePair<TKey,TValue>> source,IComparer<TKey>? keyComparer = null,IEqualityComparer<TValue>? valueComparer = null)
        where TEnumerator : struct, IValueEnumerator<KeyValuePair<TKey,TValue>>
        where TKey : notnull {
        using var e = source.Enumerator;

        var builder = ImmutableSortedDictionary.CreateBuilder<TKey,TValue>(keyComparer,valueComparer);

        if(e.TryGetSpan(out var span)) {
            foreach(var current in span) {
                builder.Add(current.Key,current.Value);
            }
        }
        else {
            while(e.TryGetNext(out var current)) {
                builder.Add(current.Key,current.Value);
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableHashSet<TSource> ToImmutableHashSet<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEqualityComparer<TSource>? equalityComparer = null)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        using var e = source.Enumerator;

        if(e.TryGetSpan(out var span)) {
            return ImmutableHashSet.Create(equalityComparer,span);
        }
        else {
            var builder = ImmutableHashSet.CreateBuilder<TSource>(equalityComparer);

            while(e.TryGetNext(out var current)) {
                builder.Add(current);
            }

            return builder.ToImmutable();
        }
    }

    public static ImmutableSortedSet<TSource> ToImmutableSortedSet<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IComparer<TSource>? comparer = null)
        where TEnumerator : struct, IValueEnumerator<TSource> {
        using var e = source.Enumerator;

        if(e.TryGetSpan(out var span)) {
            return ImmutableSortedSet.Create(comparer,span);
        }
        else {
            var builder = ImmutableSortedSet.CreateBuilder<TSource>(comparer);

            while(e.TryGetNext(out var current)) {
                builder.Add(current);
            }

            return builder.ToImmutable();
        }
    }
}

#endif
