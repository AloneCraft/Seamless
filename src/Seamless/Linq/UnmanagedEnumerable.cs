using System.Runtime.CompilerServices;
using Seamless.Collections;
using Seamless.Stores;

namespace Seamless.Linq;

public static class UnmanagedEnumerable {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> OwnTemporaryResultList<T>(UnmanagedList<T> list,string path)
        where T : unmanaged =>
        list;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,TResult> selector)
        where TResult : unmanaged =>
        Enumerable.Select(source,selector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(IEnumerable<TSource> source)
        where TResult : unmanaged
        where TSelectorStrategy : struct, IFunction<TSource,TResult> =>
        Enumerable.Select(source,static value => TSelectorStrategy.Invoke(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source,Func<TSource,bool> predicate)
        where TSource : unmanaged =>
        Enumerable.Where(source,predicate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TPredicateStrategy>(IEnumerable<TSource> source)
        where TSource : unmanaged
        where TPredicateStrategy : struct, IFunction<TSource,bool> =>
        Enumerable.Where(source,static value => TPredicateStrategy.Invoke(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector)
        where TResult : unmanaged =>
        Enumerable.SelectMany(source,selector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
        where TResult : unmanaged =>
        Enumerable.SelectMany(source,collectionSelector,resultSelector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer = null)
        where TResult : unmanaged =>
        Enumerable.Join(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
}
