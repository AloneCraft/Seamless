using System.Runtime.CompilerServices;
using Seamless.Collections;

namespace Seamless.Linq;

public static class ManagedHashSetEnumerable {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult>(IHashSet<TSource> source,Func<TSource,TResult> selector) =>
        Enumerable.Select((IEnumerable<TSource>)source,selector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(IHashSet<TSource> source)        where TSelectorStrategy : struct, IFunction<TSource,TResult> =>
        Enumerable.Select((IEnumerable<TSource>)source,static value => TSelectorStrategy.Invoke(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource>(IHashSet<TSource> source,Func<TSource,bool> predicate) =>
        Enumerable.Where((IEnumerable<TSource>)source,predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TPredicateStrategy>(IHashSet<TSource> source)        where TPredicateStrategy : struct, IFunction<TSource,bool> =>
        Enumerable.Where((IEnumerable<TSource>)source,static value => TPredicateStrategy.Invoke(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(IHashSet<TSource> source,Func<TSource,IEnumerable<TResult>> selector) =>
        Enumerable.SelectMany((IEnumerable<TSource>)source,selector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(IHashSet<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) =>
        Enumerable.SelectMany((IEnumerable<TSource>)source,collectionSelector,resultSelector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer = null) =>
        Enumerable.Join((IEnumerable<TOuter>)outer,(IEnumerable<TInner>)inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
}
