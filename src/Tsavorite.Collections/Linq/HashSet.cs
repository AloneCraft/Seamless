namespace Tsavorite.Linq;

using Collections;

using System.Collections;
using System.Numerics;

public static partial class HashSet {

    /// <summary>Applies an accumulator function over a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="func" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The final accumulator value.</returns>
    public static TSource Aggregate<TSource>(this IHashSet<TSource> source,Func<TSource,TSource,TSource> func) {
        using var enumerator = source.GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw NoElements();
        }
        var result = enumerator.Current;
        while(enumerator.MoveNext()) {
            result=func(result,enumerator.Current);
        }
        return result;
    }

    /// <summary>Applies an accumulator function over a sequence. The specified seed value is used as the initial accumulator value.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
    ///   <paramref name="source" /> or <paramref name="func" /> is <see langword="null" />.</exception>
    /// <returns>The final accumulator value.</returns>
    public static TAccumulate Aggregate<TSource, TAccumulate>(this IHashSet<TSource> source,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func) {
        var result = seed;
        foreach(var item in source) {
            result=func(result,item);
        }
        return result;
    }

    /// <summary>Applies an accumulator function over a sequence. The specified seed value is used as the initial accumulator value, and the specified function is used to select the result value.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <param name="resultSelector">A function to transform the final accumulator value into the result value.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
    /// <typeparam name="TResult">The type of the resulting value.</typeparam>
    ///   <paramref name="source" /> or <paramref name="func" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>The transformed final accumulator value.</returns>
    public static TResult Aggregate<TSource, TAccumulate, TResult>(this IHashSet<TSource> source,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,Func<TAccumulate,TResult> resultSelector) {
        return resultSelector(Aggregate(source,seed,func));
    }

    /// <summary>Applies an accumulator function over a sequence, grouping results by key.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <param name="keyComparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
    /// <returns>An enumerable containing the aggregates corresponding to each key deriving from <paramref name="source" />.</returns>
    public static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null)        where TKey : notnull {
        return AggregateByIterator(source,keySelector,seed,func,keyComparer);
    }

    /// <summary>Applies an accumulator function over a sequence, grouping results by key.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="seedSelector">A factory for the initial accumulator value.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <param name="keyComparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
    /// <returns>An enumerable containing the aggregates corresponding to each key deriving from <paramref name="source" />.</returns>
    public static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null)        where TKey : notnull {
        return AggregateByIterator(source,keySelector,seedSelector,func,keyComparer);
    }

    /// <summary>Determines whether all elements of a sequence satisfy a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, <see langword="false" />.</returns>
    public static bool All<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        foreach(var item in source) {
            if(!predicate(item)) return false;
        }
        return true;
    }

    /// <summary>Determines whether a sequence contains any elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to check for emptiness.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains any elements; otherwise, <see langword="false" />.</returns>
    public static bool Any<TSource>(this IHashSet<TSource> source) {
        return source.Count>0;
    }

    /// <summary>Determines whether any element of a sequence satisfies a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence is not empty and at least one of its elements passes the test in the specified predicate; otherwise, <see langword="false" />.</returns>
    public static bool Any<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        foreach(var item in source) {
            if(predicate(item)) return true;
        }
        return false;
    }

    /// <summary>Appends a value to the end of the sequence.</summary>
    /// <param name="source">A sequence of values.</param>
    /// <param name="element">The value to append to <paramref name="source" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new sequence that ends with <paramref name="element" />.</returns>
    public static IEnumerable<TSource> Append<TSource>(this IHashSet<TSource> source,TSource element) {
        return AppendIterator(source,element);
    }

    /// <summary>Returns the input typed as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">The sequence to type as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>The input sequence typed as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static IEnumerable<TSource> AsEnumerable<TSource>(this IHashSet<TSource> source) {
        return source;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to calculate the average of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static decimal Average(this IHashSet<decimal> source) {
        var sum=0m;
        var count=0L;
        foreach(var item in (IEnumerable<decimal>)source) {
            checked {
                sum+=item;
                count++;
            }
        }
        if(count==0)
            throw GeneratedCodeHelpers.NoElements();
        return sum/count;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to calculate the average of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this IHashSet<double> source) {
        var sum=0D;
        var count=0L;
        foreach(var item in (IEnumerable<double>)source){
            sum+=item;
            checked {
                count++;
            }
        }
        if(count==0)
            throw GeneratedCodeHelpers.NoElements();
        return sum/count;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to calculate the average of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this IHashSet<int> source) {
        return Enumerable.Average(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to calculate the average of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this IHashSet<long> source) {
        return Enumerable.Average(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to calculate the average of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static float Average(this IHashSet<float> source) {
        double sum = 0;
        long count = 0;
        foreach(var item in source) {
            sum+=item;
            checked {
                count++;
            }
        }
        if(count==0) {
            throw NoElements();
        }
        return (float)(sum/count);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate an average.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static decimal Average<TSource>(this IHashSet<TSource> source,Func<TSource,decimal> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average<TSource>(this IHashSet<TSource> source,Func<TSource,double> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average<TSource>(this IHashSet<TSource> source,Func<TSource,int> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average<TSource>(this IHashSet<TSource> source,Func<TSource,long> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static decimal? Average<TSource>(this IHashSet<TSource> source,Func<TSource,decimal?> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this IHashSet<TSource> source,Func<TSource,double?> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this IHashSet<TSource> source,Func<TSource,int?> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this IHashSet<TSource> source,Func<TSource,long?> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static float? Average<TSource>(this IHashSet<TSource> source,Func<TSource,float?> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static float Average<TSource>(this IHashSet<TSource> source,Func<TSource,float> selector) {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Splits the elements of a sequence into chunks of size at most <paramref name="size" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to chunk.</param>
    /// <param name="size">The maximum size of each chunk.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="size" /> is below 1.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements the input sequence split into chunks of size <paramref name="size" />.</returns>
    public static IEnumerable<TSource[]> Chunk<TSource>(this IHashSet<TSource> source,int size) {
        return ChunkIterator(source,size);
    }

    /// <summary>Concatenates two sequences.</summary>
    /// <param name="first">The first sequence to concatenate.</param>
    /// <param name="second">The sequence to concatenate to the first sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the concatenated elements of the two input sequences.</returns>
    public static IEnumerable<TSource> Concat<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second) {
        return ConcatIterator(first,second);
    }

    /// <summary>Determines whether a sequence contains a specified element by using the default equality comparer.</summary>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="value">The value to locate in the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains an element that has the specified value; otherwise, <see langword="false" />.</returns>
    public static bool Contains<TSource>(this IHashSet<TSource> source,TSource value) {
        return source.Contains(value);
    }

    /// <summary>Determines whether a sequence contains a specified element by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="value">The value to locate in the sequence.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains an element that has the specified value; otherwise, <see langword="false" />.</returns>
    public static bool Contains<TSource>(this IHashSet<TSource> source,TSource value,IEqualityComparer<TSource>? comparer) {
        if(comparer==null) return source.Contains(value);
        foreach(var item in source) {
            if(comparer.Equals(item,value)) return true;
        }
        return false;
    }

    /// <summary>Returns the number of elements in a sequence.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements in <paramref name="source" /> is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The number of elements in the input sequence.</returns>
    public static int Count<TSource>(this IHashSet<TSource> source) {
        return source.Count;
    }

    /// <summary>Returns a number that represents how many elements in the specified sequence satisfy a condition.</summary>
    /// <param name="source">A sequence that contains elements to be tested and counted.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements in <paramref name="source" /> is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>A number that represents how many elements in the sequence satisfy the condition in the predicate function.</returns>
    public static int Count<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        int count = 0;
        foreach(var item in source) {
            if(predicate(item)) checked { count++; }
        }
        return count;
    }

    /// <summary>Returns the count of elements in the source sequence grouped by key.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="keyComparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <returns>An enumerable containing the frequencies of each key occurrence in <paramref name="source" />.</returns>
    public static IEnumerable<KeyValuePair<TKey,int>> CountBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? keyComparer = null)        where TKey : notnull {
        return CountByIterator(source,keySelector,keyComparer);
    }

    /// <summary>Returns the elements of the specified sequence or the type parameter's default value in a singleton collection if the sequence is empty.</summary>
    /// <param name="source">The sequence to return a default value for if it is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> object that contains the default value for the <typeparamref name="TSource" /> type if <paramref name="source" /> is empty; otherwise, <paramref name="source" />.</returns>
    public static IEnumerable<TSource?> DefaultIfEmpty<TSource>(this IHashSet<TSource> source) {
        return DefaultIfEmptyIterator(source);
    }

    /// <summary>Returns the elements of the specified sequence or the specified value in a singleton collection if the sequence is empty.</summary>
    /// <param name="source">The sequence to return the specified value for if it is empty.</param>
    /// <param name="defaultValue">The value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains <paramref name="defaultValue" /> if <paramref name="source" /> is empty; otherwise, <paramref name="source" />.</returns>
    public static IEnumerable<TSource> DefaultIfEmpty<TSource>(this IHashSet<TSource> source,TSource defaultValue) {
        return DefaultIfEmptyIterator(source,defaultValue);
    }

    /// <summary>Returns distinct elements from a sequence according to a specified key selector function.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return DistinctByIterator(source,keySelector,null);
    }

    /// <summary>Returns distinct elements from a sequence according to a specified key selector function and using a specified comparer to compare keys.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        return DistinctByIterator(source,keySelector,comparer);
    }

    /// <summary>Returns distinct elements from a sequence by using the default equality comparer to compare values.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> Distinct<TSource>(this IHashSet<TSource> source) {
        return DistinctIterator(source,null);
    }

    /// <summary>Returns distinct elements from a sequence by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> Distinct<TSource>(this IHashSet<TSource> source,IEqualityComparer<TSource>? comparer) {
        return DistinctIterator(source,comparer);
    }

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The index of the element to retrieve, which is either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" /> if <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence; otherwise, the element at the specified position in the <paramref name="source" /> sequence.</returns>
    public static TSource? ElementAtOrDefault<TSource>(this IHashSet<TSource> source,Index index) {
        return Enumerable.ElementAtOrDefault(source,index);
    }

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the index is outside the bounds of the source sequence; otherwise, the element at the specified position in the source sequence.</returns>
    public static TSource? ElementAtOrDefault<TSource>(this IHashSet<TSource> source,int index) {
        return Enumerable.ElementAtOrDefault(source,index);
    }

    /// <summary>Returns the element at a specified index in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The index of the element to retrieve, which is either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence.</exception>
    /// <returns>The element at the specified position in the <paramref name="source" /> sequence.</returns>
    public static TSource ElementAt<TSource>(this IHashSet<TSource> source,Index index) {
        return Enumerable.ElementAt(source,index);
    }

    /// <summary>Returns the element at a specified index in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="index" /> is less than 0 or greater than or equal to the number of elements in <paramref name="source" />.</exception>
    /// <returns>The element at the specified position in the source sequence.</returns>
    public static TSource ElementAt<TSource>(this IHashSet<TSource> source,int index) {
        return Enumerable.ElementAt(source,index);
    }

    /// <summary>Produces the set difference of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> ExceptBy<TSource, TKey>(this IHashSet<TSource> first,IHashSet<TKey> second,Func<TSource,TKey> keySelector) {
        return ExceptByIterator(first,second,keySelector,null);
    }

    /// <summary>Produces the set difference of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> ExceptBy<TSource, TKey>(this IHashSet<TSource> first,IHashSet<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        return ExceptByIterator(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set difference of two sequences by using the default equality comparer to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> Except<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second) {
        return ExceptIterator(first,second,null);
    }

    /// <summary>Produces the set difference of two sequences by using the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> Except<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        return ExceptIterator(first,second,comparer);
    }

    /// <summary>Returns the first element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if <paramref name="source" /> is empty; otherwise, the first element in <paramref name="source" />.</returns>
    public static TSource? FirstOrDefault<TSource>(this IHashSet<TSource> source) {
        return Enumerable.FirstOrDefault(source);
    }

    /// <summary>Returns the first element of a sequence, or a specified default value if the sequence contains no elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if <paramref name="source" /> is empty; otherwise, the first element in <paramref name="source" />.</returns>
    public static TSource FirstOrDefault<TSource>(this IHashSet<TSource> source,TSource defaultValue) {
        return Enumerable.FirstOrDefault(source,defaultValue);
    }

    /// <summary>Returns the first element of the sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if <paramref name="source" /> is empty or if no element passes the test specified by <paramref name="predicate" />; otherwise, the first element in <paramref name="source" /> that passes the test specified by <paramref name="predicate" />.</returns>
    public static TSource? FirstOrDefault<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return Enumerable.FirstOrDefault(source,predicate);
    }

    /// <summary>Returns the first element of the sequence that satisfies a condition, or a specified default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if <paramref name="source" /> is empty or if no element passes the test specified by <paramref name="predicate" />; otherwise, the first element in <paramref name="source" /> that passes the test specified by <paramref name="predicate" />.</returns>
    public static TSource FirstOrDefault<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate,TSource defaultValue) {
        return Enumerable.FirstOrDefault(source,predicate,defaultValue);
    }

    /// <summary>Returns the first element of a sequence.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The source sequence is empty.</exception>
    /// <returns>The first element in the specified sequence.</returns>
    public static TSource First<TSource>(this IHashSet<TSource> source) {
        return Enumerable.First(source);
    }

    /// <summary>Returns the first element in a sequence that satisfies a specified condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">No element satisfies the condition in <paramref name="predicate" />.
    ///
    ///  -or-
    ///
    ///  The source sequence is empty.</exception>
    /// <returns>The first element in the sequence that passes the test in the specified predicate function.</returns>
    public static TSource First<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return Enumerable.First(source,predicate);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TSource&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TSource))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a sequence of objects and a key.</returns>
    public static IEnumerable<IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return GroupByIterator(source,keySelector,static x => x,null);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and compares the keys by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TSource&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TSource))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a collection of objects and a key.</returns>
    public static IEnumerable<IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        return GroupByIterator(source,keySelector,static x => x,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and projects the elements for each group by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in the <see cref="T:System.Linq.IGrouping`2" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the <see cref="T:System.Linq.IGrouping`2" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TElement&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TElement))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a collection of objects of type <typeparamref name="TElement" /> and a key.</returns>
    public static IEnumerable<IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector) {
        return GroupByIterator(source,keySelector,elementSelector,null);
    }

    /// <summary>Groups the elements of a sequence according to a key selector function. The keys are compared by using a comparer and each group's elements are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.IGrouping`2" />.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the <see cref="T:System.Linq.IGrouping`2" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TElement&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TElement))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a collection of objects of type <typeparamref name="TElement" /> and a key.</returns>
    public static IEnumerable<IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        return GroupByIterator(source,keySelector,elementSelector,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector) {
        return GroupByIterator(source,keySelector,resultSelector,null);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key. The keys are compared by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        return GroupByIterator(source,keySelector,resultSelector,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key. The elements of each group are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.IGrouping`2" />.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in each <see cref="T:System.Linq.IGrouping`2" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector) {
        return GroupByIterator(source,keySelector,elementSelector,resultSelector,null);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key. Key values are compared by using a specified comparer, and the elements of each group are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.IGrouping`2" />.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in each <see cref="T:System.Linq.IGrouping`2" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        return GroupByIterator(source,keySelector,elementSelector,resultSelector,comparer);
    }

    /// <summary>Correlates the elements of two sequences based on equality of keys and groups the results. The default equality comparer is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from an element from the first sequence and a collection of matching elements from the second sequence.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements of type <typeparamref name="TResult" /> that are obtained by performing a grouped join on two sequences.</returns>
    public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector) {
        return GroupJoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
    }

    /// <summary>Correlates the elements of two sequences based on key equality and groups the results. A specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from an element from the first sequence and a collection of matching elements from the second sequence.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to hash and compare keys.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements of type <typeparamref name="TResult" /> that are obtained by performing a grouped join on two sequences.</returns>
    public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        return GroupJoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns an enumerable that incorporates the element's index into a tuple.</summary>
    /// <param name="source">The source enumerable providing the elements.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An enumerable that incorporates each element index into a tuple.</returns>
    public static IEnumerable<(int Index,TSource Item)> Index<TSource>(this IHashSet<TSource> source) {
        return IndexIterator(source);
    }

    /// <summary>Produces the set intersection of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> IntersectBy<TSource, TKey>(this IHashSet<TSource> first,IHashSet<TKey> second,Func<TSource,TKey> keySelector) {
        return IntersectByIterator(first,second,keySelector,null);
    }

    /// <summary>Produces the set intersection of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> IntersectBy<TSource, TKey>(this IHashSet<TSource> first,IHashSet<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        return IntersectByIterator(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set intersection of two sequences by using the default equality comparer to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> Intersect<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second) {
        return IntersectIterator(first,second,null);
    }

    /// <summary>Produces the set intersection of two sequences by using the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> Intersect<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        return IntersectIterator(first,second,comparer);
    }

    /// <summary>Correlates the elements of two sequences based on matching keys. The default equality comparer is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from two matching elements.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing an inner join on two sequences.</returns>
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector) {
        return JoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
    }

    /// <summary>Correlates the elements of two sequences based on matching keys. A specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from two matching elements.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to hash and compare keys.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing an inner join on two sequences.</returns>
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        return JoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns the last element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the source sequence is empty; otherwise, the last element in the <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static TSource? LastOrDefault<TSource>(this IHashSet<TSource> source) {
        return Enumerable.LastOrDefault(source);
    }

    /// <summary>Returns the last element of a sequence, or a specified default value if the sequence contains no elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if the source sequence is empty; otherwise, the last element in the <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static TSource LastOrDefault<TSource>(this IHashSet<TSource> source,TSource defaultValue) {
        return Enumerable.LastOrDefault(source,defaultValue);
    }

    /// <summary>Returns the last element of a sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the sequence is empty or if no elements pass the test in the predicate function; otherwise, the last element that passes the test in the predicate function.</returns>
    public static TSource? LastOrDefault<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return Enumerable.LastOrDefault(source,predicate);
    }

    /// <summary>Returns the last element of a sequence that satisfies a condition, or a specified default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if the sequence is empty or if no elements pass the test in the predicate function; otherwise, the last element that passes the test in the predicate function.</returns>
    public static TSource LastOrDefault<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate,TSource defaultValue) {
        return Enumerable.LastOrDefault(source,predicate,defaultValue);
    }

    /// <summary>Returns the last element of a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The source sequence is empty.</exception>
    /// <returns>The value at the last position in the source sequence.</returns>
    public static TSource Last<TSource>(this IHashSet<TSource> source) {
        return Enumerable.Last(source);
    }

    /// <summary>Returns the last element of a sequence that satisfies a specified condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">No element satisfies the condition in <paramref name="predicate" />.
    ///
    ///  -or-
    ///
    ///  The source sequence is empty.</exception>
    /// <returns>The last element in the sequence that passes the test in the specified predicate function.</returns>
    public static TSource Last<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return Enumerable.Last(source,predicate);
    }

    /// <summary>Correlates the elements of two sequences based on matching keys. The default equality comparer is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from two matching elements.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a left outer join on two sequences.</returns>
    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector) {
        return LeftJoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
    }

    /// <summary>Correlates the elements of two sequences based on matching keys. A specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from two matching elements.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to hash and compare keys.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a left outer join on two sequences.</returns>
    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        return LeftJoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns an <see cref="T:System.Int64" /> that represents the total number of elements in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to be counted.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements exceeds <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The number of elements in the source sequence.</returns>
    public static long LongCount<TSource>(this IHashSet<TSource> source) {
        return (long)source.Count;
    }

    /// <summary>Returns an <see cref="T:System.Int64" /> that represents how many elements in a sequence satisfy a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to be counted.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of matching elements exceeds <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>A number that represents how many elements in the sequence satisfy the condition in the predicate function.</returns>
    public static long LongCount<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        long count = 0;
        foreach(var item in source) {
            if(predicate(item)) checked { count++; }
        }
        return count;
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to determine the maximum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static decimal Max(this IHashSet<decimal> source) {
        return Extremum(source,Comparer<decimal>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to determine the maximum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static double Max(this IHashSet<double> source) {
        return Extremum(source,Comparer<double>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to determine the maximum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static int Max(this IHashSet<int> source) {
        return Extremum(source,Comparer<int>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to determine the maximum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static long Max(this IHashSet<long> source) {
        return Extremum(source,Comparer<long>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to determine the maximum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static float Max(this IHashSet<float> source) {
        return Extremum(source,Comparer<float>.Default,true);
    }

    /// <summary>Returns the maximum value in a generic sequence according to a specified key selector function.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the maximum key in the sequence.</returns>
    public static TSource? MaxBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return ExtremumBy(source,keySelector,null,true);
    }

    /// <summary>Returns the maximum value in a generic sequence according to a specified key selector function and key comparer.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the maximum key in the sequence.</returns>
    public static TSource? MaxBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return ExtremumBy(source,keySelector,comparer,true);
    }

    /// <summary>Returns the maximum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TSource? Max<TSource>(this IHashSet<TSource> source) {
        return GenericExtremum(source,null,true);
    }

    /// <summary>Returns the maximum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TSource? Max<TSource>(this IHashSet<TSource> source,IComparer<TSource>? comparer) {
        return GenericExtremum(source,comparer,true);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static decimal Max<TSource>(this IHashSet<TSource> source,Func<TSource,decimal> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static double Max<TSource>(this IHashSet<TSource> source,Func<TSource,double> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static int Max<TSource>(this IHashSet<TSource> source,Func<TSource,int> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static long Max<TSource>(this IHashSet<TSource> source,Func<TSource,long> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Decimal&gt;</c> in C# or <c>Nullable(Of Decimal)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static decimal? Max<TSource>(this IHashSet<TSource> source,Func<TSource,decimal?> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Double&gt;</c> in C# or <c>Nullable(Of Double)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static double? Max<TSource>(this IHashSet<TSource> source,Func<TSource,double?> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Int32&gt;</c> in C# or <c>Nullable(Of Int32)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static int? Max<TSource>(this IHashSet<TSource> source,Func<TSource,int?> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the maximum value in the sequence.</returns>
    public static long? Max<TSource>(this IHashSet<TSource> source,Func<TSource,long?> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the maximum value in the sequence.</returns>
    public static float? Max<TSource>(this IHashSet<TSource> source,Func<TSource,float?> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static float Max<TSource>(this IHashSet<TSource> source,Func<TSource,float> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a generic sequence and returns the maximum resulting value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TResult? Max<TSource, TResult>(this IHashSet<TSource> source,Func<TSource,TResult> selector) {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to determine the minimum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static decimal Min(this IHashSet<decimal> source) {
        return Extremum(source,Comparer<decimal>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to determine the minimum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static double Min(this IHashSet<double> source) {
        return Extremum(source,Comparer<double>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to determine the minimum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static int Min(this IHashSet<int> source) {
        return Extremum(source,Comparer<int>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to determine the minimum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static long Min(this IHashSet<long> source) {
        return Extremum(source,Comparer<long>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to determine the minimum value of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static float Min(this IHashSet<float> source) {
        return Extremum(source,Comparer<float>.Default,false);
    }

    /// <summary>Returns the minimum value in a generic sequence according to a specified key selector function.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the minimum key in the sequence.</returns>
    public static TSource? MinBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return ExtremumBy(source,keySelector,null,false);
    }

    /// <summary>Returns the minimum value in a generic sequence according to a specified key selector function and key comparer.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the minimum key in the sequence.</returns>
    public static TSource? MinBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return ExtremumBy(source,keySelector,comparer,false);
    }

    /// <summary>Returns the minimum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TSource? Min<TSource>(this IHashSet<TSource> source) {
        return GenericExtremum(source,null,false);
    }

    /// <summary>Returns the minimum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TSource? Min<TSource>(this IHashSet<TSource> source,IComparer<TSource>? comparer) {
        return GenericExtremum(source,comparer,false);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static decimal Min<TSource>(this IHashSet<TSource> source,Func<TSource,decimal> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static double Min<TSource>(this IHashSet<TSource> source,Func<TSource,double> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static int Min<TSource>(this IHashSet<TSource> source,Func<TSource,int> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static long Min<TSource>(this IHashSet<TSource> source,Func<TSource,long> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static decimal? Min<TSource>(this IHashSet<TSource> source,Func<TSource,decimal?> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static double? Min<TSource>(this IHashSet<TSource> source,Func<TSource,double?> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static int? Min<TSource>(this IHashSet<TSource> source,Func<TSource,int?> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static long? Min<TSource>(this IHashSet<TSource> source,Func<TSource,long?> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static float? Min<TSource>(this IHashSet<TSource> source,Func<TSource,float?> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static float Min<TSource>(this IHashSet<TSource> source,Func<TSource,float> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a generic sequence and returns the minimum resulting value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TResult? Min<TSource, TResult>(this IHashSet<TSource> source,Func<TSource,TResult> selector) {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Sorts the elements of a sequence in descending order according to a key.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return CreateOrdered(source,keySelector,null,true);
    }

    /// <summary>Sorts the elements of a sequence in descending order by using a specified comparer.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return CreateOrdered(source,keySelector,comparer,true);
    }

    /// <summary>Sorts the elements of a sequence in ascending order according to a key.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return CreateOrdered(source,keySelector,null,false);
    }

    /// <summary>Sorts the elements of a sequence in ascending order by using a specified comparer.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return CreateOrdered(source,keySelector,comparer,false);
    }

    /// <summary>Sorts the elements of a sequence in descending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static IOrderedEnumerable<T> OrderDescending<T>(this IHashSet<T> source) {
        return CreateOrdered(source,static x => x,null,true);
    }

    /// <summary>Sorts the elements of a sequence in descending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static IOrderedEnumerable<T> OrderDescending<T>(this IHashSet<T> source,IComparer<T>? comparer) {
        return CreateOrdered(source,static x => x,comparer,true);
    }

    /// <summary>Sorts the elements of a sequence in ascending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static IOrderedEnumerable<T> Order<T>(this IHashSet<T> source) {
        return CreateOrdered(source,static x => x,null,false);
    }

    /// <summary>Sorts the elements of a sequence in ascending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static IOrderedEnumerable<T> Order<T>(this IHashSet<T> source,IComparer<T>? comparer) {
        return CreateOrdered(source,static x => x,comparer,false);
    }

    /// <summary>Adds a value to the beginning of the sequence.</summary>
    /// <param name="source">A sequence of values.</param>
    /// <param name="element">The value to prepend to <paramref name="source" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new sequence that begins with <paramref name="element" />.</returns>
    public static IEnumerable<TSource> Prepend<TSource>(this IHashSet<TSource> source,TSource element) {
        return PrependIterator(source,element);
    }

    /// <summary>Inverts the order of the elements in a sequence.</summary>
    /// <param name="source">A sequence of values to reverse.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A sequence whose elements correspond to those of the input sequence in reverse order.</returns>
    public static IEnumerable<TSource> Reverse<TSource>(this IHashSet<TSource> source) {
        return ReverseIterator(source);
    }

    /// <summary>Correlates the elements of two sequences based on matching keys. The default equality comparer is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from two matching elements.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a right outer join on two sequences.</returns>
    public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector) {
        return RightJoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
    }

    /// <summary>Correlates the elements of two sequences based on matching keys. A specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> is used to compare keys.</summary>
    /// <param name="outer">The first sequence to join.</param>
    /// <param name="inner">The sequence to join to the first sequence.</param>
    /// <param name="outerKeySelector">A function to extract the join key from each element of the first sequence.</param>
    /// <param name="innerKeySelector">A function to extract the join key from each element of the second sequence.</param>
    /// <param name="resultSelector">A function to create a result element from two matching elements.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to hash and compare keys.</param>
    /// <typeparam name="TOuter">The type of the elements of the first sequence.</typeparam>
    /// <typeparam name="TInner">The type of the elements of the second sequence.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by the key selector functions.</typeparam>
    /// <typeparam name="TResult">The type of the result elements.</typeparam>
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a right outer join on two sequences.</returns>
    public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        return RightJoinIterator(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" /> and flattens the resulting sequences into one sequence.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by <paramref name="selector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function on each element of the input sequence.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IHashSet<TSource> source,Func<TSource,IEnumerable<TResult>> selector) {
        return SelectManyIterator(source,selector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, and flattens the resulting sequences into one sequence. The index of each source element is used in the projected form of that element.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by <paramref name="selector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function on each element of an input sequence.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IHashSet<TSource> source,Func<TSource,int,IEnumerable<TResult>> selector) {
        return SelectManyIterator(source,selector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, flattens the resulting sequences into one sequence, and invokes a result selector function on each element therein.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="collectionSelector">A transform function to apply to each element of the input sequence.</param>
    /// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="collectionSelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the resulting sequence.</typeparam>
    ///   <paramref name="source" /> or <paramref name="collectionSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function <paramref name="collectionSelector" /> on each element of <paramref name="source" /> and then mapping each of those sequence elements and their corresponding source element to a result element.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IHashSet<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        return SelectManyIterator(source,collectionSelector,resultSelector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, flattens the resulting sequences into one sequence, and invokes a result selector function on each element therein. The index of each source element is used in the intermediate projected form of that element.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="collectionSelector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="collectionSelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the resulting sequence.</typeparam>
    ///   <paramref name="source" /> or <paramref name="collectionSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function <paramref name="collectionSelector" /> on each element of <paramref name="source" /> and then mapping each of those sequence elements and their corresponding source element to a result element.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IHashSet<TSource> source,Func<TSource,int,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        return SelectManyIterator(source,collectionSelector,resultSelector);
    }

    /// <summary>Projects each element of a sequence into a new form by incorporating the element's index.</summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of <paramref name="source" />.</returns>
    public static IEnumerable<TResult> Select<TSource, TResult>(this IHashSet<TSource> source,Func<TSource,int,TResult> selector) {
        return SelectIterator(source,selector);
    }

    /// <summary>Projects each element of a sequence into a new form.</summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of <paramref name="source" />.</returns>
    public static IEnumerable<TResult> Select<TSource, TResult>(this IHashSet<TSource> source,Func<TSource,TResult> selector) {
        return SelectIterator(source,selector);
    }

    /// <summary>Determines whether two sequences are equal by comparing the elements by using the default equality comparer for their type.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to <paramref name="second" />.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to the first sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the two source sequences are of equal length and their corresponding elements are equal according to the default equality comparer for their type; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second) {
        return Enumerable.SequenceEqual(first,second);
    }

    /// <summary>Determines whether two sequences are equal by comparing their elements by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to <paramref name="second" />.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to the first sequence.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to use to compare elements.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the two source sequences are of equal length and their corresponding elements compare equal according to <paramref name="comparer" />; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        return Enumerable.SequenceEqual(first,second,comparer);
    }

    /// <summary>Shuffles the order of the elements of a sequence.</summary>
    /// <param name="source">A sequence of values to shuffle.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>A sequence whose elements correspond to those of the input sequence in randomized order.</returns>
    public static IEnumerable<TSource> Shuffle<TSource>(this IHashSet<TSource> source) {
        return ShuffleIterator(source);
    }

    /// <summary>Returns the only element of a sequence, or a default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.</exception>
    /// <returns>The single element of the input sequence, or <see langword="default" />(<typeparamref name="TSource" />) if the sequence contains no elements.</returns>
    public static TSource? SingleOrDefault<TSource>(this IHashSet<TSource> source) {
        return Enumerable.SingleOrDefault(source);
    }

    /// <summary>Returns the only element of a sequence, or a specified default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.</exception>
    /// <returns>The single element of the input sequence, or <paramref name="defaultValue" /> if the sequence contains no elements.</returns>
    public static TSource SingleOrDefault<TSource>(this IHashSet<TSource> source,TSource defaultValue) {
        return Enumerable.SingleOrDefault(source,defaultValue);
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition or a default value if no such element exists; this method throws an exception if more than one element satisfies the condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">More than one element satisfies the condition in <paramref name="predicate" />.</exception>
    /// <returns>The single element of the input sequence that satisfies the condition, or <see langword="default" />(<typeparamref name="TSource" />) if no such element is found.</returns>
    public static TSource? SingleOrDefault<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return Enumerable.SingleOrDefault(source,predicate);
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition, or a specified default value if no such element exists; this method throws an exception if more than one element satisfies the condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">More than one element satisfies the condition in <paramref name="predicate" />.</exception>
    /// <returns>The single element of the input sequence that satisfies the condition, or <paramref name="defaultValue" /> if no such element is found.</returns>
    public static TSource SingleOrDefault<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate,TSource defaultValue) {
        return Enumerable.SingleOrDefault(source,predicate,defaultValue);
    }

    /// <summary>Returns the only element of a sequence, and throws an exception if there is not exactly one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.
    ///
    ///  -or-
    ///
    ///  The input sequence is empty.</exception>
    /// <returns>The single element of the input sequence.</returns>
    public static TSource Single<TSource>(this IHashSet<TSource> source) {
        return Enumerable.Single(source);
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition, and throws an exception if more than one such element exists.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">No element satisfies the condition in <paramref name="predicate" />.
    ///
    ///  -or-
    ///
    ///  More than one element satisfies the condition in <paramref name="predicate" />.
    ///
    ///  -or-
    ///
    ///  The source sequence is empty.</exception>
    /// <returns>The single element of the input sequence that satisfies a condition.</returns>
    public static TSource Single<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return Enumerable.Single(source,predicate);
    }

    /// <summary>Returns a new enumerable collection that contains the elements from <paramref name="source" /> with the last <paramref name="count" /> elements of the source collection omitted.</summary>
    /// <param name="source">An enumerable collection instance.</param>
    /// <param name="count">The number of elements to omit from the end of the collection.</param>
    /// <typeparam name="TSource">The type of the elements in the enumerable collection.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new enumerable collection that contains the elements from <paramref name="source" /> minus <paramref name="count" /> elements from the end of the collection.</returns>
    public static IEnumerable<TSource> SkipLast<TSource>(this IHashSet<TSource> source,int count) {
        return SkipLastIterator(source,count);
    }

    /// <summary>Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by <paramref name="predicate" />.</returns>
    public static IEnumerable<TSource> SkipWhile<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return SkipWhileIterator(source,predicate);
    }

    /// <summary>Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements. The element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by <paramref name="predicate" />.</returns>
    public static IEnumerable<TSource> SkipWhile<TSource>(this IHashSet<TSource> source,Func<TSource,int,bool> predicate) {
        return SkipWhileIterator(source,predicate);
    }

    /// <summary>Bypasses a specified number of elements in a sequence and then returns the remaining elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="count">The number of elements to skip before returning the remaining elements.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements that occur after the specified index in the input sequence.</returns>
    public static IEnumerable<TSource> Skip<TSource>(this IHashSet<TSource> source,int count) {
        return SkipIterator(source,count);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to calculate the sum of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static decimal Sum(this IHashSet<decimal> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to calculate the sum of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static double Sum(this IHashSet<double> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to calculate the sum of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static int Sum(this IHashSet<int> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to calculate the sum of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static long Sum(this IHashSet<long> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to calculate the sum of.</param>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static float Sum(this IHashSet<float> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static decimal Sum<TSource>(this IHashSet<TSource> source,Func<TSource,decimal> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static double Sum<TSource>(this IHashSet<TSource> source,Func<TSource,double> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static int Sum<TSource>(this IHashSet<TSource> source,Func<TSource,int> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static long Sum<TSource>(this IHashSet<TSource> source,Func<TSource,long> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static decimal? Sum<TSource>(this IHashSet<TSource> source,Func<TSource,decimal?> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static double? Sum<TSource>(this IHashSet<TSource> source,Func<TSource,double?> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static int? Sum<TSource>(this IHashSet<TSource> source,Func<TSource,int?> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static long? Sum<TSource>(this IHashSet<TSource> source,Func<TSource,long?> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static float? Sum<TSource>(this IHashSet<TSource> source,Func<TSource,float?> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static float Sum<TSource>(this IHashSet<TSource> source,Func<TSource,float> selector) {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Returns a new enumerable collection that contains the last <paramref name="count" /> elements from <paramref name="source" />.</summary>
    /// <param name="source">An enumerable collection instance.</param>
    /// <param name="count">The number of elements to take from the end of the collection.</param>
    /// <typeparam name="TSource">The type of the elements in the enumerable collection.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new enumerable collection that contains the last <paramref name="count" /> elements from <paramref name="source" />.</returns>
    public static IEnumerable<TSource> TakeLast<TSource>(this IHashSet<TSource> source,int count) {
        return TakeLastIterator(source,count);
    }

    /// <summary>Returns elements from a sequence as long as a specified condition is true.</summary>
    /// <param name="source">A sequence to return elements from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence that occur before the element at which the test no longer passes.</returns>
    public static IEnumerable<TSource> TakeWhile<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return TakeWhileIterator(source,predicate);
    }

    /// <summary>Returns elements from a sequence as long as a specified condition is true. The element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that occur before the element at which the test no longer passes.</returns>
    public static IEnumerable<TSource> TakeWhile<TSource>(this IHashSet<TSource> source,Func<TSource,int,bool> predicate) {
        return TakeWhileIterator(source,predicate);
    }

    /// <summary>Returns a specified number of contiguous elements from the start of a sequence.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="count">The number of elements to return.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the specified number of elements from the start of the input sequence.</returns>
    public static IEnumerable<TSource> Take<TSource>(this IHashSet<TSource> source,int count) {
        return TakeIterator(source,count);
    }

    /// <summary>Returns a specified range of contiguous elements from a sequence.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="range">The range of elements to return, which has start and end indexes either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the specified range of elements from the <paramref name="source" /> sequence.</returns>
    public static IEnumerable<TSource> Take<TSource>(this IHashSet<TSource> source,Range range) {
        return TakeIterator(source,range);
    }

    /// <summary>Creates an array from a <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create an array from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An array that contains the elements from the input sequence.</returns>
    public static TSource[] ToArray<TSource>(this IHashSet<TSource> source) {
        return Enumerable.ToArray(source);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static Dictionary<TKey,TSource> ToDictionary<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector)        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function and key comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static Dictionary<TKey,TSource> ToDictionary<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector,comparer);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to specified key selector and element selector functions.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector)        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector,elementSelector);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function, a comparer, and an element selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer)        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector,elementSelector,comparer);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.HashSet`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.HashSet`1" /> from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.HashSet`1" /> that contains values of type <typeparamref name="TSource" /> selected from the input sequence.</returns>
    public static global::System.Collections.Generic.HashSet<TSource> ToHashSet<TSource>(this IHashSet<TSource> source) {
        return Enumerable.ToHashSet(source);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.HashSet`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> using the <paramref name="comparer" /> to compare keys.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.HashSet`1" /> from.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.HashSet`1" /> that contains values of type <typeparamref name="TSource" /> selected from the input sequence.</returns>
    public static global::System.Collections.Generic.HashSet<TSource> ToHashSet<TSource>(this IHashSet<TSource> source,IEqualityComparer<TSource>? comparer) {
        var set=new global::System.Collections.Generic.HashSet<TSource>(comparer);
        foreach(var item in (IEnumerable<TSource>)source) {
            set.Add(item);
        }
        return set;
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.List`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.List`1" /> from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.List`1" /> that contains elements from the input sequence.</returns>
    public static global::System.Collections.Generic.List<TSource> ToList<TSource>(this IHashSet<TSource> source) {
        var list = new global::System.Collections.Generic.List<TSource>();
        foreach(var item in source) {
            list.Add(item);
        }
        return list;
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static ILookup<TKey,TSource> ToLookup<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector) {
        return Enumerable.ToLookup(source,keySelector);
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function and key comparer.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static ILookup<TKey,TSource> ToLookup<TSource, TKey>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        IEqualityComparer<TKey>? comparer1=comparer;
        comparer1??=EqualityComparer<TKey>.Default;
        var lookup=new LookupImpl<TKey,TSource>(comparer1);
        foreach(var item in (IEnumerable<TSource>)source) {
            lookup.Add(keySelector(item),item);
        }
        return lookup;
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to specified key selector and element selector functions.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector) {
        return Enumerable.ToLookup(source,keySelector,elementSelector);
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function, a comparer and an element selector function.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement>(this IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        var lookup=new LookupImpl<TKey,TElement>(comparer);
        foreach(var item in (IEnumerable<TSource>)source) {
            lookup.Add(keySelector(item),elementSelector(item));
        }
        return lookup;
    }

    /// <summary>Attempts to determine the number of elements in a sequence without forcing an enumeration.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <param name="count">When this method returns, contains the number of elements in <paramref name="source" />, or 0 if the count couldn't be determined without enumeration.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>
    ///   <see langword="true" /> if the count of <paramref name="source" /> can be determined without enumeration; otherwise, <see langword="false" />.</returns>
    public static bool TryGetNonEnumeratedCount<TSource>(this IHashSet<TSource> source,out int count) {
        return Enumerable.TryGetNonEnumeratedCount(source,out count);
    }

    /// <summary>Produces the set union of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> UnionBy<TSource, TKey>(this IHashSet<TSource> first,IHashSet<TSource> second,Func<TSource,TKey> keySelector) {
        return UnionByIterator(first,second,keySelector,null);
    }

    /// <summary>Produces the set union of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> UnionBy<TSource, TKey>(this IHashSet<TSource> first,IHashSet<TSource> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        return UnionByIterator(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set union of two sequences by using the default equality comparer.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> Union<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second) {
        return UnionIterator(first,second,null);
    }

    /// <summary>Produces the set union of two sequences by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> Union<TSource>(this IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        return UnionIterator(first,second,comparer);
    }

    /// <summary>Filters a sequence of values based on a predicate.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to filter.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static IEnumerable<TSource> Where<TSource>(this IHashSet<TSource> source,Func<TSource,bool> predicate) {
        return WhereIterator(source,predicate);
    }

    /// <summary>Filters a sequence of values based on a predicate. Each element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to filter.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static IEnumerable<TSource> Where<TSource>(this IHashSet<TSource> source,Func<TSource,int,bool> predicate) {
        return WhereIterator(source,predicate);
    }

    /// <summary>Produces a sequence of tuples with elements from the two specified sequences.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <returns>A sequence of tuples with elements taken from the first and second sequences, in that order.</returns>
    public static IEnumerable<(TFirst First,TSecond Second)> Zip<TFirst, TSecond>(this IHashSet<TFirst> first,IHashSet<TSecond> second) {
        return ZipIterator(first,second);
    }

    /// <summary>Produces a sequence of tuples with elements from the three specified sequences.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <param name="third">The third sequence to merge.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <typeparam name="TThird">The type of the elements of the third input sequence.</typeparam>
    /// <returns>A sequence of tuples with elements taken from the first, second, and third sequences, in that order.</returns>
    public static IEnumerable<(TFirst First,TSecond Second,TThird Third)> Zip<TFirst, TSecond, TThird>(this IHashSet<TFirst> first,IHashSet<TSecond> second,IHashSet<TThird> third) {
        return ZipIterator(first,second,third);
    }

    /// <summary>Applies a specified function to the corresponding elements of two sequences, producing a sequence of the results.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <param name="resultSelector">A function that specifies how to merge the elements from the two sequences.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the result sequence.</typeparam>
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains merged elements of two input sequences.</returns>
    public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(this IHashSet<TFirst> first,IHashSet<TSecond> second,Func<TFirst,TSecond,TResult> resultSelector) {
        return ZipIterator(first,second,resultSelector);
    }
    private static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull {
        var dictionary = new Dictionary<TKey,TAccumulate>(keyComparer);
        foreach(var item in source) {
            var key = keySelector(item);
            if(!dictionary.TryGetValue(key,out var accumulator)) {
                accumulator=seed;
            }
            dictionary[key]=func(accumulator!,item);
        }
        var result = new global::System.Collections.Generic.List<KeyValuePair<TKey,TAccumulate>>();
        foreach(var pair in dictionary) {
            result.Add(pair);
        }
        return result;
    }

    private static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull {
        var dictionary = new Dictionary<TKey,TAccumulate>(keyComparer);
        foreach(var item in source) {
            var key = keySelector(item);
            if(!dictionary.TryGetValue(key,out var accumulator)) {
                accumulator=seedSelector(key);
            }
            dictionary[key]=func(accumulator,item);
        }
        var result = new global::System.Collections.Generic.List<KeyValuePair<TKey,TAccumulate>>();
        foreach(var pair in dictionary) {
            result.Add(pair);
        }
        return result;
    }

    private static IEnumerable<TSource> AppendIterator<TSource>(IHashSet<TSource> source,TSource element) {
        var result = new global::System.Collections.Generic.List<TSource>();
        foreach(var item in source) {
            result.Add(item);
        }
        result.Add(element);
        return result;
    }

    private static IEnumerable<TResult> CastIterator<TResult>(IEnumerable source) {
        var result = new global::System.Collections.Generic.List<TResult>();
        foreach(var item in source) {
            result.Add((TResult)item!);
        }
        return result;
    }

    private static IEnumerable<TSource[]> ChunkIterator<TSource>(IHashSet<TSource> source,int size) {
        var result = new global::System.Collections.Generic.List<TSource[]>();
        var chunk = new TSource[size];
        var index = 0;
        foreach(var item in source) {
            chunk[index++]=item;
            if(index==size) {
                result.Add(chunk);
                chunk=new TSource[size];
                index=0;
            }
        }
        if(index>0) {
            Array.Resize(ref chunk,index);
            result.Add(chunk);
        }
        return result;
    }

    private static IEnumerable<TSource> ConcatIterator<TSource>(IHashSet<TSource> first,IHashSet<TSource> second) {
        var result = new global::System.Collections.Generic.List<TSource>();
        foreach(var item in first) {
            result.Add(item);
        }
        foreach(var item in second) {
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<KeyValuePair<TKey,int>> CountByIterator<TSource, TKey>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull {
        var counts = new Dictionary<TKey,int>(keyComparer);
        foreach(var item in source) {
            var key = keySelector(item);
            counts.TryGetValue(key,out var count);
            checked {
                counts[key]=count+1;
            }
        }
        var result = new global::System.Collections.Generic.List<KeyValuePair<TKey,int>>();
        foreach(var pair in counts) {
            result.Add(pair);
        }
        return result;
    }

    private static IEnumerable<TSource?> DefaultIfEmptyIterator<TSource>(IHashSet<TSource> source) {
        var result = new global::System.Collections.Generic.List<TSource?>();
        foreach(var item in source) {
            result.Add(item);
        }
        if(result.Count==0) {
            result.Add(default);
        }
        return result;
    }

    private static IEnumerable<TSource> DefaultIfEmptyIterator<TSource>(IHashSet<TSource> source,TSource defaultValue) {
        var result = new global::System.Collections.Generic.List<TSource>();
        foreach(var item in source) {
            result.Add(item);
        }
        if(result.Count==0) {
            result.Add(defaultValue);
        }
        return result;
    }

    private static IEnumerable<TSource> DistinctByIterator<TSource, TKey>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var seen = new global::System.Collections.Generic.HashSet<TKey>(comparer);
        foreach(var item in source) {
            if(seen.Add(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> DistinctIterator<TSource>(IHashSet<TSource> source,IEqualityComparer<TSource>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var seen = new global::System.Collections.Generic.HashSet<TSource>(comparer);
        foreach(var item in source) {
            if(seen.Add(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> ExceptByIterator<TSource, TKey>(IHashSet<TSource> first,IHashSet<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var excluded = new global::System.Collections.Generic.HashSet<TKey>(second,comparer);
        foreach(var item in first) {
            if(excluded.Add(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> ExceptIterator<TSource>(IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var excluded = new global::System.Collections.Generic.HashSet<TSource>(second,comparer);
        foreach(var item in first) {
            if(excluded.Add(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<IGrouping<TKey,TElement>> GroupByIterator<TSource, TKey, TElement>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<IGrouping<TKey,TElement>>();
        var lookup = CreateLookup(source,keySelector,elementSelector,comparer);
        foreach(var group in lookup.Groups) {
            result.Add(group);
        }
        return result;
    }

    private static IEnumerable<TResult> GroupByIterator<TSource, TKey, TResult>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var lookup = CreateLookup(source,keySelector,static item => item,comparer);
        foreach(var group in lookup.Groups) {
            result.Add(resultSelector(group.Key,group));
        }
        return result;
    }

    private static IEnumerable<TResult> GroupByIterator<TSource, TKey, TElement, TResult>(IHashSet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var lookup = CreateLookup(source,keySelector,elementSelector,comparer);
        foreach(var group in lookup.Groups) {
            result.Add(resultSelector(group.Key,group));
        }
        return result;
    }

    private static IEnumerable<TResult> GroupJoinIterator<TOuter, TInner, TKey, TResult>(IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var lookup = CreateLookup(inner,innerKeySelector,static item => item,comparer);
        foreach(var outerItem in outer) {
            result.Add(resultSelector(outerItem,lookup[outerKeySelector(outerItem)]));
        }
        return result;
    }

    private static IEnumerable<(int Index,TSource Item)> IndexIterator<TSource>(IHashSet<TSource> source) {
        var result = new global::System.Collections.Generic.List<(int Index,TSource Item)>();
        var index = 0;
        foreach(var item in source) {
            result.Add((index,item));
            checked {
                index++;
            }
        }
        return result;
    }

    private static IEnumerable<TSource> IntersectByIterator<TSource, TKey>(IHashSet<TSource> first,IHashSet<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var set = new global::System.Collections.Generic.HashSet<TKey>(second,comparer);
        foreach(var item in first) {
            if(set.Remove(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> IntersectIterator<TSource>(IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var set = new global::System.Collections.Generic.HashSet<TSource>(second,comparer);
        foreach(var item in first) {
            if(set.Remove(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TResult> JoinIterator<TOuter, TInner, TKey, TResult>(IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var lookup = CreateLookup(inner,innerKeySelector,static item => item,comparer);
        foreach(var outerItem in outer) {
            foreach(var innerItem in lookup[outerKeySelector(outerItem)]) {
                result.Add(resultSelector(outerItem,innerItem));
            }
        }
        return result;
    }

    private static IEnumerable<TResult> LeftJoinIterator<TOuter, TInner, TKey, TResult>(IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var lookup = CreateLookup(inner,innerKeySelector,static item => item,comparer);
        foreach(var outerItem in outer) {
            var hasMatch = false;
            foreach(var innerItem in lookup[outerKeySelector(outerItem)]) {
                hasMatch=true;
                result.Add(resultSelector(outerItem,innerItem));
            }
            if(!hasMatch) {
                result.Add(resultSelector(outerItem,default));
            }
        }
        return result;
    }

    private static IEnumerable<TResult> OfTypeIterator<TResult>(IEnumerable source) {
        var result = new global::System.Collections.Generic.List<TResult>();
        foreach(var item in source) {
            if(item is TResult result2) {
                result.Add(result2);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> PrependIterator<TSource>(IHashSet<TSource> source,TSource element) {
        var result = new global::System.Collections.Generic.List<TSource>();
        result.Add(element);
        foreach(var item in source) {
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<int> RangeIterator(int start,int count) {
        var result = new global::System.Collections.Generic.List<int>();
        for(var index = 0; index<count; index++) {
            result.Add(start+index);
        }
        return result;
    }

    private static IEnumerable<TResult> RepeatIterator<TResult>(TResult element,int count) {
        var result = new global::System.Collections.Generic.List<TResult>();
        for(var index = 0; index<count; index++) {
            result.Add(element);
        }
        return result;
    }

    private static IEnumerable<TSource> ReverseIterator<TSource>(IHashSet<TSource> source) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var list = ToList(source);
        for(var index = list.Count-1; index>=0; index--) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TSource> ReverseIterator<TSource>(TSource[] source) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var list = new global::System.Collections.Generic.List<TSource>(source);
        for(var index = list.Count-1; index>=0; index--) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TResult> RightJoinIterator<TOuter, TInner, TKey, TResult>(IHashSet<TOuter> outer,IHashSet<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var lookup = CreateLookup(outer,outerKeySelector,static item => item,comparer);
        foreach(var innerItem in inner) {
            var hasMatch = false;
            foreach(var outerItem in lookup[innerKeySelector(innerItem)]) {
                hasMatch=true;
                result.Add(resultSelector(outerItem,innerItem));
            }
            if(!hasMatch) {
                result.Add(resultSelector(default,innerItem));
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TResult>(IHashSet<TSource> source,Func<TSource,IEnumerable<TResult>> selector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        foreach(var item in source) {
            foreach(var projected in selector(item)) {
                result.Add(projected);
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TResult>(IHashSet<TSource> source,Func<TSource,int,IEnumerable<TResult>> selector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            foreach(var projected in selector(item,index)) {
                result.Add(projected);
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TCollection, TResult>(IHashSet<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        foreach(var item in source) {
            foreach(var collectionItem in collectionSelector(item)) {
                result.Add(resultSelector(item,collectionItem));
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TCollection, TResult>(IHashSet<TSource> source,Func<TSource,int,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            foreach(var collectionItem in collectionSelector(item,index)) {
                result.Add(resultSelector(item,collectionItem));
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectIterator<TSource, TResult>(IHashSet<TSource> source,Func<TSource,int,TResult> selector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            result.Add(selector(item,index));
        }
        return result;
    }

    private static IEnumerable<TResult> SelectIterator<TSource, TResult>(IHashSet<TSource> source,Func<TSource,TResult> selector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        foreach(var item in source) {
            result.Add(selector(item));
        }
        return result;
    }

    private static IEnumerable<T> SequenceIterator<T>(T start,T endInclusive,T step) where T : INumber<T> {
        var result = new global::System.Collections.Generic.List<T>();
        if(step>T.Zero) {
            for(var current = start; current<=endInclusive; current+=step) {
                result.Add(current);
            }
        }
        else {
            for(var current = start; current>=endInclusive; current+=step) {
                result.Add(current);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> ShuffleIterator<TSource>(IHashSet<TSource> source) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var list = ToList(source);
        for(var index = list.Count-1; index>0; index--) {
            var swapIndex = Random.Shared.Next(index+1);
            (list[index],list[swapIndex])=(list[swapIndex],list[index]);
        }
        foreach(var item in list) {
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<TSource> SkipLastIterator<TSource>(IHashSet<TSource> source,int count) {
        var result = new global::System.Collections.Generic.List<TSource>();
        if(count<=0) {
            foreach(var item in source) {
                result.Add(item);
            }
            return result;
        }
        var list = ToList(source);
        var end = Math.Max(0,list.Count-count);
        for(var index = 0; index<end; index++) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TSource> SkipWhileIterator<TSource>(IHashSet<TSource> source,Func<TSource,bool> predicate) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var yielding = false;
        foreach(var item in source) {
            if(!yielding&&!predicate(item)) {
                yielding=true;
            }
            if(yielding) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> SkipWhileIterator<TSource>(IHashSet<TSource> source,Func<TSource,int,bool> predicate) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var yielding = false;
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            if(!yielding&&!predicate(item,index)) {
                yielding=true;
            }
            if(yielding) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> SkipIterator<TSource>(IHashSet<TSource> source,int count) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var skipped = 0;
        foreach(var item in source) {
            if(skipped<count) {
                skipped++;
                continue;
            }
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<TSource> TakeLastIterator<TSource>(IHashSet<TSource> source,int count) {
        var result = new global::System.Collections.Generic.List<TSource>();
        if(count<=0) {
            return result;
        }
        var list = ToList(source);
        var start = Math.Max(0,list.Count-count);
        for(var index = start; index<list.Count; index++) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TSource> TakeWhileIterator<TSource>(IHashSet<TSource> source,Func<TSource,bool> predicate) {
        var result = new global::System.Collections.Generic.List<TSource>();
        foreach(var item in source) {
            if(!predicate(item)) {
                return result;
            }
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<TSource> TakeWhileIterator<TSource>(IHashSet<TSource> source,Func<TSource,int,bool> predicate) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            if(!predicate(item,index)) {
                return result;
            }
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<TSource> TakeIterator<TSource>(IHashSet<TSource> source,int count) {
        var result = new global::System.Collections.Generic.List<TSource>();
        if(count<=0) {
            return result;
        }
        var taken = 0;
        foreach(var item in source) {
            if(taken++>=count) {
                return result;
            }
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<TSource> TakeIterator<TSource>(IHashSet<TSource> source,Range range) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var list = ToList(source);
        var (offset,length)=range.GetOffsetAndLength(list.Count);
        for(var index = offset; index<offset+length; index++) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TSource> UnionByIterator<TSource, TKey>(IHashSet<TSource> first,IHashSet<TSource> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var seen = new global::System.Collections.Generic.HashSet<TKey>(comparer);
        foreach(var item in first) {
            if(seen.Add(keySelector(item))) {
                result.Add(item);
            }
        }
        foreach(var item in second) {
            if(seen.Add(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> UnionIterator<TSource>(IHashSet<TSource> first,IHashSet<TSource> second,IEqualityComparer<TSource>? comparer) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var seen = new global::System.Collections.Generic.HashSet<TSource>(comparer);
        foreach(var item in first) {
            if(seen.Add(item)) {
                result.Add(item);
            }
        }
        foreach(var item in second) {
            if(seen.Add(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> WhereIterator<TSource>(IHashSet<TSource> source,Func<TSource,bool> predicate) {
        var result = new global::System.Collections.Generic.List<TSource>();
        foreach(var item in source) {
            if(predicate(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> WhereIterator<TSource>(IHashSet<TSource> source,Func<TSource,int,bool> predicate) {
        var result = new global::System.Collections.Generic.List<TSource>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            if(predicate(item,index)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<(TFirst First,TSecond Second)> ZipIterator<TFirst, TSecond>(IHashSet<TFirst> first,IHashSet<TSecond> second) {
        var result = new global::System.Collections.Generic.List<(TFirst First,TSecond Second)>();
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        while(firstEnumerator.MoveNext()&&secondEnumerator.MoveNext()) {
            result.Add((firstEnumerator.Current,secondEnumerator.Current));
        }
        return result;
    }

    private static IEnumerable<(TFirst First,TSecond Second,TThird Third)> ZipIterator<TFirst, TSecond, TThird>(IHashSet<TFirst> first,IHashSet<TSecond> second,IHashSet<TThird> third) {
        var result = new global::System.Collections.Generic.List<(TFirst First,TSecond Second,TThird Third)>();
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        using var thirdEnumerator = third.GetEnumerator();
        while(firstEnumerator.MoveNext()&&secondEnumerator.MoveNext()&&thirdEnumerator.MoveNext()) {
            result.Add((firstEnumerator.Current,secondEnumerator.Current,thirdEnumerator.Current));
        }
        return result;
    }

    private static IEnumerable<TResult> ZipIterator<TFirst, TSecond, TResult>(IHashSet<TFirst> first,IHashSet<TSecond> second,Func<TFirst,TSecond,TResult> resultSelector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        while(firstEnumerator.MoveNext()&&secondEnumerator.MoveNext()) {
            result.Add(resultSelector(firstEnumerator.Current,secondEnumerator.Current));
        }
        return result;
    }
    private static IEnumerable<TResult> Project<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,TResult> selector) {
        var result = new global::System.Collections.Generic.List<TResult>();
        foreach(var item in source) {
            result.Add(selector(item));
        }
        return result;
    }

    private static T Extremum<T>(IEnumerable<T> source,IComparer<T> comparer,bool findMax) {
        var hasValue = false;
        T? result = default;
        foreach(var item in source) {
            if(!hasValue) {
                result=item;
                hasValue=true;
                continue;
            }
            if(IsBetter(comparer.Compare(item,result!),findMax)) {
                result=item;
            }
        }
        return hasValue ? result! : throw NoElements();
    }

    private static T? NullableExtremum<T>(IEnumerable<T?> source,IComparer<T> comparer,bool findMax)
        where T : struct {
        var hasValue = false;
        T result = default;
        foreach(var item in source) {
            if(!item.HasValue) {
                continue;
            }
            var value = item.GetValueOrDefault();
            if(!hasValue) {
                result=value;
                hasValue=true;
                continue;
            }
            if(IsBetter(comparer.Compare(value,result),findMax)) {
                result=value;
            }
        }
        return hasValue ? result : null;
    }

    private static TSource? GenericExtremum<TSource>(IEnumerable<TSource> source,IComparer<TSource>? comparer,bool findMax) {
        comparer??=Comparer<TSource>.Default;
        var hasValue = false;
        TSource? result = default;
        foreach(var item in source) {
            if(item is null) {
                continue;
            }
            if(!hasValue) {
                result=item;
                hasValue=true;
                continue;
            }
            if(IsBetter(comparer.Compare(item,result!),findMax)) {
                result=item;
            }
        }
        if(hasValue) {
            return result;
        }
        if(default(TSource) is null) {
            return default;
        }
        throw NoElements();
    }

    private static TSource? ExtremumBy<TSource, TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer,bool findMax) {
        comparer??=Comparer<TKey>.Default;
        using var enumerator = source.GetEnumerator();
        if(!enumerator.MoveNext()) {
            if(default(TSource) is null) {
                return default;
            }
            throw NoElements();
        }
        var result = enumerator.Current;
        var resultKey = keySelector(result);
        while(enumerator.MoveNext()) {
            var item = enumerator.Current;
            var key = keySelector(item);
            if(IsBetter(comparer.Compare(key,resultKey),findMax)) {
                result=item;
                resultKey=key;
            }
        }
        return result;
    }

    private static bool IsBetter(int comparison,bool findMax) {
        return findMax ? comparison>0 : comparison<0;
    }

    private static IOrderedEnumerable<TSource> CreateOrdered<TSource, TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer,bool descending) {
        return new OrderedEnumerableImpl<TSource>(source).CreateOrderedEnumerable(keySelector,comparer,descending);
    }

    private static LookupImpl<TKey,TElement> CreateLookup<TSource, TKey, TElement>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        var lookup = new LookupImpl<TKey,TElement>(comparer);
        foreach(var item in source) {
            lookup.Add(keySelector(item),elementSelector(item));
        }
        return lookup;
    }

    private static InvalidOperationException NoElements() {
        return new InvalidOperationException("Sequence contains no elements.");
    }

    private static InvalidOperationException NoMatch() {
        return new InvalidOperationException("Sequence contains no matching element.");
    }

    private static InvalidOperationException MoreThanOneElement() {
        return new InvalidOperationException("Sequence contains more than one element.");
    }

    private static InvalidOperationException MoreThanOneMatch() {
        return new InvalidOperationException("Sequence contains more than one matching element.");
    }

    private sealed class GroupingImpl<TKey, TElement>(TKey key):IGrouping<TKey,TElement> {
        private readonly global::System.Collections.Generic.List<TElement> elements =[];

        public TKey Key { get; } = key;

        public void Add(TElement element) {
            this.elements.Add(element);
        }

        public IEnumerator<TElement> GetEnumerator() {
            return this.elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }
    }

    private sealed class LookupImpl<TKey, TElement>(IEqualityComparer<TKey>? comparer):ILookup<TKey,TElement> {
        private readonly IEqualityComparer<TKey> comparer = comparer??EqualityComparer<TKey>.Default;

        public global::System.Collections.Generic.List<GroupingImpl<TKey,TElement>> Groups { get; } = [];

        public int Count => this.Groups.Count;

        public IEnumerable<TElement> this[TKey key] {
            get {
                var group = this.Find(key);
                return group is null ? Array.Empty<TElement>() : group;
            }
        }

        public bool Contains(TKey key) {
            return this.Find(key) is not null;
        }

        public void Add(TKey key,TElement element) {
            var group = this.Find(key);
            if(group is null) {
                group=new GroupingImpl<TKey,TElement>(key);
                this.Groups.Add(group);
            }
            group.Add(element);
        }

        public IEnumerator<IGrouping<TKey,TElement>> GetEnumerator() {
            foreach(var group in this.Groups) {
                yield return group;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        private GroupingImpl<TKey,TElement>? Find(TKey key) {
            foreach(var group in this.Groups) {
                if(this.comparer.Equals(group.Key,key)) {
                    return group;
                }
            }
            return null;
        }
    }

    private sealed class InfiniteSequenceEnumerable<T>(T start,T step):IEnumerable<T>
        where T : IAdditionOperators<T,T,T> {
        private readonly T start = start;
        private readonly T step = step;

        public IEnumerator<T> GetEnumerator() {
            return new InfiniteSequenceEnumerator<T>(this.start,this.step);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }
    }

    private sealed class InfiniteSequenceEnumerator<T>(T start,T step):IEnumerator<T>
        where T : IAdditionOperators<T,T,T> {
        private readonly T start = start;
        private readonly T step = step;
        private bool started;
        private T current = default!;

        public T Current => this.current;

        object IEnumerator.Current => this.Current;

        public bool MoveNext() {
            if(!this.started) {
                this.current=this.start;
                this.started=true;
            }
            else {
                this.current+=this.step;
            }
            return true;
        }

        public void Reset() {
            this.started=false;
            this.current=default!;
        }

        public void Dispose() {
        }
    }

    private sealed class OrderedEnumerableImpl<T>:IOrderedEnumerable<T> {
        private readonly IEnumerable<T> source;
        private readonly global::System.Collections.Generic.List<Comparison<T>> comparisons;

        public OrderedEnumerableImpl(IEnumerable<T> source) {
            this.source=source;
            this.comparisons=[];
        }

        private OrderedEnumerableImpl(IEnumerable<T> source,global::System.Collections.Generic.List<Comparison<T>> comparisons) {
            this.source=source;
            this.comparisons=comparisons;
        }

        public IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T,TKey> keySelector,IComparer<TKey>? comparer,bool descending) {
            comparer??=Comparer<TKey>.Default;
            var next = new global::System.Collections.Generic.List<Comparison<T>>(this.comparisons);
            next.Add((left,right) => {
                var result = comparer.Compare(keySelector(left),keySelector(right));
                return descending ? Negate(result) : result;
            });
            return new OrderedEnumerableImpl<T>(this.source,next);
        }

        public IEnumerator<T> GetEnumerator() {
            var items = new global::System.Collections.Generic.List<OrderedItem<T>>();
            var index = 0;
            foreach(var item in this.source) {
                items.Add(new OrderedItem<T>(item,index));
                checked {
                    index++;
                }
            }
            items.Sort(this.Compare);
            foreach(var item in items) {
                yield return item.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        private int Compare(OrderedItem<T> left,OrderedItem<T> right) {
            foreach(var comparison in this.comparisons) {
                var result = comparison(left.Value,right.Value);
                if(result!=0) {
                    return result;
                }
            }
            return left.Index.CompareTo(right.Index);
        }

        private static int Negate(int value) {
            return value==int.MinValue ? int.MaxValue : -value;
        }
    }

    private readonly struct OrderedItem<T>(T value,int index) {
        public T Value { get; } = value;
        public int Index { get; } = index;
    }
}
