using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Linq;

/// <summary>Provides a set of <see langword="static" /> (<see langword="Shared" /> in Visual Basic) methods for querying objects that implement <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
public static partial class Enumerable {
    /// <summary>Applies an accumulator function over a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="func" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The final accumulator value.</returns>
    public static TSource Aggregate<TSource>(this IEnumerable<TSource> source,Func<TSource,TSource,TSource> func) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(func);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="func" /> is <see langword="null" />.</exception>
    /// <returns>The final accumulator value.</returns>
    public static TAccumulate Aggregate<TSource, TAccumulate>(this IEnumerable<TSource> source,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(func);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="func" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>The transformed final accumulator value.</returns>
    public static TResult Aggregate<TSource, TAccumulate, TResult>(this IEnumerable<TSource> source,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,Func<TAccumulate,TResult> resultSelector) {
        ArgumentNullException.ThrowIfNull(resultSelector);
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
    public static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(func);
        return AggregateByIterator<TSource,TKey,TAccumulate>(source,keySelector,seed,func,keyComparer);
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
    public static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(seedSelector);
        ArgumentNullException.ThrowIfNull(func);
        return AggregateByIterator<TSource,TKey,TAccumulate>(source,keySelector,seedSelector,func,keyComparer);
    }

    /// <summary>Determines whether all elements of a sequence satisfy a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, <see langword="false" />.</returns>
    public static bool All<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        foreach(var item in source) {
            if(!predicate(item)) {
                return false;
            }
        }
        return true;
    }

    /// <summary>Determines whether a sequence contains any elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to check for emptiness.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains any elements; otherwise, <see langword="false" />.</returns>
    public static bool Any<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        using var enumerator = source.GetEnumerator();
        return enumerator.MoveNext();
    }

    /// <summary>Determines whether any element of a sequence satisfies a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence is not empty and at least one of its elements passes the test in the specified predicate; otherwise, <see langword="false" />.</returns>
    public static bool Any<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        foreach(var item in source) {
            if(predicate(item)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>Appends a value to the end of the sequence.</summary>
    /// <param name="source">A sequence of values.</param>
    /// <param name="element">The value to append to <paramref name="source" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new sequence that ends with <paramref name="element" />.</returns>
    public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source,TSource element) {
        ArgumentNullException.ThrowIfNull(source);
        return AppendIterator<TSource>(source,element);
    }

    /// <summary>Returns the input typed as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">The sequence to type as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>The input sequence typed as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static IEnumerable<TSource> AsEnumerable<TSource>(this IEnumerable<TSource> source) {
        return source;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static decimal Average(this IEnumerable<decimal> source) {
        ArgumentNullException.ThrowIfNull(source);
        decimal sum = 0;
        long count = 0;
        foreach(var item in source) {
            checked {
                sum+=item;
                count++;
            }
        }
        if(count==0) {
            throw NoElements();
        }
        return sum/count;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this IEnumerable<double> source) {
        ArgumentNullException.ThrowIfNull(source);
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
        return sum/count;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this IEnumerable<int> source) {
        ArgumentNullException.ThrowIfNull(source);
        long sum = 0;
        long count = 0;
        foreach(var item in source) {
            checked {
                sum+=item;
                count++;
            }
        }
        if(count==0) {
            throw NoElements();
        }
        return (double)sum/count;
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this IEnumerable<long> source) {
        ArgumentNullException.ThrowIfNull(source);
        decimal sum = 0;
        long count = 0;
        foreach(var item in source) {
            checked {
                sum+=item;
                count++;
            }
        }
        if(count==0) {
            throw NoElements();
        }
        return (double)(sum/count);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Decimal" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static decimal? Average(this IEnumerable<decimal?> source) {
        ArgumentNullException.ThrowIfNull(source);
        decimal sum = 0;
        long count = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                checked {
                    sum+=item.GetValueOrDefault();
                    count++;
                }
            }
        }
        return count==0 ? null : sum/count;
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Double" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average(this IEnumerable<double?> source) {
        ArgumentNullException.ThrowIfNull(source);
        double sum = 0;
        long count = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                sum+=item.GetValueOrDefault();
                checked {
                    count++;
                }
            }
        }
        return count==0 ? null : sum/count;
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int32" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average(this IEnumerable<int?> source) {
        ArgumentNullException.ThrowIfNull(source);
        long sum = 0;
        long count = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                checked {
                    sum+=item.GetValueOrDefault();
                    count++;
                }
            }
        }
        return count==0 ? null : (double)sum/count;
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int64" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average(this IEnumerable<long?> source) {
        ArgumentNullException.ThrowIfNull(source);
        decimal sum = 0;
        long count = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                checked {
                    sum+=item.GetValueOrDefault();
                    count++;
                }
            }
        }
        return count==0 ? null : (double)(sum/count);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Single" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static float? Average(this IEnumerable<float?> source) {
        ArgumentNullException.ThrowIfNull(source);
        double sum = 0;
        long count = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                sum+=item.GetValueOrDefault();
                checked {
                    count++;
                }
            }
        }
        return count==0 ? null : (float)(sum/count);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static float Average(this IEnumerable<float> source) {
        ArgumentNullException.ThrowIfNull(source);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static decimal Average<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average<TSource>(this IEnumerable<TSource> source,Func<TSource,double> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average<TSource>(this IEnumerable<TSource> source,Func<TSource,int> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of source.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average<TSource>(this IEnumerable<TSource> source,Func<TSource,long> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static decimal? Average<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this IEnumerable<TSource> source,Func<TSource,double?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this IEnumerable<TSource> source,Func<TSource,int?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this IEnumerable<TSource> source,Func<TSource,long?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static float? Average<TSource>(this IEnumerable<TSource> source,Func<TSource,float?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static float Average<TSource>(this IEnumerable<TSource> source,Func<TSource,float> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Average(Project(source,selector));
    }

    /// <summary>Casts the elements of an <see cref="T:System.Collections.IEnumerable" /> to the specified type.</summary>
    /// <param name="source">The <see cref="T:System.Collections.IEnumerable" /> that contains the elements to be cast to type <typeparamref name="TResult" />.</param>
    /// <typeparam name="TResult">The type to cast the elements of <paramref name="source" /> to.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidCastException">An element in the sequence cannot be cast to type <typeparamref name="TResult" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains each element of the source sequence cast to the specified type.</returns>
    public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source) {
        ArgumentNullException.ThrowIfNull(source);
        return CastIterator<TResult>(source);
    }

    /// <summary>Splits the elements of a sequence into chunks of size at most <paramref name="size" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to chunk.</param>
    /// <param name="size">The maximum size of each chunk.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="size" /> is below 1.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements the input sequence split into chunks of size <paramref name="size" />.</returns>
    public static IEnumerable<TSource[]> Chunk<TSource>(this IEnumerable<TSource> source,int size) {
        ArgumentNullException.ThrowIfNull(source);
        if(size<1) {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        return ChunkIterator<TSource>(source,size);
    }

    /// <summary>Concatenates two sequences.</summary>
    /// <param name="first">The first sequence to concatenate.</param>
    /// <param name="second">The sequence to concatenate to the first sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the concatenated elements of the two input sequences.</returns>
    public static IEnumerable<TSource> Concat<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return ConcatIterator<TSource>(first,second);
    }

    /// <summary>Determines whether a sequence contains a specified element by using the default equality comparer.</summary>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="value">The value to locate in the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains an element that has the specified value; otherwise, <see langword="false" />.</returns>
    public static bool Contains<TSource>(this IEnumerable<TSource> source,TSource value) {
        return Contains(source,value,null);
    }

    /// <summary>Determines whether a sequence contains a specified element by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="value">The value to locate in the sequence.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains an element that has the specified value; otherwise, <see langword="false" />.</returns>
    public static bool Contains<TSource>(this IEnumerable<TSource> source,TSource value,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        comparer??=EqualityComparer<TSource>.Default;
        foreach(var item in source) {
            if(comparer.Equals(item,value)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>Returns the number of elements in a sequence.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements in <paramref name="source" /> is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The number of elements in the input sequence.</returns>
    public static int Count<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        if(source is ICollection<TSource> collection) {
            return collection.Count;
        }
        if(source is IReadOnlyCollection<TSource> readOnlyCollection) {
            return readOnlyCollection.Count;
        }
        var count = 0;
        foreach(var item in source) {
            _=item;
            checked {
                count++;
            }
        }
        return count;
    }

    /// <summary>Returns a number that represents how many elements in the specified sequence satisfy a condition.</summary>
    /// <param name="source">A sequence that contains elements to be tested and counted.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements in <paramref name="source" /> is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>A number that represents how many elements in the sequence satisfy the condition in the predicate function.</returns>
    public static int Count<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var count = 0;
        foreach(var item in source) {
            if(predicate(item)) {
                checked {
                    count++;
                }
            }
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
    public static IEnumerable<KeyValuePair<TKey,int>> CountBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        return CountByIterator<TSource,TKey>(source,keySelector,keyComparer);
    }

    /// <summary>Returns the elements of the specified sequence or the type parameter's default value in a singleton collection if the sequence is empty.</summary>
    /// <param name="source">The sequence to return a default value for if it is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> object that contains the default value for the <typeparamref name="TSource" /> type if <paramref name="source" /> is empty; otherwise, <paramref name="source" />.</returns>
    public static IEnumerable<TSource?> DefaultIfEmpty<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        return DefaultIfEmptyIterator<TSource>(source);
    }

    /// <summary>Returns the elements of the specified sequence or the specified value in a singleton collection if the sequence is empty.</summary>
    /// <param name="source">The sequence to return the specified value for if it is empty.</param>
    /// <param name="defaultValue">The value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains <paramref name="defaultValue" /> if <paramref name="source" /> is empty; otherwise, <paramref name="source" />.</returns>
    public static IEnumerable<TSource> DefaultIfEmpty<TSource>(this IEnumerable<TSource> source,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        return DefaultIfEmptyIterator<TSource>(source,defaultValue);
    }

    /// <summary>Returns distinct elements from a sequence according to a specified key selector function.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return DistinctBy(source,keySelector,null);
    }

    /// <summary>Returns distinct elements from a sequence according to a specified key selector function and using a specified comparer to compare keys.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        return DistinctByIterator<TSource,TKey>(source,keySelector,comparer);
    }

    /// <summary>Returns distinct elements from a sequence by using the default equality comparer to compare values.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source) {
        return Distinct(source,null);
    }

    /// <summary>Returns distinct elements from a sequence by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        return DistinctIterator<TSource>(source,comparer);
    }

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The index of the element to retrieve, which is either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" /> if <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence; otherwise, the element at the specified position in the <paramref name="source" /> sequence.</returns>
    public static TSource? ElementAtOrDefault<TSource>(this IEnumerable<TSource> source,Index index) {
        ArgumentNullException.ThrowIfNull(source);
        var list = ToList(source);
        var offset = index.GetOffset(list.Count);
        return offset<0||offset>=list.Count ? default : list[offset];
    }

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the index is outside the bounds of the source sequence; otherwise, the element at the specified position in the source sequence.</returns>
    public static TSource? ElementAtOrDefault<TSource>(this IEnumerable<TSource> source,int index) {
        ArgumentNullException.ThrowIfNull(source);
        if(index<0) {
            return default;
        }
        if(source is IList<TSource> list) {
            return index<list.Count ? list[index] : default;
        }
        var current = 0;
        foreach(var item in source) {
            if(current==index) {
                return item;
            }
            current++;
        }
        return default;
    }

    /// <summary>Returns the element at a specified index in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The index of the element to retrieve, which is either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence.</exception>
    /// <returns>The element at the specified position in the <paramref name="source" /> sequence.</returns>
    public static TSource ElementAt<TSource>(this IEnumerable<TSource> source,Index index) {
        ArgumentNullException.ThrowIfNull(source);
        var list = ToList(source);
        var offset = index.GetOffset(list.Count);
        if(offset<0||offset>=list.Count) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return list[offset];
    }

    /// <summary>Returns the element at a specified index in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="index" /> is less than 0 or greater than or equal to the number of elements in <paramref name="source" />.</exception>
    /// <returns>The element at the specified position in the source sequence.</returns>
    public static TSource ElementAt<TSource>(this IEnumerable<TSource> source,int index) {
        ArgumentNullException.ThrowIfNull(source);
        if(index<0) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        if(source is IList<TSource> list) {
            if(index<list.Count) {
                return list[index];
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        var current = 0;
        foreach(var item in source) {
            if(current==index) {
                return item;
            }
            current++;
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <summary>Returns an empty <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has the specified type argument.</summary>
    /// <typeparam name="TResult">The type to assign to the type parameter of the returned generic <see cref="T:System.Collections.Generic.IEnumerable`1" />.</typeparam>
    /// <returns>An empty <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose type argument is <typeparamref name="TResult" />.</returns>
    public static IEnumerable<TResult> Empty<TResult>() {
        return Array.Empty<TResult>();
    }

    /// <summary>Produces the set difference of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> ExceptBy<TSource, TKey>(this IEnumerable<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector) {
        return ExceptBy(first,second,keySelector,null);
    }

    /// <summary>Produces the set difference of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> ExceptBy<TSource, TKey>(this IEnumerable<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(keySelector);
        return ExceptByIterator<TSource,TKey>(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set difference of two sequences by using the default equality comparer to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second) {
        return Except(first,second,null);
    }

    /// <summary>Produces the set difference of two sequences by using the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return ExceptIterator<TSource>(first,second,comparer);
    }

    /// <summary>Returns the first element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if <paramref name="source" /> is empty; otherwise, the first element in <paramref name="source" />.</returns>
    public static TSource? FirstOrDefault<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        foreach(var item in source) {
            return item;
        }
        return default;
    }

    /// <summary>Returns the first element of a sequence, or a specified default value if the sequence contains no elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if <paramref name="source" /> is empty; otherwise, the first element in <paramref name="source" />.</returns>
    public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        foreach(var item in source) {
            return item;
        }
        return defaultValue;
    }

    /// <summary>Returns the first element of the sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if <paramref name="source" /> is empty or if no element passes the test specified by <paramref name="predicate" />; otherwise, the first element in <paramref name="source" /> that passes the test specified by <paramref name="predicate" />.</returns>
    public static TSource? FirstOrDefault<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        foreach(var item in source) {
            if(predicate(item)) {
                return item;
            }
        }
        return default;
    }

    /// <summary>Returns the first element of the sequence that satisfies a condition, or a specified default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if <paramref name="source" /> is empty or if no element passes the test specified by <paramref name="predicate" />; otherwise, the first element in <paramref name="source" /> that passes the test specified by <paramref name="predicate" />.</returns>
    public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        foreach(var item in source) {
            if(predicate(item)) {
                return item;
            }
        }
        return defaultValue;
    }

    /// <summary>Returns the first element of a sequence.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The source sequence is empty.</exception>
    /// <returns>The first element in the specified sequence.</returns>
    public static TSource First<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        foreach(var item in source) {
            return item;
        }
        throw NoElements();
    }

    /// <summary>Returns the first element in a sequence that satisfies a specified condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">No element satisfies the condition in <paramref name="predicate" />.
    ///
    ///  -or-
    ///
    ///  The source sequence is empty.</exception>
    /// <returns>The first element in the sequence that passes the test in the specified predicate function.</returns>
    public static TSource First<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        foreach(var item in source) {
            if(predicate(item)) {
                return item;
            }
        }
        throw NoMatch();
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;System.Linq.IGrouping&lt;TKey, TSource&gt;&gt;</c> in C# or <c>IEnumerable(Of System.Linq.IGrouping(Of TKey, TSource))</c> in Visual Basic where each <see cref="T:System.Linq.System.Linq.IGrouping`2" /> object contains a sequence of objects and a key.</returns>
    public static IEnumerable<System.Linq.IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return GroupBy(source,keySelector,static item => item,null);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and compares the keys by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;System.Linq.IGrouping&lt;TKey, TSource&gt;&gt;</c> in C# or <c>IEnumerable(Of System.Linq.IGrouping(Of TKey, TSource))</c> in Visual Basic where each <see cref="T:System.Linq.System.Linq.IGrouping`2" /> object contains a collection of objects and a key.</returns>
    public static IEnumerable<System.Linq.IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        return GroupBy(source,keySelector,static item => item,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and projects the elements for each group by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in the <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;System.Linq.IGrouping&lt;TKey, TElement&gt;&gt;</c> in C# or <c>IEnumerable(Of System.Linq.IGrouping(Of TKey, TElement))</c> in Visual Basic where each <see cref="T:System.Linq.System.Linq.IGrouping`2" /> object contains a collection of objects of type <typeparamref name="TElement" /> and a key.</returns>
    public static IEnumerable<System.Linq.IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector) {
        return GroupBy(source,keySelector,elementSelector,null);
    }

    /// <summary>Groups the elements of a sequence according to a key selector function. The keys are compared by using a comparer and each group's elements are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;System.Linq.IGrouping&lt;TKey, TElement&gt;&gt;</c> in C# or <c>IEnumerable(Of System.Linq.IGrouping(Of TKey, TElement))</c> in Visual Basic where each <see cref="T:System.Linq.System.Linq.IGrouping`2" /> object contains a collection of objects of type <typeparamref name="TElement" /> and a key.</returns>
    public static IEnumerable<System.Linq.IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        return GroupByIterator<TSource,TKey,TElement>(source,keySelector,elementSelector,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector) {
        return GroupBy(source,keySelector,resultSelector,null);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key. The keys are compared by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return GroupByIterator<TSource,TKey,TResult>(source,keySelector,resultSelector,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key. The elements of each group are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in each <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector) {
        return GroupBy(source,keySelector,elementSelector,resultSelector,null);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and creates a result value from each group and its key. Key values are compared by using a specified comparer, and the elements of each group are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</param>
    /// <param name="resultSelector">A function to create a result value from each group.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in each <see cref="T:System.Linq.System.Linq.IGrouping`2" />.</typeparam>
    /// <typeparam name="TResult">The type of the result value returned by <paramref name="resultSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return GroupByIterator<TSource,TKey,TElement,TResult>(source,keySelector,elementSelector,resultSelector,comparer);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements of type <typeparamref name="TResult" /> that are obtained by performing a grouped join on two sequences.</returns>
    public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector) {
        return GroupJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements of type <typeparamref name="TResult" /> that are obtained by performing a grouped join on two sequences.</returns>
    public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return GroupJoinIterator<TOuter,TInner,TKey,TResult>(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns an enumerable that incorporates the element's index into a tuple.</summary>
    /// <param name="source">The source enumerable providing the elements.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An enumerable that incorporates each element index into a tuple.</returns>
    public static IEnumerable<(int Index,TSource Item)> Index<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        return IndexIterator<TSource>(source);
    }

    /// <summary>Generates an infinite sequence that begins with <paramref name="start" /> and yields additional values each incremented by <paramref name="step" />.</summary>
    /// <param name="start">The starting value.</param>
    /// <param name="step">The amount by which the next yielded value should be incremented from the previous yielded value.</param>
    /// <typeparam name="T">The type of the value to be yielded in the result sequence.</typeparam>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the sequence.</returns>
    public static IEnumerable<T> InfiniteSequence<T>(T start,T step)
        where T : IAdditionOperators<T,T,T> {
        return new InfiniteSequenceEnumerable<T>(start,step);
    }

    /// <summary>Produces the set intersection of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> IntersectBy<TSource, TKey>(this IEnumerable<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector) {
        return IntersectBy(first,second,keySelector,null);
    }

    /// <summary>Produces the set intersection of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> IntersectBy<TSource, TKey>(this IEnumerable<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(keySelector);
        return IntersectByIterator<TSource,TKey>(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set intersection of two sequences by using the default equality comparer to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> Intersect<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second) {
        return Intersect(first,second,null);
    }

    /// <summary>Produces the set intersection of two sequences by using the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static IEnumerable<TSource> Intersect<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return IntersectIterator<TSource>(first,second,comparer);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing an inner join on two sequences.</returns>
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector) {
        return Join(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing an inner join on two sequences.</returns>
    public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        var result = new List<TResult>();
        var lookup = CreateLookup(inner,innerKeySelector,static item => item,comparer);
        foreach(var outerItem in outer) {
            foreach(var innerItem in lookup[outerKeySelector(outerItem)]) {
                result.Add(resultSelector(outerItem,innerItem));
            }
        }
        return result;
        //return JoinIterator<TOuter,TInner,TKey,TResult>(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns the last element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the source sequence is empty; otherwise, the last element in the <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static TSource? LastOrDefault<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        var hasAny = false;
        TSource? result = default;
        foreach(var item in source) {
            hasAny=true;
            result=item;
        }
        return hasAny ? result : default;
    }

    /// <summary>Returns the last element of a sequence, or a specified default value if the sequence contains no elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if the source sequence is empty; otherwise, the last element in the <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static TSource LastOrDefault<TSource>(this IEnumerable<TSource> source,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        var result = defaultValue;
        foreach(var item in source) {
            result=item;
        }
        return result;
    }

    /// <summary>Returns the last element of a sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the sequence is empty or if no elements pass the test in the predicate function; otherwise, the last element that passes the test in the predicate function.</returns>
    public static TSource? LastOrDefault<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var hasAny = false;
        TSource? result = default;
        foreach(var item in source) {
            if(predicate(item)) {
                hasAny=true;
                result=item;
            }
        }
        return hasAny ? result : default;
    }

    /// <summary>Returns the last element of a sequence that satisfies a condition, or a specified default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if the sequence is empty or if no elements pass the test in the predicate function; otherwise, the last element that passes the test in the predicate function.</returns>
    public static TSource LastOrDefault<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var result = defaultValue;
        foreach(var item in source) {
            if(predicate(item)) {
                result=item;
            }
        }
        return result;
    }

    /// <summary>Returns the last element of a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The source sequence is empty.</exception>
    /// <returns>The value at the last position in the source sequence.</returns>
    public static TSource Last<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        var hasAny = false;
        TSource? result = default;
        foreach(var item in source) {
            hasAny=true;
            result=item;
        }
        return hasAny ? result! : throw NoElements();
    }

    /// <summary>Returns the last element of a sequence that satisfies a specified condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">No element satisfies the condition in <paramref name="predicate" />.
    ///
    ///  -or-
    ///
    ///  The source sequence is empty.</exception>
    /// <returns>The last element in the sequence that passes the test in the specified predicate function.</returns>
    public static TSource Last<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var hasAny = false;
        TSource? result = default;
        foreach(var item in source) {
            if(predicate(item)) {
                hasAny=true;
                result=item;
            }
        }
        return hasAny ? result! : throw NoMatch();
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a left outer join on two sequences.</returns>
    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector) {
        return LeftJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a left outer join on two sequences.</returns>
    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return LeftJoinIterator<TOuter,TInner,TKey,TResult>(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns an <see cref="T:System.Int64" /> that represents the total number of elements in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to be counted.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements exceeds <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The number of elements in the source sequence.</returns>
    public static long LongCount<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        long count = 0;
        foreach(var item in source) {
            _=item;
            checked {
                count++;
            }
        }
        return count;
    }

    /// <summary>Returns an <see cref="T:System.Int64" /> that represents how many elements in a sequence satisfy a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to be counted.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of matching elements exceeds <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>A number that represents how many elements in the sequence satisfy the condition in the predicate function.</returns>
    public static long LongCount<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        long count = 0;
        foreach(var item in source) {
            if(predicate(item)) {
                checked {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static decimal Max(this IEnumerable<decimal> source) {
        return Extremum(source,Comparer<decimal>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static double Max(this IEnumerable<double> source) {
        return Extremum(source,Comparer<double>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static int Max(this IEnumerable<int> source) {
        return Extremum(source,Comparer<int>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static long Max(this IEnumerable<long> source) {
        return Extremum(source,Comparer<long>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of nullable <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Decimal" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A value of type <c>Nullable&lt;Decimal&gt;</c> in C# or <c>Nullable(Of Decimal)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static decimal? Max(this IEnumerable<decimal?> source) {
        return NullableExtremum(source,Comparer<decimal>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of nullable <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Double" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A value of type <c>Nullable&lt;Double&gt;</c> in C# or <c>Nullable(Of Double)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static double? Max(this IEnumerable<double?> source) {
        return NullableExtremum(source,Comparer<double>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of nullable <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int32" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A value of type <c>Nullable&lt;Int32&gt;</c> in C# or <c>Nullable(Of Int32)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static int? Max(this IEnumerable<int?> source) {
        return NullableExtremum(source,Comparer<int>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of nullable <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int64" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A value of type <c>Nullable&lt;Int64&gt;</c> in C# or <c>Nullable(Of Int64)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static long? Max(this IEnumerable<long?> source) {
        return NullableExtremum(source,Comparer<long>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of nullable <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Single" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A value of type <c>Nullable&lt;Single&gt;</c> in C# or <c>Nullable(Of Single)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static float? Max(this IEnumerable<float?> source) {
        return NullableExtremum(source,Comparer<float>.Default,true);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static float Max(this IEnumerable<float> source) {
        return Extremum(source,Comparer<float>.Default,true);
    }

    /// <summary>Returns the maximum value in a generic sequence according to a specified key selector function.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the maximum key in the sequence.</returns>
    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return MaxBy(source,keySelector,null);
    }

    /// <summary>Returns the maximum value in a generic sequence according to a specified key selector function and key comparer.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the maximum key in the sequence.</returns>
    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return ExtremumBy(source,keySelector,comparer,true);
    }

    /// <summary>Returns the maximum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TSource? Max<TSource>(this IEnumerable<TSource> source) {
        return Max(source,(IComparer<TSource>?)null);
    }

    /// <summary>Returns the maximum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TSource? Max<TSource>(this IEnumerable<TSource> source,IComparer<TSource>? comparer) {
        return GenericExtremum(source,comparer,true);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static decimal Max<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static double Max<TSource>(this IEnumerable<TSource> source,Func<TSource,double> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static int Max<TSource>(this IEnumerable<TSource> source,Func<TSource,int> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static long Max<TSource>(this IEnumerable<TSource> source,Func<TSource,long> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Decimal&gt;</c> in C# or <c>Nullable(Of Decimal)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static decimal? Max<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Double&gt;</c> in C# or <c>Nullable(Of Double)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static double? Max<TSource>(this IEnumerable<TSource> source,Func<TSource,double?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Int32&gt;</c> in C# or <c>Nullable(Of Int32)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static int? Max<TSource>(this IEnumerable<TSource> source,Func<TSource,int?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the maximum value in the sequence.</returns>
    public static long? Max<TSource>(this IEnumerable<TSource> source,Func<TSource,long?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the maximum value in the sequence.</returns>
    public static float? Max<TSource>(this IEnumerable<TSource> source,Func<TSource,float?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static float Max<TSource>(this IEnumerable<TSource> source,Func<TSource,float> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a generic sequence and returns the maximum resulting value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TResult? Max<TSource, TResult>(this IEnumerable<TSource> source,Func<TSource,TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Max(Project(source,selector));
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static decimal Min(this IEnumerable<decimal> source) {
        return Extremum(source,Comparer<decimal>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static double Min(this IEnumerable<double> source) {
        return Extremum(source,Comparer<double>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static int Min(this IEnumerable<int> source) {
        return Extremum(source,Comparer<int>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static long Min(this IEnumerable<long> source) {
        return Extremum(source,Comparer<long>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of nullable <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Decimal" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static decimal? Min(this IEnumerable<decimal?> source) {
        return NullableExtremum(source,Comparer<decimal>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of nullable <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Double" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static double? Min(this IEnumerable<double?> source) {
        return NullableExtremum(source,Comparer<double>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of nullable <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int32" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static int? Min(this IEnumerable<int?> source) {
        return NullableExtremum(source,Comparer<int>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of nullable <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int64" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static long? Min(this IEnumerable<long?> source) {
        return NullableExtremum(source,Comparer<long>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of nullable <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Single" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static float? Min(this IEnumerable<float?> source) {
        return NullableExtremum(source,Comparer<float>.Default,false);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static float Min(this IEnumerable<float> source) {
        return Extremum(source,Comparer<float>.Default,false);
    }

    /// <summary>Returns the minimum value in a generic sequence according to a specified key selector function.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the minimum key in the sequence.</returns>
    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return MinBy(source,keySelector,null);
    }

    /// <summary>Returns the minimum value in a generic sequence according to a specified key selector function and key comparer.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to compare elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No key extracted from <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <typeparamref name="TSource" /> is a primitive type and the source sequence is empty.</exception>
    /// <returns>The value with the minimum key in the sequence.</returns>
    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return ExtremumBy(source,keySelector,comparer,false);
    }

    /// <summary>Returns the minimum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TSource? Min<TSource>(this IEnumerable<TSource> source) {
        return Min(source,(IComparer<TSource>?)null);
    }

    /// <summary>Returns the minimum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TSource? Min<TSource>(this IEnumerable<TSource> source,IComparer<TSource>? comparer) {
        return GenericExtremum(source,comparer,false);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static decimal Min<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static double Min<TSource>(this IEnumerable<TSource> source,Func<TSource,double> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static int Min<TSource>(this IEnumerable<TSource> source,Func<TSource,int> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static long Min<TSource>(this IEnumerable<TSource> source,Func<TSource,long> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static decimal? Min<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static double? Min<TSource>(this IEnumerable<TSource> source,Func<TSource,double?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static int? Min<TSource>(this IEnumerable<TSource> source,Func<TSource,int?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static long? Min<TSource>(this IEnumerable<TSource> source,Func<TSource,long?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static float? Min<TSource>(this IEnumerable<TSource> source,Func<TSource,float?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static float Min<TSource>(this IEnumerable<TSource> source,Func<TSource,float> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Invokes a transform function on each element of a generic sequence and returns the minimum resulting value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TResult? Min<TSource, TResult>(this IEnumerable<TSource> source,Func<TSource,TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Min(Project(source,selector));
    }

    /// <summary>Filters the elements of an <see cref="T:System.Collections.IEnumerable" /> based on a specified type.</summary>
    /// <param name="source">The <see cref="T:System.Collections.IEnumerable" /> whose elements to filter.</param>
    /// <typeparam name="TResult">The type to filter the elements of the sequence on.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence of type <typeparamref name="TResult" />.</returns>
    public static IEnumerable<TResult> OfType<TResult>(this IEnumerable source) {
        ArgumentNullException.ThrowIfNull(source);
        return OfTypeIterator<TResult>(source);
    }

    /// <summary>Sorts the elements of a sequence in descending order according to a key.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return OrderByDescending(source,keySelector,null);
    }

    /// <summary>Sorts the elements of a sequence in descending order by using a specified comparer.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return CreateOrdered(source,keySelector,comparer,true);
    }

    /// <summary>Sorts the elements of a sequence in ascending order according to a key.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return OrderBy(source,keySelector,null);
    }

    /// <summary>Sorts the elements of a sequence in ascending order by using a specified comparer.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        return CreateOrdered(source,keySelector,comparer,false);
    }

    /// <summary>Sorts the elements of a sequence in descending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static System.Linq.IOrderedEnumerable<T> OrderDescending<T>(this IEnumerable<T> source) {
        return OrderDescending(source,null);
    }

    /// <summary>Sorts the elements of a sequence in descending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static System.Linq.IOrderedEnumerable<T> OrderDescending<T>(this IEnumerable<T> source,IComparer<T>? comparer) {
        return CreateOrdered(source,static item => item,comparer,true);
    }

    /// <summary>Sorts the elements of a sequence in ascending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static System.Linq.IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source) {
        return Order(source,null);
    }

    /// <summary>Sorts the elements of a sequence in ascending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static System.Linq.IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source,IComparer<T>? comparer) {
        return CreateOrdered(source,static item => item,comparer,false);
    }

    /// <summary>Adds a value to the beginning of the sequence.</summary>
    /// <param name="source">A sequence of values.</param>
    /// <param name="element">The value to prepend to <paramref name="source" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new sequence that begins with <paramref name="element" />.</returns>
    public static IEnumerable<TSource> Prepend<TSource>(this IEnumerable<TSource> source,TSource element) {
        ArgumentNullException.ThrowIfNull(source);
        return PrependIterator<TSource>(source,element);
    }

    /// <summary>Generates a sequence of integral numbers within a specified range.</summary>
    /// <param name="start">The value of the first integer in the sequence.</param>
    /// <param name="count">The number of sequential integers to generate.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="count" /> is less than 0.
    ///
    ///  -or-
    ///
    ///  <paramref name="start" /> + <paramref name="count" /> -1 is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>An <c>IEnumerable&lt;Int32&gt;</c> in C# or <c>IEnumerable(Of Int32)</c> in Visual Basic that contains a range of sequential integral numbers.</returns>
    public static IEnumerable<int> Range(int start,int count) {
        if(count<0||(count>0&&(long)start+count-1>int.MaxValue)) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        return RangeIterator(start,count);
    }

    /// <summary>Generates a sequence that contains one repeated value.</summary>
    /// <param name="element">The value to be repeated.</param>
    /// <param name="count">The number of times to repeat the value in the generated sequence.</param>
    /// <typeparam name="TResult">The type of the value to be repeated in the result sequence.</typeparam>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="count" /> is less than 0.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains a repeated value.</returns>
    public static IEnumerable<TResult> Repeat<TResult>(TResult element,int count) {
        if(count<0) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        return RepeatIterator<TResult>(element,count);
    }

    /// <summary>Inverts the order of the elements in a sequence.</summary>
    /// <param name="source">A sequence of values to reverse.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A sequence whose elements correspond to those of the input sequence in reverse order.</returns>
    public static IEnumerable<TSource> Reverse<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        return ReverseIterator<TSource>(source);
    }

    /// <param name="source" />
    /// <typeparam name="TSource" />
    public static IEnumerable<TSource> Reverse<TSource>(this TSource[] source) {
        ArgumentNullException.ThrowIfNull(source);
        return ReverseIterator<TSource>(source);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a right outer join on two sequences.</returns>
    public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector) {
        return RightJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector,null);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a right outer join on two sequences.</returns>
    public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return RightJoinIterator<TOuter,TInner,TKey,TResult>(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" /> and flattens the resulting sequences into one sequence.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function on each element of the input sequence.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return SelectManyIterator<TSource,TResult>(source,selector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, and flattens the resulting sequences into one sequence. The index of each source element is used in the projected form of that element.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function on each element of an input sequence.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source,Func<TSource,int,IEnumerable<TResult>> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return SelectManyIterator<TSource,TResult>(source,selector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, flattens the resulting sequences into one sequence, and invokes a result selector function on each element therein.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="collectionSelector">A transform function to apply to each element of the input sequence.</param>
    /// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="collectionSelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the resulting sequence.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="collectionSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function <paramref name="collectionSelector" /> on each element of <paramref name="source" /> and then mapping each of those sequence elements and their corresponding source element to a result element.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IEnumerable<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return SelectManyIterator<TSource,TCollection,TResult>(source,collectionSelector,resultSelector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, flattens the resulting sequences into one sequence, and invokes a result selector function on each element therein. The index of each source element is used in the intermediate projected form of that element.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="collectionSelector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <param name="resultSelector">A transform function to apply to each element of the intermediate sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TCollection">The type of the intermediate elements collected by <paramref name="collectionSelector" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the resulting sequence.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="collectionSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function <paramref name="collectionSelector" /> on each element of <paramref name="source" /> and then mapping each of those sequence elements and their corresponding source element to a result element.</returns>
    public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IEnumerable<TSource> source,Func<TSource,int,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return SelectManyIterator<TSource,TCollection,TResult>(source,collectionSelector,resultSelector);
    }

    /// <summary>Projects each element of a sequence into a new form by incorporating the element's index.</summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of <paramref name="source" />.</returns>
    public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source,Func<TSource,int,TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return SelectIterator<TSource,TResult>(source,selector);
    }

    /// <summary>Projects each element of a sequence into a new form.</summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of <paramref name="source" />.</returns>
    public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source,Func<TSource,TResult> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var result = new List<TResult>();
        foreach(var item in source) {
            result.Add(Inline(item,selector));
        }
        return result;
        static TResult Inline(TSource item,Func<TSource,TResult> selector) {
            return selector(item);
        }
    }

    /// <summary>Generates a sequence that begins with <paramref name="start" /> and yields additional values each incremented by <paramref name="step" /> until <paramref name="endInclusive" /> is reached.</summary>
    /// <param name="start">The starting value. This value will always be included in the resulting sequence.</param>
    /// <param name="endInclusive">The ending bound beyond which values will not be included in the sequence.</param>
    /// <param name="step">The amount by which the next value in the sequence should be incremented from the previous value.</param>
    /// <typeparam name="T">The type of the value to be yielded in the result sequence.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="start" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="endInclusive" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="step" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="start" /> is NaN.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="endInclusive" /> is NaN.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="step" /> is NaN.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="step" /> is greater than zero but <paramref name="endInclusive" /> is less than <paramref name="start" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="step" /> is less than zero but <paramref name="endInclusive" /> is greater than <paramref name="start" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///   <paramref name="step" /> is zero and <paramref name="endInclusive" /> does not equal <paramref name="start" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the sequence.</returns>
    public static IEnumerable<T> Sequence<T>(T start,T endInclusive,T step)
        where T : INumber<T> {
        if(T.IsNaN(start)) {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if(T.IsNaN(endInclusive)) {
            throw new ArgumentOutOfRangeException(nameof(endInclusive));
        }
        if(T.IsNaN(step)) {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
        if(step==T.Zero) {
            if(start!=endInclusive) {
                throw new ArgumentOutOfRangeException(nameof(step));
            }
            return Repeat(start,1);
        }
        if(step>T.Zero&&endInclusive<start) {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
        if(step<T.Zero&&endInclusive>start) {
            throw new ArgumentOutOfRangeException(nameof(step));
        }
        return SequenceIterator<T>(start,endInclusive,step);
    }

    /// <summary>Determines whether two sequences are equal by comparing the elements by using the default equality comparer for their type.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to <paramref name="second" />.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to the first sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the two source sequences are of equal length and their corresponding elements are equal according to the default equality comparer for their type; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second) {
        return SequenceEqual(first,second,null);
    }

    /// <summary>Determines whether two sequences are equal by comparing their elements by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to <paramref name="second" />.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to the first sequence.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to use to compare elements.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the two source sequences are of equal length and their corresponding elements compare equal according to <paramref name="comparer" />; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        comparer??=EqualityComparer<TSource>.Default;
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        while(true) {
            var hasFirst = firstEnumerator.MoveNext();
            var hasSecond = secondEnumerator.MoveNext();
            if(hasFirst!=hasSecond) {
                return false;
            }
            if(!hasFirst) {
                return true;
            }
            if(!comparer.Equals(firstEnumerator.Current,secondEnumerator.Current)) {
                return false;
            }
        }
    }

    /// <summary>Shuffles the order of the elements of a sequence.</summary>
    /// <param name="source">A sequence of values to shuffle.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>A sequence whose elements correspond to those of the input sequence in randomized order.</returns>
    public static IEnumerable<TSource> Shuffle<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        return ShuffleIterator<TSource>(source);
    }

    /// <summary>Returns the only element of a sequence, or a default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.</exception>
    /// <returns>The single element of the input sequence, or <see langword="default" />(<typeparamref name="TSource" />) if the sequence contains no elements.</returns>
    public static TSource? SingleOrDefault<TSource>(this IEnumerable<TSource> source) {
        return SingleOrDefault(source,default(TSource));
    }

    /// <summary>Returns the only element of a sequence, or a specified default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.</exception>
    /// <returns>The single element of the input sequence, or <paramref name="defaultValue" /> if the sequence contains no elements.</returns>
    public static TSource SingleOrDefault<TSource>(this IEnumerable<TSource> source,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        using var enumerator = source.GetEnumerator();
        if(!enumerator.MoveNext()) {
            return defaultValue;
        }
        var result = enumerator.Current;
        if(enumerator.MoveNext()) {
            throw MoreThanOneElement();
        }
        return result;
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition or a default value if no such element exists; this method throws an exception if more than one element satisfies the condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">More than one element satisfies the condition in <paramref name="predicate" />.</exception>
    /// <returns>The single element of the input sequence that satisfies the condition, or <see langword="default" />(<typeparamref name="TSource" />) if no such element is found.</returns>
    public static TSource? SingleOrDefault<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        return SingleOrDefault(source,predicate,default!);
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition, or a specified default value if no such element exists; this method throws an exception if more than one element satisfies the condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">More than one element satisfies the condition in <paramref name="predicate" />.</exception>
    /// <returns>The single element of the input sequence that satisfies the condition, or <paramref name="defaultValue" /> if no such element is found.</returns>
    public static TSource SingleOrDefault<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate,TSource defaultValue) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var found = false;
        var result = defaultValue;
        foreach(var item in source) {
            if(predicate(item)) {
                if(found) {
                    throw MoreThanOneMatch();
                }
                found=true;
                result=item;
            }
        }
        return result;
    }

    /// <summary>Returns the only element of a sequence, and throws an exception if there is not exactly one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.
    ///
    ///  -or-
    ///
    ///  The input sequence is empty.</exception>
    /// <returns>The single element of the input sequence.</returns>
    public static TSource Single<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        using var enumerator = source.GetEnumerator();
        if(!enumerator.MoveNext()) {
            throw NoElements();
        }
        var result = enumerator.Current;
        if(enumerator.MoveNext()) {
            throw MoreThanOneElement();
        }
        return result;
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition, and throws an exception if more than one such element exists.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
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
    public static TSource Single<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var found = false;
        TSource? result = default;
        foreach(var item in source) {
            if(predicate(item)) {
                if(found) {
                    throw MoreThanOneMatch();
                }
                found=true;
                result=item;
            }
        }
        return found ? result! : throw NoMatch();
    }

    /// <summary>Returns a new enumerable collection that contains the elements from <paramref name="source" /> with the last <paramref name="count" /> elements of the source collection omitted.</summary>
    /// <param name="source">An enumerable collection instance.</param>
    /// <param name="count">The number of elements to omit from the end of the collection.</param>
    /// <typeparam name="TSource">The type of the elements in the enumerable collection.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new enumerable collection that contains the elements from <paramref name="source" /> minus <paramref name="count" /> elements from the end of the collection.</returns>
    public static IEnumerable<TSource> SkipLast<TSource>(this IEnumerable<TSource> source,int count) {
        ArgumentNullException.ThrowIfNull(source);
        return SkipLastIterator<TSource>(source,count);
    }

    /// <summary>Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by <paramref name="predicate" />.</returns>
    public static IEnumerable<TSource> SkipWhile<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return SkipWhileIterator<TSource>(source,predicate);
    }

    /// <summary>Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements. The element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by <paramref name="predicate" />.</returns>
    public static IEnumerable<TSource> SkipWhile<TSource>(this IEnumerable<TSource> source,Func<TSource,int,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return SkipWhileIterator<TSource>(source,predicate);
    }

    /// <summary>Bypasses a specified number of elements in a sequence and then returns the remaining elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="count">The number of elements to skip before returning the remaining elements.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements that occur after the specified index in the input sequence.</returns>
    public static IEnumerable<TSource> Skip<TSource>(this IEnumerable<TSource> source,int count) {
        ArgumentNullException.ThrowIfNull(source);
        return SkipIterator<TSource>(source,count);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static decimal Sum(this IEnumerable<decimal> source) {
        ArgumentNullException.ThrowIfNull(source);
        decimal sum = 0;
        foreach(var item in source) {
            sum+=item;
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static double Sum(this IEnumerable<double> source) {
        ArgumentNullException.ThrowIfNull(source);
        double sum = 0;
        foreach(var item in source) {
            sum+=item;
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static int Sum(this IEnumerable<int> source) {
        ArgumentNullException.ThrowIfNull(source);
        var sum = 0;
        foreach(var item in source) {
            checked {
                sum+=item;
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static long Sum(this IEnumerable<long> source) {
        ArgumentNullException.ThrowIfNull(source);
        long sum = 0;
        foreach(var item in source) {
            checked {
                sum+=item;
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of nullable <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Decimal" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static decimal? Sum(this IEnumerable<decimal?> source) {
        ArgumentNullException.ThrowIfNull(source);
        decimal sum = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                sum+=item.GetValueOrDefault();
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of nullable <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Double" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static double? Sum(this IEnumerable<double?> source) {
        ArgumentNullException.ThrowIfNull(source);
        double sum = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                sum+=item.GetValueOrDefault();
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of nullable <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int32" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static int? Sum(this IEnumerable<int?> source) {
        ArgumentNullException.ThrowIfNull(source);
        var sum = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                checked {
                    sum+=item.GetValueOrDefault();
                }
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of nullable <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Int64" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static long? Sum(this IEnumerable<long?> source) {
        ArgumentNullException.ThrowIfNull(source);
        long sum = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                checked {
                    sum+=item.GetValueOrDefault();
                }
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of nullable <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of nullable <see cref="T:System.Single" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static float? Sum(this IEnumerable<float?> source) {
        ArgumentNullException.ThrowIfNull(source);
        float sum = 0;
        foreach(var item in source) {
            if(item.HasValue) {
                sum+=item.GetValueOrDefault();
            }
        }
        return sum;
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static float Sum(this IEnumerable<float> source) {
        ArgumentNullException.ThrowIfNull(source);
        float sum = 0;
        foreach(var item in source) {
            sum+=item;
        }
        return sum;
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static decimal Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static double Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,double> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static int Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,int> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static long Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,long> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static decimal? Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,decimal?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static double? Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,double?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static int? Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,int?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static long? Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,long?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static float? Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,float?> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static float Sum<TSource>(this IEnumerable<TSource> source,Func<TSource,float> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return Sum(Project(source,selector));
    }

    /// <summary>Returns a new enumerable collection that contains the last <paramref name="count" /> elements from <paramref name="source" />.</summary>
    /// <param name="source">An enumerable collection instance.</param>
    /// <param name="count">The number of elements to take from the end of the collection.</param>
    /// <typeparam name="TSource">The type of the elements in the enumerable collection.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new enumerable collection that contains the last <paramref name="count" /> elements from <paramref name="source" />.</returns>
    public static IEnumerable<TSource> TakeLast<TSource>(this IEnumerable<TSource> source,int count) {
        ArgumentNullException.ThrowIfNull(source);
        return TakeLastIterator<TSource>(source,count);
    }

    /// <summary>Returns elements from a sequence as long as a specified condition is true.</summary>
    /// <param name="source">A sequence to return elements from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence that occur before the element at which the test no longer passes.</returns>
    public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return TakeWhileIterator<TSource>(source,predicate);
    }

    /// <summary>Returns elements from a sequence as long as a specified condition is true. The element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that occur before the element at which the test no longer passes.</returns>
    public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source,Func<TSource,int,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return TakeWhileIterator<TSource>(source,predicate);
    }

    /// <summary>Returns a specified number of contiguous elements from the start of a sequence.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="count">The number of elements to return.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the specified number of elements from the start of the input sequence.</returns>
    public static IEnumerable<TSource> Take<TSource>(this IEnumerable<TSource> source,int count) {
        ArgumentNullException.ThrowIfNull(source);
        return TakeIterator<TSource>(source,count);
    }

    /// <summary>Returns a specified range of contiguous elements from a sequence.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="range">The range of elements to return, which has start and end indexes either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the specified range of elements from the <paramref name="source" /> sequence.</returns>
    public static IEnumerable<TSource> Take<TSource>(this IEnumerable<TSource> source,Range range) {
        ArgumentNullException.ThrowIfNull(source);
        return TakeIterator<TSource>(source,range);
    }

    /// <summary>Performs a subsequent ordering of the elements in a sequence in descending order, according to a key.</summary>
    /// <param name="source">An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> that contains elements to sort.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this System.Linq.IOrderedEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return ThenByDescending(source,keySelector,null);
    }

    /// <summary>Performs a subsequent ordering of the elements in a sequence in descending order by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> that contains elements to sort.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> ThenByDescending<TSource, TKey>(this System.Linq.IOrderedEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        return source.CreateOrderedEnumerable(keySelector,comparer,true);
    }

    /// <summary>Performs a subsequent ordering of the elements in a sequence in ascending order according to a key.</summary>
    /// <param name="source">An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> that contains elements to sort.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this System.Linq.IOrderedEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return ThenBy(source,keySelector,null);
    }

    /// <summary>Performs a subsequent ordering of the elements in a sequence in ascending order by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> that contains elements to sort.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static System.Linq.IOrderedEnumerable<TSource> ThenBy<TSource, TKey>(this System.Linq.IOrderedEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        return source.CreateOrderedEnumerable(keySelector,comparer,false);
    }

    /// <summary>Creates an array from a <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create an array from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An array that contains the elements from the input sequence.</returns>
    public static TSource[] ToArray<TSource>(this IEnumerable<TSource> source) {
        return ToList(source).ToArray();
    }

    /// <summary>Creates a dictionary from an enumeration according to the default comparer for the key type.</summary>
    /// <param name="source">The enumeration to create a dictionary from.</param>
    /// <typeparam name="TKey">The type of the keys from elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TValue">The type of the values from elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is a <see langword="null" /> reference.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="source" /> contains one or more duplicate keys.</exception>
    /// <returns>A dictionary that contains keys and values from <paramref name="source" /> and uses the default comparer for the key type.</returns>
    public static Dictionary<TKey,TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey,TValue>> source)
        where TKey : notnull {
        return ToDictionary(source,null);
    }

    /// <summary>Creates a dictionary from an enumeration according to specified key comparer.</summary>
    /// <param name="source">The enumeration to create a dictionary from.</param>
    /// <param name="comparer">An equality comparer to compare keys.</param>
    /// <typeparam name="TKey">The type of the keys from elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TValue">The type of the values from elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is a <see langword="null" /> reference.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="source" /> contains one or more duplicate keys.</exception>
    /// <returns>A dictionary that contains keys and values from <paramref name="source" />.</returns>
    public static Dictionary<TKey,TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey,TValue>> source,IEqualityComparer<TKey>? comparer)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        var dictionary = new Dictionary<TKey,TValue>(comparer);
        foreach(var pair in source) {
            dictionary.Add(pair.Key,pair.Value);
        }
        return dictionary;
    }

    public static Dictionary<TKey,TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey Key,TValue Value)> source)
        where TKey : notnull {
        return ToDictionary(source,null);
    }

    public static Dictionary<TKey,TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey Key,TValue Value)> source,IEqualityComparer<TKey>? comparer)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        var dictionary = new Dictionary<TKey,TValue>(comparer);
        foreach(var pair in source) {
            dictionary.Add(pair.Key,pair.Value);
        }
        return dictionary;
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static Dictionary<TKey,TSource> ToDictionary<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector)
        where TKey : notnull {
        return ToDictionary(source,keySelector,null);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function and key comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the keys returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static Dictionary<TKey,TSource> ToDictionary<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        var dictionary = new Dictionary<TKey,TSource>(comparer);
        foreach(var item in source) {
            dictionary.Add(keySelector(item),item);
        }
        return dictionary;
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to specified key selector and element selector functions.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector)
        where TKey : notnull {
        return ToDictionary(source,keySelector,elementSelector,null);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.Dictionary`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function, a comparer, and an element selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.Dictionary`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer)
        where TKey : notnull {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        var dictionary = new Dictionary<TKey,TElement>(comparer);
        foreach(var item in source) {
            dictionary.Add(keySelector(item),elementSelector(item));
        }
        return dictionary;
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.HashSet`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.HashSet`1" /> from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.HashSet`1" /> that contains values of type <typeparamref name="TSource" /> selected from the input sequence.</returns>
    public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source) {
        return ToHashSet(source,null);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.HashSet`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> using the <paramref name="comparer" /> to compare keys.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.HashSet`1" /> from.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.HashSet`1" /> that contains values of type <typeparamref name="TSource" /> selected from the input sequence.</returns>
    public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        var set = new HashSet<TSource>(comparer);
        foreach(var item in source) {
            set.Add(item);
        }
        return set;
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.List`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.List`1" /> from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.List`1" /> that contains elements from the input sequence.</returns>
    public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source) {
        ArgumentNullException.ThrowIfNull(source);
        var list = new List<TSource>();
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static System.Linq.ILookup<TKey,TSource> ToLookup<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        return ToLookup(source,keySelector,null);
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function and key comparer.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static System.Linq.ILookup<TKey,TSource> ToLookup<TSource, TKey>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        return CreateLookup(source,keySelector,static item => item,comparer);
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to specified key selector and element selector functions.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static System.Linq.ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector) {
        return ToLookup(source,keySelector,elementSelector,null);
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function, a comparer and an element selector function.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the value returned by <paramref name="elementSelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static System.Linq.ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement>(this IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        return CreateLookup(source,keySelector,elementSelector,comparer);
    }

    /// <summary>Attempts to determine the number of elements in a sequence without forcing an enumeration.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <param name="count">When this method returns, contains the number of elements in <paramref name="source" />, or 0 if the count couldn't be determined without enumeration.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>
    ///   <see langword="true" /> if the count of <paramref name="source" /> can be determined without enumeration; otherwise, <see langword="false" />.</returns>
    public static bool TryGetNonEnumeratedCount<TSource>(this IEnumerable<TSource> source,out int count) {
        ArgumentNullException.ThrowIfNull(source);
        if(source is ICollection<TSource> collection) {
            count=collection.Count;
            return true;
        }
        if(source is IReadOnlyCollection<TSource> readOnlyCollection) {
            count=readOnlyCollection.Count;
            return true;
        }
        if(source is ICollection nonGenericCollection) {
            count=nonGenericCollection.Count;
            return true;
        }
        count=0;
        return false;
    }

    /// <summary>Produces the set union of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> UnionBy<TSource, TKey>(this IEnumerable<TSource> first,IEnumerable<TSource> second,Func<TSource,TKey> keySelector) {
        return UnionBy(first,second,keySelector,null);
    }

    /// <summary>Produces the set union of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> UnionBy<TSource, TKey>(this IEnumerable<TSource> first,IEnumerable<TSource> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(keySelector);
        return UnionByIterator<TSource,TKey>(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set union of two sequences by using the default equality comparer.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> Union<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second) {
        return Union(first,second,null);
    }

    /// <summary>Produces the set union of two sequences by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static IEnumerable<TSource> Union<TSource>(this IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return UnionIterator<TSource>(first,second,comparer);
    }

    /// <summary>Filters a sequence of values based on a predicate.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to filter.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return WhereIterator<TSource>(source,predicate);
    }

    /// <summary>Filters a sequence of values based on a predicate. Each element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to filter.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source,Func<TSource,int,bool> predicate) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return WhereIterator<TSource>(source,predicate);
    }

    /// <summary>Produces a sequence of tuples with elements from the two specified sequences.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <returns>A sequence of tuples with elements taken from the first and second sequences, in that order.</returns>
    public static IEnumerable<(TFirst First,TSecond Second)> Zip<TFirst, TSecond>(this IEnumerable<TFirst> first,IEnumerable<TSecond> second) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return ZipIterator<TFirst,TSecond>(first,second);
    }

    /// <summary>Produces a sequence of tuples with elements from the three specified sequences.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <param name="third">The third sequence to merge.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <typeparam name="TThird">The type of the elements of the third input sequence.</typeparam>
    /// <returns>A sequence of tuples with elements taken from the first, second, and third sequences, in that order.</returns>
    public static IEnumerable<(TFirst First,TSecond Second,TThird Third)> Zip<TFirst, TSecond, TThird>(this IEnumerable<TFirst> first,IEnumerable<TSecond> second,IEnumerable<TThird> third) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(third);
        return ZipIterator<TFirst,TSecond,TThird>(first,second,third);
    }

    /// <summary>Applies a specified function to the corresponding elements of two sequences, producing a sequence of the results.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <param name="resultSelector">A function that specifies how to merge the elements from the two sequences.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the result sequence.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains merged elements of two input sequences.</returns>
    public static IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first,IEnumerable<TSecond> second,Func<TFirst,TSecond,TResult> resultSelector) {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return ZipIterator<TFirst,TSecond,TResult>(first,second,resultSelector);
    }


    private static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,TAccumulate seed,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull {
        var dictionary = new Dictionary<TKey,TAccumulate>(keyComparer);
        foreach(var item in source) {
            var key = keySelector(item);
            if(!dictionary.TryGetValue(key,out var accumulator)) {
                accumulator=seed;
            }
            dictionary[key]=func(accumulator!,item);
        }
        var result = new List<KeyValuePair<TKey,TAccumulate>>();
        foreach(var pair in dictionary) {
            result.Add(pair);
        }
        return result;
    }

    private static IEnumerable<KeyValuePair<TKey,TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,TAccumulate> seedSelector,Func<TAccumulate,TSource,TAccumulate> func,IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull {
        var dictionary = new Dictionary<TKey,TAccumulate>(keyComparer);
        foreach(var item in source) {
            var key = keySelector(item);
            if(!dictionary.TryGetValue(key,out var accumulator)) {
                accumulator=seedSelector(key);
            }
            dictionary[key]=func(accumulator,item);
        }
        var result = new List<KeyValuePair<TKey,TAccumulate>>();
        foreach(var pair in dictionary) {
            result.Add(pair);
        }
        return result;
    }

    private static IEnumerable<TSource> AppendIterator<TSource>(IEnumerable<TSource> source,TSource element) {
        var result = new List<TSource>();
        foreach(var item in source) {
            result.Add(item);
        }
        result.Add(element);
        return result;
    }

    private static IEnumerable<TResult> CastIterator<TResult>(IEnumerable source) {
        var result = new List<TResult>();
        foreach(var item in source) {
            result.Add((TResult)item!);
        }
        return result;
    }

    private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source,int size) {
        var result = new List<TSource[]>();
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

    private static IEnumerable<TSource> ConcatIterator<TSource>(IEnumerable<TSource> first,IEnumerable<TSource> second) {
        var result = new List<TSource>();
        foreach(var item in first) {
            result.Add(item);
        }
        foreach(var item in second) {
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<KeyValuePair<TKey,int>> CountByIterator<TSource, TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? keyComparer = null) where TKey : notnull {
        var counts = new Dictionary<TKey,int>(keyComparer);
        foreach(var item in source) {
            var key = keySelector(item);
            counts.TryGetValue(key,out var count);
            checked {
                counts[key]=count+1;
            }
        }
        var result = new List<KeyValuePair<TKey,int>>();
        foreach(var pair in counts) {
            result.Add(pair);
        }
        return result;
    }

    private static IEnumerable<TSource?> DefaultIfEmptyIterator<TSource>(IEnumerable<TSource> source) {
        var result = new List<TSource?>();
        foreach(var item in source) {
            result.Add(item);
        }
        if(result.Count==0) {
            result.Add(default);
        }
        return result;
    }

    private static IEnumerable<TSource> DefaultIfEmptyIterator<TSource>(IEnumerable<TSource> source,TSource defaultValue) {
        var result = new List<TSource>();
        foreach(var item in source) {
            result.Add(item);
        }
        if(result.Count==0) {
            result.Add(defaultValue);
        }
        return result;
    }

    private static IEnumerable<TSource> DistinctByIterator<TSource, TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TSource>();
        var seen = new HashSet<TKey>(comparer);
        foreach(var item in source) {
            if(seen.Add(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> DistinctIterator<TSource>(IEnumerable<TSource> source,IEqualityComparer<TSource>? comparer) {
        var result = new List<TSource>();
        var seen = new HashSet<TSource>(comparer);
        foreach(var item in source) {
            if(seen.Add(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> ExceptByIterator<TSource, TKey>(IEnumerable<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TSource>();
        var excluded = new HashSet<TKey>(second,comparer);
        foreach(var item in first) {
            if(excluded.Add(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> ExceptIterator<TSource>(IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        var result = new List<TSource>();
        var excluded = new HashSet<TSource>(second,comparer);
        foreach(var item in first) {
            if(excluded.Add(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<System.Linq.IGrouping<TKey,TElement>> GroupByIterator<TSource, TKey, TElement>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<System.Linq.IGrouping<TKey,TElement>>();
        var lookup = CreateLookup(source,keySelector,elementSelector,comparer);
        foreach(var group in lookup.Groups) {
            result.Add(group);
        }
        return result;
    }

    private static IEnumerable<TResult> GroupByIterator<TSource, TKey, TResult>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TResult>();
        var lookup = CreateLookup(source,keySelector,static item => item,comparer);
        foreach(var group in lookup.Groups) {
            result.Add(resultSelector(group.Key,group));
        }
        return result;
    }

    private static IEnumerable<TResult> GroupByIterator<TSource, TKey, TElement, TResult>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TResult>();
        var lookup = CreateLookup(source,keySelector,elementSelector,comparer);
        foreach(var group in lookup.Groups) {
            result.Add(resultSelector(group.Key,group));
        }
        return result;
    }

    private static IEnumerable<TResult> GroupJoinIterator<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TResult>();
        var lookup = CreateLookup(inner,innerKeySelector,static item => item,comparer);
        foreach(var outerItem in outer) {
            result.Add(resultSelector(outerItem,lookup[outerKeySelector(outerItem)]));
        }
        return result;
    }

    private static IEnumerable<(int Index,TSource Item)> IndexIterator<TSource>(IEnumerable<TSource> source) {
        var result = new List<(int Index,TSource Item)>();
        var index = 0;
        foreach(var item in source) {
            result.Add((index,item));
            checked {
                index++;
            }
        }
        return result;
    }

    private static IEnumerable<TSource> IntersectByIterator<TSource, TKey>(IEnumerable<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TSource>();
        var set = new HashSet<TKey>(second,comparer);
        foreach(var item in first) {
            if(set.Remove(keySelector(item))) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> IntersectIterator<TSource>(IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        var result = new List<TSource>();
        var set = new HashSet<TSource>(second,comparer);
        foreach(var item in first) {
            if(set.Remove(item)) {
                result.Add(item);
            }
        }
        return result;
    }


    private static IEnumerable<TResult> LeftJoinIterator<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner?,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TResult>();
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
        var result = new List<TResult>();
        foreach(var item in source) {
            if(item is TResult castedResult) {
                result.Add(castedResult);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> PrependIterator<TSource>(IEnumerable<TSource> source,TSource element) {
        var result = new List<TSource>();
        result.Add(element);
        foreach(var item in source) {
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<int> RangeIterator(int start,int count) {
        var result = new List<int>();
        for(var index = 0; index<count; index++) {
            result.Add(start+index);
        }
        return result;
    }

    private static IEnumerable<TResult> RepeatIterator<TResult>(TResult element,int count) {
        var result = new List<TResult>();
        for(var index = 0; index<count; index++) {
            result.Add(element);
        }
        return result;
    }

    private static IEnumerable<TSource> ReverseIterator<TSource>(IEnumerable<TSource> source) {
        var result = new List<TSource>();
        var list = ToList(source);
        for(var index = list.Count-1; index>=0; index--) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TSource> ReverseIterator<TSource>(TSource[] source) {
        var result = new List<TSource>();
        var list = ToList(source);
        for(var index = list.Count-1; index>=0; index--) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TResult> RightJoinIterator<TOuter, TInner, TKey, TResult>(IEnumerable<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TResult>();
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

    private static IEnumerable<TResult> SelectManyIterator<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TResult>> selector) {
        var result = new List<TResult>();
        foreach(var item in source) {
            foreach(var projectedItem in selector(item)) {
                result.Add(projectedItem);
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,int,IEnumerable<TResult>> selector) {
        var result = new List<TResult>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            foreach(var projectedItem in selector(item,index)) {
                result.Add(projectedItem);
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TCollection, TResult>(IEnumerable<TSource> source,Func<TSource,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        var result = new List<TResult>();
        foreach(var item in source) {
            foreach(var collectionItem in collectionSelector(item)) {
                result.Add(resultSelector(item,collectionItem));
            }
        }
        return result;
    }

    private static IEnumerable<TResult> SelectManyIterator<TSource, TCollection, TResult>(IEnumerable<TSource> source,Func<TSource,int,IEnumerable<TCollection>> collectionSelector,Func<TSource,TCollection,TResult> resultSelector) {
        var result = new List<TResult>();
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

    private static IEnumerable<TResult> SelectIterator<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,int,TResult> selector) {
        var result = new List<TResult>();
        var index = -1;
        foreach(var item in source) {
            checked {
                index++;
            }
            result.Add(selector(item,index));
        }
        return result;
    }


    private static IEnumerable<T> SequenceIterator<T>(T start,T endInclusive,T step) where T : INumber<T> {
        var result = new List<T>();
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

    private static IEnumerable<TSource> ShuffleIterator<TSource>(IEnumerable<TSource> source) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> SkipLastIterator<TSource>(IEnumerable<TSource> source,int count) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> SkipWhileIterator<TSource>(IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> SkipWhileIterator<TSource>(IEnumerable<TSource> source,Func<TSource,int,bool> predicate) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> SkipIterator<TSource>(IEnumerable<TSource> source,int count) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> TakeLastIterator<TSource>(IEnumerable<TSource> source,int count) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> TakeWhileIterator<TSource>(IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        var result = new List<TSource>();
        foreach(var item in source) {
            if(!predicate(item)) {
                return result;
            }
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<TSource> TakeWhileIterator<TSource>(IEnumerable<TSource> source,Func<TSource,int,bool> predicate) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> TakeIterator<TSource>(IEnumerable<TSource> source,int count) {
        var result = new List<TSource>();
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

    private static IEnumerable<TSource> TakeIterator<TSource>(IEnumerable<TSource> source,Range range) {
        var result = new List<TSource>();
        var list = ToList(source);
        var (offset,length)=range.GetOffsetAndLength(list.Count);
        for(var index = offset; index<offset+length; index++) {
            result.Add(list[index]);
        }
        return result;
    }

    private static IEnumerable<TSource> UnionByIterator<TSource, TKey>(IEnumerable<TSource> first,IEnumerable<TSource> second,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer) {
        var result = new List<TSource>();
        var seen = new HashSet<TKey>(comparer);
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

    private static IEnumerable<TSource> UnionIterator<TSource>(IEnumerable<TSource> first,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer) {
        var result = new List<TSource>();
        var seen = new HashSet<TSource>(comparer);
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

    private static IEnumerable<TSource> WhereIterator<TSource>(IEnumerable<TSource> source,Func<TSource,bool> predicate) {
        var result = new List<TSource>();
        foreach(var item in source) {
            if(predicate(item)) {
                result.Add(item);
            }
        }
        return result;
    }

    private static IEnumerable<TSource> WhereIterator<TSource>(IEnumerable<TSource> source,Func<TSource,int,bool> predicate) {
        var result = new List<TSource>();
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

    private static IEnumerable<(TFirst First,TSecond Second)> ZipIterator<TFirst, TSecond>(IEnumerable<TFirst> first,IEnumerable<TSecond> second) {
        var result = new List<(TFirst First,TSecond Second)>();
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        while(firstEnumerator.MoveNext()&&secondEnumerator.MoveNext()) {
            result.Add((firstEnumerator.Current,secondEnumerator.Current));
        }
        return result;
    }

    private static IEnumerable<(TFirst First,TSecond Second,TThird Third)> ZipIterator<TFirst, TSecond, TThird>(IEnumerable<TFirst> first,IEnumerable<TSecond> second,IEnumerable<TThird> third) {
        var result = new List<(TFirst First,TSecond Second,TThird Third)>();
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        using var thirdEnumerator = third.GetEnumerator();
        while(firstEnumerator.MoveNext()&&secondEnumerator.MoveNext()&&thirdEnumerator.MoveNext()) {
            result.Add((firstEnumerator.Current,secondEnumerator.Current,thirdEnumerator.Current));
        }
        return result;
    }

    private static IEnumerable<TResult> ZipIterator<TFirst, TSecond, TResult>(IEnumerable<TFirst> first,IEnumerable<TSecond> second,Func<TFirst,TSecond,TResult> resultSelector) {
        var result = new List<TResult>();
        using var firstEnumerator = first.GetEnumerator();
        using var secondEnumerator = second.GetEnumerator();
        while(firstEnumerator.MoveNext()&&secondEnumerator.MoveNext()) {
            result.Add(resultSelector(firstEnumerator.Current,secondEnumerator.Current));
        }
        return result;
    }
    private static IEnumerable<TResult> Project<TSource, TResult>(IEnumerable<TSource> source,Func<TSource,TResult> selector) {
        var result = new List<TResult>();
        foreach(var item in source) {
            result.Add(selector(item));
        }
        return result;
    }

    private static T Extremum<T>(IEnumerable<T> source,IComparer<T> comparer,bool findMax) {
        ArgumentNullException.ThrowIfNull(source);
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
        ArgumentNullException.ThrowIfNull(source);
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
        ArgumentNullException.ThrowIfNull(source);
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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
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

    private static System.Linq.IOrderedEnumerable<TSource> CreateOrdered<TSource, TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector,IComparer<TKey>? comparer,bool descending) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
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

    private sealed class GroupingImpl<TKey, TElement>:System.Linq.IGrouping<TKey,TElement> {
        private readonly List<TElement> elements = new();

        public GroupingImpl(TKey key) {
            Key=key;
        }

        public TKey Key { get; }

        public void Add(TElement element) {
            elements.Add(element);
        }

        public IEnumerator<TElement> GetEnumerator() {
            return elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    private sealed class LookupImpl<TKey, TElement>:System.Linq.ILookup<TKey,TElement> {
        private readonly IEqualityComparer<TKey> comparer;

        public LookupImpl(IEqualityComparer<TKey>? comparer) {
            this.comparer=comparer??EqualityComparer<TKey>.Default;
            Groups=new List<GroupingImpl<TKey,TElement>>();
        }

        public List<GroupingImpl<TKey,TElement>> Groups { get; }

        public int Count => Groups.Count;

        public IEnumerable<TElement> this[TKey key] {
            get {
                var group = Find(key);
                return group is null ? Empty<TElement>() : group;
            }
        }

        public bool Contains(TKey key) {
            return Find(key) is not null;
        }

        public void Add(TKey key,TElement element) {
            var group = Find(key);
            if(group is null) {
                group=new GroupingImpl<TKey,TElement>(key);
                Groups.Add(group);
            }
            group.Add(element);
        }

        public IEnumerator<System.Linq.IGrouping<TKey,TElement>> GetEnumerator() {
            var result = new List<System.Linq.IGrouping<TKey,TElement>>();
            foreach(var group in Groups) {
                result.Add(group);
            }
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private GroupingImpl<TKey,TElement>? Find(TKey key) {
            foreach(var group in Groups) {
                if(comparer.Equals(group.Key,key)) {
                    return group;
                }
            }
            return null;
        }
    }

    private sealed class InfiniteSequenceEnumerable<T>:IEnumerable<T>
        where T : IAdditionOperators<T,T,T> {
        private readonly T start;
        private readonly T step;

        public InfiniteSequenceEnumerable(T start,T step) {
            this.start=start;
            this.step=step;
        }

        public IEnumerator<T> GetEnumerator() {
            return new InfiniteSequenceEnumerator<T>(start,step);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    private sealed class InfiniteSequenceEnumerator<T>:IEnumerator<T>
        where T : IAdditionOperators<T,T,T> {
        private readonly T start;
        private readonly T step;
        private bool started;
        private T current = default!;

        public InfiniteSequenceEnumerator(T start,T step) {
            this.start=start;
            this.step=step;
        }

        public T Current => current;

        object IEnumerator.Current => Current;

        public bool MoveNext() {
            if(!started) {
                current=start;
                started=true;
            }
            else {
                current+=step;
            }
            return true;
        }

        public void Reset() {
            started=false;
            current=default!;
        }

        public void Dispose() {
        }
    }

    private sealed class OrderedEnumerableImpl<T>:System.Linq.IOrderedEnumerable<T> {
        private readonly IEnumerable<T> source;
        private readonly List<Comparison<T>> comparisons;

        public OrderedEnumerableImpl(IEnumerable<T> source) {
            this.source=source;
            comparisons=new List<Comparison<T>>();
        }

        private OrderedEnumerableImpl(IEnumerable<T> source,List<Comparison<T>> comparisons) {
            this.source=source;
            this.comparisons=comparisons;
        }

        public System.Linq.IOrderedEnumerable<T> CreateOrderedEnumerable<TKey>(Func<T,TKey> keySelector,IComparer<TKey>? comparer,bool descending) {
            ArgumentNullException.ThrowIfNull(keySelector);
            comparer??=Comparer<TKey>.Default;
            var next = new List<Comparison<T>>(comparisons);
            next.Add((left,right) => {
                var result = comparer.Compare(keySelector(left),keySelector(right));
                return descending ? Negate(result) : result;
            });
            return new OrderedEnumerableImpl<T>(source,next);
        }

        public IEnumerator<T> GetEnumerator() {
            var items = new List<OrderedItem<T>>();
            var index = 0;
            foreach(var item in source) {
                items.Add(new OrderedItem<T>(item,index));
                checked {
                    index++;
                }
            }
            items.Sort(Compare);
            var result = new List<T>();
            foreach(var item in items) {
                result.Add(item.Value);
            }
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private int Compare(OrderedItem<T> left,OrderedItem<T> right) {
            foreach(var comparison in comparisons) {
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

    private readonly struct OrderedItem<T> {
        public OrderedItem(T value,int index) {
            Value=value;
            Index=index;
        }

        public T Value { get; }
        public int Index { get; }
    }
}
