using System.Runtime.CompilerServices;
using Seamless.Collections;

namespace Seamless.Linq;

public static class ManagedHashSetEnumerable {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult>(IHashSet<TSource> source,Func<TSource,TResult> selector) =>
        ManagedEnumerable.Select(source,selector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(IHashSet<TSource> source)        where TSelectorStrategy : struct, IFunction<TSource,TResult> =>
        ManagedEnumerable.Select<TSource,TResult,TSelectorStrategy>(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource>(IHashSet<TSource> source,Func<TSource,bool> predicate) =>
        ManagedEnumerable.Where(source,predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TPredicateStrategy>(IHashSet<TSource> source)        where TPredicateStrategy : struct, IFunction<TSource,bool> =>
        ManagedEnumerable.Where<TSource,TPredicateStrategy>(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(IHashSet<TSource> source,Func<TSource,IEnumerable<TResult>> selector) =>
        ManagedEnumerable.SelectMany(source,selector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(IHashSet<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) =>
        ManagedEnumerable.SelectMany(source,collectionSelector,resultSelector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer = null) =>
        ManagedEnumerable.Join(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
}
