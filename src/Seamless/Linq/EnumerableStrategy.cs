//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace Seamless.Linq;

//public interface IFunction<TResult> {
//    static abstract TResult Invoke();
//}

//public interface IFunction<T1,TResult> {
//    static abstract TResult Invoke(T1 value1);
//}

//public interface IFunction<T1,T2,TResult> {
//    static abstract TResult Invoke(T1 value1,T2 value2);
//}

//public interface IFunction<T1,T2,T3,TResult> {
//    static abstract TResult Invoke(T1 value1,T2 value2,T3 value3);
//}

//public static partial class Enumerable {

//    public static TSource Aggregate<TSource, TFuncStrategy>(this IEnumerable<TSource> source)
//        where TFuncStrategy : struct,IFunction<TSource,TSource,TSource>
//    {
//        return Enumerable.Aggregate(source,static (value1,value2) => TFuncStrategy.Invoke(value1,value2));
//    }

//    public static TAccumulate Aggregate<TSource, TAccumulate, TFuncStrategy>(this IEnumerable<TSource> source,TAccumulate seed)
//        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
//    {
//        return Enumerable.Aggregate(source,seed,static (value1,value2) => TFuncStrategy.Invoke(value1,value2));
//    }

//    public static TResult Aggregate<TSource, TAccumulate, TResult, TFuncStrategy, TResultSelectorStrategy>(this IEnumerable<TSource> source,TAccumulate seed)
//        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
//        where TResultSelectorStrategy : struct,IFunction<TAccumulate,TResult>
//    {
//        return Enumerable.Aggregate(source,seed,static (value1,value2) => TFuncStrategy.Invoke(value1,value2),static value1 => TResultSelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate, TKeySelectorStrategy, TFuncStrategy>(this IEnumerable<TSource> source,TAccumulate seed,IEqualityComparer<TKey>? keyComparer = null)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
//    {
//        return Enumerable.AggregateBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),seed,static (value1,value2) => TFuncStrategy.Invoke(value1,value2),keyComparer);
//    }

//    public static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate, TKeySelectorStrategy, TSeedSelectorStrategy, TFuncStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? keyComparer = null)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TSeedSelectorStrategy : struct,IFunction<TKey,TAccumulate>
//        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
//    {
//        return Enumerable.AggregateBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TSeedSelectorStrategy.Invoke(value1),static (value1,value2) => TFuncStrategy.Invoke(value1,value2),keyComparer);
//    }

//    public static bool All<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.All(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static bool Any<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.Any(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static decimal Average<TSource, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,decimal>
//    {
//        return Enumerable.Average(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static int Count<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.Count(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static IEnumerable<KeyValuePair<TKey,int>> CountBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? keyComparer = null)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.CountBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),keyComparer);
//    }

//    public static IEnumerable<TSource> DistinctBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.DistinctBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TSource> DistinctBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey> {
//        Func<TSource,TKey> keySelector=static value1 => TKeySelectorStrategy.Invoke(value1);
//        var                result     =new List<TSource>();
//        var                seen       =new HashSet<TKey>(comparer);
//        foreach(var item in source) {
//            if(seen.Add(keySelector(item))) {
//                result.Add(item);
//            }
//        }
//        return result;
//    }

//    public static IEnumerable<TSource> ExceptBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> first,IEnumerable<TKey> second)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ExceptBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TSource> ExceptBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> first,IEnumerable<TKey> second,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey> {
//        Func<TSource,TKey> keySelector=static value1 => TKeySelectorStrategy.Invoke(value1);
//        var                result     =new List<TSource>();
//        var                excluded   =new HashSet<TKey>(second,comparer);
//        foreach(var item in first) {
//            if(excluded.Add(keySelector(item))) {
//                result.Add(item);
//            }
//        }
//        return result;
//    }

//    public static TSource? FirstOrDefault<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.FirstOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static TSource FirstOrDefault<TSource, TPredicateStrategy>(this IEnumerable<TSource> source,TSource defaultValue)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.FirstOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1),defaultValue);
//    }

//    public static TSource First<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.First(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static IEnumerable<IGrouping<TKey,TSource>> GroupBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<IGrouping<TKey,TSource>> GroupBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static IEnumerable<IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
//    {
//        return Enumerable.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement> {
//        Func<TSource,TKey>     keySelector    =static value1 => TKeySelectorStrategy.Invoke(value1);
//        Func<TSource,TElement> elementSelector=static value1 => TElementSelectorStrategy.Invoke(value1);
//        var                    result         =new List<System.Linq.IGrouping<TKey,TElement>>();
//        var                    lookup1        =new LookupImpl<TKey,TElement>(comparer);
//        foreach(var item in source) {
//            lookup1.Add(keySelector(item),elementSelector(item));
//        }
//        var lookup=lookup1;
//        foreach(var group in lookup.Groups) {
//            result.Add(group);
//        }
//        return result;
//    }

//    public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult, TKeySelectorStrategy, TElementSelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
//        where TResultSelectorStrategy : struct,IFunction<TKey,IEnumerable<TElement>,TResult>
//    {
//        return Enumerable.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }

//    public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult, TKeySelectorStrategy, TElementSelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
//        where TResultSelectorStrategy : struct,IFunction<TKey,IEnumerable<TElement>,TResult>
//    {
//        return Enumerable.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
//    }

//    public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter,IEnumerable<TInner>,TResult>
//    {
//        return Enumerable.GroupJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }

//    public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,IEqualityComparer<TKey>? comparer)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter,IEnumerable<TInner>,TResult>
//    {
//        return Enumerable.GroupJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
//    }

//    public static IEnumerable<TSource> IntersectBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> first,IEnumerable<TKey> second)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.IntersectBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TSource> IntersectBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> first,IEnumerable<TKey> second,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.IntersectBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner,TResult>
//    {
//        return Enumerable.Join(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }

//    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,IEqualityComparer<TKey>? comparer)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner,TResult> {
//        Func<TOuter,TKey>           outerKeySelector=static value1 => TOuterKeySelectorStrategy.Invoke(value1);
//        Func<TInner,TKey>           innerKeySelector=static value1 => TInnerKeySelectorStrategy.Invoke(value1);
//        Func<TOuter,TInner,TResult> resultSelector  =static (value1,                                                                                                          value2) => TResultSelectorStrategy.Invoke(value1,value2);
//        var                         result          = new List<TResult>();
//        Func<TInner,TInner>         elementSelector =static item => item;
//        var                         lookup1         = new LookupImpl<TKey,TInner>(comparer);
//        foreach(var item in inner) {
//            lookup1.Add(innerKeySelector(item),elementSelector(item));
//        }
//        var                         lookup          = lookup1;
//        foreach(var outerItem in outer) {
//            foreach(var innerItem in lookup[outerKeySelector(outerItem)]) {
//                result.Add(resultSelector(outerItem,innerItem));
//            }
//        }
//        return result;
//    }

//    public static TSource? LastOrDefault<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.LastOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static TSource LastOrDefault<TSource, TPredicateStrategy>(this IEnumerable<TSource> source,TSource defaultValue)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.LastOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1),defaultValue);
//    }

//    public static TSource Last<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.Last(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner?,TResult>
//    {
//        return Enumerable.LeftJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }

//    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,IEqualityComparer<TKey>? comparer)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner?,TResult>
//    {
//        return Enumerable.LeftJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
//    }

//    public static long LongCount<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.LongCount(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static TSource? MaxBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.MaxBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static TSource? MaxBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.MaxBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static decimal Max<TSource, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,decimal>
//    {
//        return Enumerable.Max(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static TResult? Max<TSource, TResult, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,TResult>
//    {
//        return Enumerable.Max(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static TSource? MinBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.MinBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static TSource? MinBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.MinBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static decimal Min<TSource, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,decimal>
//    {
//        return Enumerable.Min(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static TResult? Min<TSource, TResult, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,TResult>
//    {
//        return Enumerable.Min(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.OrderByDescending(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.OrderByDescending(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.OrderBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.OrderBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter?,TInner,TResult>
//    {
//        return Enumerable.RightJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }

//    public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,IEqualityComparer<TKey>? comparer)
//        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
//        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
//        where TResultSelectorStrategy : struct,IFunction<TOuter?,TInner,TResult>
//    {
//        return Enumerable.RightJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
//    }

//    public static IEnumerable<TResult> SelectMany<TSource, TResult, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,IEnumerable<TResult>>
//    {
//        return Enumerable.SelectMany(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult, TCollectionSelectorStrategy, TResultSelectorStrategy>(this IEnumerable<TSource> source)
//        where TCollectionSelectorStrategy : struct,IFunction<TSource,IEnumerable<TCollection>>
//        where TResultSelectorStrategy : struct,IFunction<TSource,TCollection,TResult>
//    {
//        return Enumerable.SelectMany(source,static value1 => TCollectionSelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }

//    public static IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,int,TResult>
//    {
//        return Enumerable.Select(source,static (value1,value2) => TSelectorStrategy.Invoke(value1,value2));
//    }

//    public static TSource? SingleOrDefault<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.SingleOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static TSource SingleOrDefault<TSource, TPredicateStrategy>(this IEnumerable<TSource> source,TSource defaultValue)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.SingleOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1),defaultValue);
//    }

//    public static TSource Single<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.Single(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TSource> SkipWhile<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.SkipWhile(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static decimal Sum<TSource, TSelectorStrategy>(this IEnumerable<TSource> source)
//        where TSelectorStrategy : struct,IFunction<TSource,decimal>
//    {
//        return Enumerable.Sum(source,static value1 => TSelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TSource> TakeWhile<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.TakeWhile(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey, TKeySelectorStrategy>(this IOrderedEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ThenByDescending(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey, TKeySelectorStrategy>(this IOrderedEnumerable<TSource> source,IComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ThenByDescending(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static IOrderedEnumerable<TSource> ThenBy<TSource, TKey, TKeySelectorStrategy>(this IOrderedEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ThenBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IOrderedEnumerable<TSource> ThenBy<TSource, TKey, TKeySelectorStrategy>(this IOrderedEnumerable<TSource> source,IComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ThenBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static Dictionary<TKey,TSource> ToDictionary<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static Dictionary<TKey,TSource> ToDictionary<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this IEnumerable<TSource> source)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
//    {
//        return Enumerable.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1));
//    }

//    public static Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKey : notnull
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
//    {
//        return Enumerable.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),comparer);
//    }

//    public static ILookup<TKey,TSource> ToLookup<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.ToLookup(source,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static ILookup<TKey,TSource> ToLookup<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey> {
//        Func<TSource,TKey> keySelector=static value1 => TKeySelectorStrategy.Invoke(value1);
//        IEqualityComparer<TKey>? comparer1=comparer;
//        comparer1??=EqualityComparer<TKey>.Default;
//        var lookup=new LookupImpl<TKey,TSource>(comparer1);
//        foreach(var item in source) {
//            lookup.Add(keySelector(item),item);
//        }
//        return lookup;
//    }

//    public static ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this IEnumerable<TSource> source)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
//    {
//        return Enumerable.ToLookup(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1));
//    }

//    public static ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this IEnumerable<TSource> source,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//        where TElementSelectorStrategy : struct,IFunction<TSource,TElement> {
//        Func<TSource,TKey> keySelector=static value1 => TKeySelectorStrategy.Invoke(value1);
//        Func<TSource,TElement> elementSelector=static value1 => TElementSelectorStrategy.Invoke(value1);
//        var lookup=new LookupImpl<TKey,TElement>(comparer);
//        foreach(var item in source) {
//            lookup.Add(keySelector(item),elementSelector(item));
//        }
//        return lookup;
//    }

//    public static IEnumerable<TSource> UnionBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> first,IEnumerable<TSource> second)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.UnionBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TSource> UnionBy<TSource, TKey, TKeySelectorStrategy>(this IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TKey>? comparer)
//        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
//    {
//        return Enumerable.UnionBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
//    }

//    public static IEnumerable<TSource> Where<TSource, TPredicateStrategy>(this IEnumerable<TSource> source)
//        where TPredicateStrategy : struct,IFunction<TSource,bool>
//    {
//        return Enumerable.Where(source,static value1 => TPredicateStrategy.Invoke(value1));
//    }

//    public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult, TResultSelectorStrategy>(this IEnumerable<TFirst> first,IEnumerable<TSecond> second)
//        where TResultSelectorStrategy : struct,IFunction<TFirst,TSecond,TResult>
//    {
//        return Enumerable.Zip(first,second,static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
//    }
//}
