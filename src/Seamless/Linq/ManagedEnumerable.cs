using System.Runtime.CompilerServices;
using Seamless.Collections;
using Seamless.Stores;

namespace Seamless.Linq;

public static class ManagedEnumerable {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ManagedList<T> CreateTemporaryResultList<T>(string path,long initialCapacity = 16,long initialDataCapacity = 4096,ManagedTag _ = default) =>
        Enumerable.CreateTemporaryResultList<T>(path,initialCapacity,initialDataCapacity,_);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> OwnTemporaryResultList<T>(ManagedList<T> list,string path) =>
        Enumerable.OwnTemporaryResultList(list,path);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,TResult> selector) =>
        StorageEnumerableCore.Select<TSource,TResult,ManagedList<TResult>,ManagedTemporaryResultListFactory<TResult>>(source,selector,"managed-select");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(IEnumerable<TSource> source)
        where TSelectorStrategy : struct, IFunction<TSource,TResult> =>
        StorageEnumerableCore.Select<TSource,TResult,ManagedList<TResult>,ManagedTemporaryResultListFactory<TResult>,TSelectorStrategy>(source,"managed-select-strategy");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource>(IEnumerable<TSource> source,Func<TSource,bool> predicate) =>
        StorageEnumerableCore.Where<TSource,ManagedList<TSource>,ManagedTemporaryResultListFactory<TSource>>(source,predicate,"managed-where");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TPredicateStrategy>(IEnumerable<TSource> source)
        where TPredicateStrategy : struct, IFunction<TSource,bool> =>
        StorageEnumerableCore.Where<TSource,ManagedList<TSource>,ManagedTemporaryResultListFactory<TSource>,TPredicateStrategy>(source,"managed-where-strategy");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector) =>
        StorageEnumerableCore.SelectMany<TSource,TResult,ManagedList<TResult>,ManagedTemporaryResultListFactory<TResult>>(source,selector,"managed-select-many");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) =>
        StorageEnumerableCore.SelectMany<TSource,TCollection,TResult,ManagedList<TResult>,ManagedTemporaryResultListFactory<TResult>>(source,collectionSelector,resultSelector,"managed-select-many-result");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer = null) =>
        StorageEnumerableCore.Join<TOuter,TInner,TKey,TResult,ManagedList<TResult>,ManagedTemporaryResultListFactory<TResult>>(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer,"managed-join");
}
