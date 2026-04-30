using System.Runtime.CompilerServices;

namespace Seamless.Linq;

internal interface ITemporaryResultListFactory<T, TList>
    where TList : Collections.List<T> {
    static abstract TList Create(string path,long initialCapacity,long initialDataCapacity);
}

internal readonly struct ManagedTemporaryResultListFactory<T>:ITemporaryResultListFactory<T,Collections.ManagedList<T>> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Collections.ManagedList<T> Create(string path,long initialCapacity,long initialDataCapacity) =>
        Collections.ListStore.Create<T>(path,initialCapacity,initialDataCapacity);
}

internal readonly struct UnmanagedTemporaryResultListFactory<T>:ITemporaryResultListFactory<T,Collections.UnmanagedList<T>>
    where T : unmanaged {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Collections.UnmanagedList<T> Create(string path,long initialCapacity,long initialDataCapacity) =>
        Collections.ListStore.Create<T>(path,initialCapacity);
}

internal static class StorageEnumerableCore {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TList, TFactory>(IEnumerable<TSource> source,Func<TSource,TResult> selector,string operationName)
        where TList : Collections.List<TResult>
        where TFactory : struct, ITemporaryResultListFactory<TResult,TList> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            foreach(var 入力値 in source) {
                結果.Add(selector(入力値));
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Select<TSource, TResult, TList, TFactory, TSelectorStrategy>(IEnumerable<TSource> source,string operationName)
        where TList : Collections.List<TResult>
        where TFactory : struct, ITemporaryResultListFactory<TResult,TList>
        where TSelectorStrategy : struct, IFunction<TSource,TResult> {
        ArgumentNullException.ThrowIfNull(source);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            foreach(var 入力値 in source) {
                結果.Add(TSelectorStrategy.Invoke(入力値));
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TList, TFactory>(IEnumerable<TSource> source,Func<TSource,bool> predicate,string operationName)
        where TList : Collections.List<TSource>
        where TFactory : struct, ITemporaryResultListFactory<TSource,TList> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            foreach(var 入力値 in source) {
                if(predicate(入力値)) {
                    結果.Add(入力値);
                }
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TSource> Where<TSource, TList, TFactory, TPredicateStrategy>(IEnumerable<TSource> source,string operationName)
        where TList : Collections.List<TSource>
        where TFactory : struct, ITemporaryResultListFactory<TSource,TList>
        where TPredicateStrategy : struct, IFunction<TSource,bool> {
        ArgumentNullException.ThrowIfNull(source);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            foreach(var 入力値 in source) {
                if(TPredicateStrategy.Invoke(入力値)) {
                    結果.Add(入力値);
                }
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TResult, TList, TFactory>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector,string operationName)
        where TList : Collections.List<TResult>
        where TFactory : struct, ITemporaryResultListFactory<TResult,TList> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            foreach(var 外側値 in source) {
                foreach(var 内側値 in selector(外側値)) {
                    結果.Add(内側値);
                }
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult, TList, TFactory>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector,string operationName)
        where TList : Collections.List<TResult>
        where TFactory : struct, ITemporaryResultListFactory<TResult,TList> {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            foreach(var 外側値 in source) {
                foreach(var 内側値 in collectionSelector(外側値)) {
                    結果.Add(resultSelector(外側値,内側値));
                }
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult, TList, TFactory>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer,string operationName)
        where TList : Collections.List<TResult>
        where TFactory : struct, ITemporaryResultListFactory<TResult,TList> {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        var 結果パス = Enumerable.CreateTemporaryResultListPath(operationName);
        var 結果 = TFactory.Create(結果パス,16,4096);
        try {
            var 照合表 = Enumerable.ToLookup(inner,innerKeySelector,static item => item,comparer);
            foreach(var 外側値 in outer) {
                foreach(var 内側値 in 照合表[outerKeySelector(外側値)]) {
                    結果.Add(resultSelector(外側値,内側値));
                }
            }
            return Enumerable.OwnTemporaryResultList(結果,結果パス);
        }
        catch {
            結果.Dispose();
            Enumerable.DeleteTemporaryResultListPath(結果パス);
            throw;
        }
    }
}
