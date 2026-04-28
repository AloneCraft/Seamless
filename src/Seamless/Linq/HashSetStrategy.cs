namespace Seamless.Linq;

public static partial class HashSet {

    public static TSource Aggregate<TSource, TFuncStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TFuncStrategy : struct,IFunction<TSource,TSource,TSource>
    {
        return HashSet.Aggregate(source,static (value1,value2) => TFuncStrategy.Invoke(value1,value2));
    }

    public static TAccumulate Aggregate<TSource, TAccumulate, TFuncStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,TAccumulate seed)
        where TSource : global::System.IEquatable<TSource>
        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
    {
        return HashSet.Aggregate(source,seed,static (value1,value2) => TFuncStrategy.Invoke(value1,value2));
    }

    public static TResult Aggregate<TSource, TAccumulate, TResult, TFuncStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,TAccumulate seed)
        where TSource : global::System.IEquatable<TSource>
        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
        where TResultSelectorStrategy : struct,IFunction<TAccumulate,TResult>
    {
        return HashSet.Aggregate(source,seed,static (value1,value2) => TFuncStrategy.Invoke(value1,value2),static value1 => TResultSelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate, TKeySelectorStrategy, TFuncStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,TAccumulate seed,global::System.Collections.Generic.IEqualityComparer<TKey>? keyComparer = null)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
    {
        return HashSet.AggregateBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),seed,static (value1,value2) => TFuncStrategy.Invoke(value1,value2),keyComparer);
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate, TKeySelectorStrategy, TSeedSelectorStrategy, TFuncStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? keyComparer = null)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TSeedSelectorStrategy : struct,IFunction<TKey,TAccumulate>
        where TFuncStrategy : struct,IFunction<TAccumulate,TSource,TAccumulate>
    {
        return HashSet.AggregateBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TSeedSelectorStrategy.Invoke(value1),static (value1,value2) => TFuncStrategy.Invoke(value1,value2),keyComparer);
    }

    public static bool All<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.All(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static bool Any<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.Any(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static decimal Average<TSource, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,decimal>
    {
        return HashSet.Average(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static int Count<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.Count(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<TKey,int>> CountBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? keyComparer = null)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.CountBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),keyComparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> DistinctBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.DistinctBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> DistinctBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.DistinctBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> ExceptBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.ExceptBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> ExceptBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.ExceptBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static TSource? FirstOrDefault<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.FirstOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static TSource FirstOrDefault<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.FirstOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1),defaultValue);
    }

    public static TSource First<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.First(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TSource>> GroupBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TSource>> GroupBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
    {
        return HashSet.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
    {
        return HashSet.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult, TKeySelectorStrategy, TElementSelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
        where TResultSelectorStrategy : struct,IFunction<TKey,global::System.Collections.Generic.IEnumerable<TElement>,TResult>
    {
        return HashSet.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult, TKeySelectorStrategy, TElementSelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
        where TResultSelectorStrategy : struct,IFunction<TKey,global::System.Collections.Generic.IEnumerable<TElement>,TResult>
    {
        return HashSet.GroupBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter,global::System.Collections.Generic.IEnumerable<TInner>,TResult>
    {
        return HashSet.GroupJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter,global::System.Collections.Generic.IEnumerable<TInner>,TResult>
    {
        return HashSet.GroupJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> IntersectBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.IntersectBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> IntersectBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.IntersectBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner,TResult>
    {
        return HashSet.Join(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner,TResult>
    {
        return HashSet.Join(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
    }

    public static TSource? LastOrDefault<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.LastOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static TSource LastOrDefault<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.LastOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1),defaultValue);
    }

    public static TSource Last<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.Last(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner?,TResult>
    {
        return HashSet.LeftJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter,TInner?,TResult>
    {
        return HashSet.LeftJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
    }

    public static long LongCount<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.LongCount(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static TSource? MaxBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.MaxBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static TSource? MaxBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.MaxBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static decimal Max<TSource, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,decimal>
    {
        return HashSet.Max(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static TResult? Max<TSource, TResult, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,TResult>
    {
        return HashSet.Max(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static TSource? MinBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.MinBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static TSource? MinBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.MinBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static decimal Min<TSource, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,decimal>
    {
        return HashSet.Min(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static TResult? Min<TSource, TResult, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,TResult>
    {
        return HashSet.Min(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static global::System.Linq.IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.OrderByDescending(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Linq.IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.OrderByDescending(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Linq.IOrderedEnumerable<TSource> OrderBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.OrderBy(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Linq.IOrderedEnumerable<TSource> OrderBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.OrderBy(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter?,TInner,TResult>
    {
        return HashSet.RightJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult, TOuterKeySelectorStrategy, TInnerKeySelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner>
        where TOuterKeySelectorStrategy : struct,IFunction<TOuter,TKey>
        where TInnerKeySelectorStrategy : struct,IFunction<TInner,TKey>
        where TResultSelectorStrategy : struct,IFunction<TOuter?,TInner,TResult>
    {
        return HashSet.RightJoin(outer,inner,static value1 => TOuterKeySelectorStrategy.Invoke(value1),static value1 => TInnerKeySelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> SelectMany<TSource, TResult, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,global::System.Collections.Generic.IEnumerable<TResult>>
    {
        return HashSet.SelectMany(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> SelectMany<TSource, TCollection, TResult, TCollectionSelectorStrategy, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TCollectionSelectorStrategy : struct,IFunction<TSource,global::System.Collections.Generic.IEnumerable<TCollection>>
        where TResultSelectorStrategy : struct,IFunction<TSource,TCollection,TResult>
    {
        return HashSet.SelectMany(source,static value1 => TCollectionSelectorStrategy.Invoke(value1),static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> Select<TSource, TResult, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,int,TResult>
    {
        return HashSet.Select(source,static (value1,value2) => TSelectorStrategy.Invoke(value1,value2));
    }

    public static TSource? SingleOrDefault<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.SingleOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static TSource SingleOrDefault<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.SingleOrDefault(source,static value1 => TPredicateStrategy.Invoke(value1),defaultValue);
    }

    public static TSource Single<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.Single(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> SkipWhile<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.SkipWhile(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static decimal Sum<TSource, TSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TSelectorStrategy : struct,IFunction<TSource,decimal>
    {
        return HashSet.Sum(source,static value1 => TSelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> TakeWhile<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.TakeWhile(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.Dictionary<TKey,TSource> ToDictionary<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.Dictionary<TKey,TSource> ToDictionary<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
    {
        return HashSet.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
    {
        return HashSet.ToDictionary(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Linq.ILookup<TKey,TSource> ToLookup<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.ToLookup(source,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Linq.ILookup<TKey,TSource> ToLookup<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.ToLookup(source,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Linq.ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
    {
        return HashSet.ToLookup(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1));
    }

    public static global::System.Linq.ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement, TKeySelectorStrategy, TElementSelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
        where TElementSelectorStrategy : struct,IFunction<TSource,TElement>
    {
        return HashSet.ToLookup(source,static value1 => TKeySelectorStrategy.Invoke(value1),static value1 => TElementSelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> UnionBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.UnionBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> UnionBy<TSource, TKey, TKeySelectorStrategy>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKeySelectorStrategy : struct,IFunction<TSource,TKey>
    {
        return HashSet.UnionBy(first,second,static value1 => TKeySelectorStrategy.Invoke(value1),comparer);
    }

    public static global::System.Collections.Generic.IEnumerable<TSource> Where<TSource, TPredicateStrategy>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource>
        where TPredicateStrategy : struct,IFunction<TSource,bool>
    {
        return HashSet.Where(source,static value1 => TPredicateStrategy.Invoke(value1));
    }

    public static global::System.Collections.Generic.IEnumerable<TResult> Zip<TFirst, TSecond, TResult, TResultSelectorStrategy>(this global::Seamless.Collections.IHashSet<TFirst> first,global::Seamless.Collections.IHashSet<TSecond> second)
        where TFirst : global::System.IEquatable<TFirst>
        where TSecond : global::System.IEquatable<TSecond>
        where TResultSelectorStrategy : struct,IFunction<TFirst,TSecond,TResult>
    {
        return HashSet.Zip(first,second,static (value1,value2) => TResultSelectorStrategy.Invoke(value1,value2));
    }
}
