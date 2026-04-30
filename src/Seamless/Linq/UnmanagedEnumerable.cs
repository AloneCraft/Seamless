using System.Runtime.CompilerServices;
using Seamless.Collections;
using Seamless.Stores;

namespace Seamless.Linq;

public static class UnmanagedEnumerable {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedList<T> CreateTemporaryResultList<T>(string path,long initialCapacity = 16,UnmanagedTag _ = default)
        where T : unmanaged =>
        Enumerable.CreateTemporaryResultList<T>(path,initialCapacity,_);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> OwnTemporaryResultList<T>(UnmanagedList<T> list,string path)
        where T : unmanaged =>
        Enumerable.OwnTemporaryResultList(list,path);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,TResult> selector)
        where TResult : unmanaged =>
        StorageEnumerableCore.Select<TSource,TResult,UnmanagedList<TResult>,UnmanagedTemporaryResultListFactory<TResult>>(source,selector,"unmanaged-select");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(IEnumerable<TSource> source)
        where TResult : unmanaged
        where TSelectorStrategy : struct, IFunction<TSource,TResult> =>
        StorageEnumerableCore.Select<TSource,TResult,UnmanagedList<TResult>,UnmanagedTemporaryResultListFactory<TResult>,TSelectorStrategy>(source,"unmanaged-select-strategy");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source,Func<TSource,bool> predicate)
        where TSource : unmanaged =>
        StorageEnumerableCore.Where<TSource,UnmanagedList<TSource>,UnmanagedTemporaryResultListFactory<TSource>>(source,predicate,"unmanaged-where");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TPredicateStrategy>(IEnumerable<TSource> source)
        where TSource : unmanaged
        where TPredicateStrategy : struct, IFunction<TSource,bool> =>
        StorageEnumerableCore.Where<TSource,UnmanagedList<TSource>,UnmanagedTemporaryResultListFactory<TSource>,TPredicateStrategy>(source,"unmanaged-where-strategy");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector)
        where TResult : unmanaged =>
        StorageEnumerableCore.SelectMany<TSource,TResult,UnmanagedList<TResult>,UnmanagedTemporaryResultListFactory<TResult>>(source,selector,"unmanaged-select-many");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector)
        where TResult : unmanaged =>
        StorageEnumerableCore.SelectMany<TSource,TCollection,TResult,UnmanagedList<TResult>,UnmanagedTemporaryResultListFactory<TResult>>(source,collectionSelector,resultSelector,"unmanaged-select-many-result");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer = null)
        where TResult : unmanaged =>
        StorageEnumerableCore.Join<TOuter,TInner,TKey,TResult,UnmanagedList<TResult>,UnmanagedTemporaryResultListFactory<TResult>>(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer,"unmanaged-join");
}
