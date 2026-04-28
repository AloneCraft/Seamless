namespace Seamless.Linq;

public static partial class HashSet {

    /// <summary>Applies an accumulator function over a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to aggregate over.</param>
    /// <param name="func">An accumulator function to be invoked on each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="func" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The final accumulator value.</returns>
    public static TSource Aggregate<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TSource,TSource> func)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Aggregate(source,func);
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
    public static TAccumulate Aggregate<TSource, TAccumulate>(this global::Seamless.Collections.IHashSet<TSource> source,TAccumulate seed,global::System.Func<TAccumulate,TSource,TAccumulate> func)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Aggregate(source,seed,func);
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
    public static TResult Aggregate<TSource, TAccumulate, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,TAccumulate seed,global::System.Func<TAccumulate,TSource,TAccumulate> func,global::System.Func<TAccumulate,TResult> resultSelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Aggregate(source,seed,func,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,TAccumulate seed,global::System.Func<TAccumulate,TSource,TAccumulate> func,global::System.Collections.Generic.IEqualityComparer<TKey>? keyComparer = null)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
        return Enumerable.AggregateBy(source,keySelector,seed,func,keyComparer);
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
    public static global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<TKey,TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TKey,TAccumulate> seedSelector,global::System.Func<TAccumulate,TSource,TAccumulate> func,global::System.Collections.Generic.IEqualityComparer<TKey>? keyComparer = null)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
        return Enumerable.AggregateBy(source,keySelector,seedSelector,func,keyComparer);
    }

    /// <summary>Determines whether all elements of a sequence satisfy a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, <see langword="false" />.</returns>
    public static bool All<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.All(source,predicate);
    }

    /// <summary>Determines whether a sequence contains any elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to check for emptiness.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains any elements; otherwise, <see langword="false" />.</returns>
    public static bool Any<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Any(source);
    }

    /// <summary>Determines whether any element of a sequence satisfies a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to apply the predicate to.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence is not empty and at least one of its elements passes the test in the specified predicate; otherwise, <see langword="false" />.</returns>
    public static bool Any<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Any(source,predicate);
    }

    /// <summary>Appends a value to the end of the sequence.</summary>
    /// <param name="source">A sequence of values.</param>
    /// <param name="element">The value to append to <paramref name="source" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new sequence that ends with <paramref name="element" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Append<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource element)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Append(source,element);
    }

    /// <summary>Returns the input typed as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">The sequence to type as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>The input sequence typed as <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> AsEnumerable<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.AsEnumerable(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static decimal Average(this global::Seamless.Collections.IHashSet<decimal> source) {
        return Enumerable.Average(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this global::Seamless.Collections.IHashSet<double> source) {
        return Enumerable.Average(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this global::Seamless.Collections.IHashSet<int> source) {
        return Enumerable.Average(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static double Average(this global::Seamless.Collections.IHashSet<long> source) {
        return Enumerable.Average(source);
    }

    /// <summary>Computes the average of a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to calculate the average of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The average of the sequence of values.</returns>
    public static float Average(this global::Seamless.Collections.IHashSet<float> source) {
        return Enumerable.Average(source);
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
    public static decimal Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
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
    public static double Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
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
    public static double Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
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
    public static double Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static decimal? Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum of the elements in the sequence is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static double? Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
    }

    /// <summary>Computes the average of a sequence of nullable <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values to calculate the average of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The average of the sequence of values, or <see langword="null" /> if the source sequence is empty or contains only values that are <see langword="null" />.</returns>
    public static float? Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
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
    public static float Average<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Average(source,selector);
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
    public static global::System.Collections.Generic.IEnumerable<TSource[]> Chunk<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int size)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Chunk(source,size);
    }

    /// <summary>Concatenates two sequences.</summary>
    /// <param name="first">The first sequence to concatenate.</param>
    /// <param name="second">The sequence to concatenate to the first sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the concatenated elements of the two input sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Concat<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Concat(first,second);
    }

    /// <summary>Determines whether a sequence contains a specified element by using the default equality comparer.</summary>
    /// <param name="source">A sequence in which to locate a value.</param>
    /// <param name="value">The value to locate in the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the source sequence contains an element that has the specified value; otherwise, <see langword="false" />.</returns>
    public static bool Contains<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource value)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Contains(source,value);
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
    public static bool Contains<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource value,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Contains(source,value,comparer);
    }

    /// <summary>Returns the number of elements in a sequence.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements in <paramref name="source" /> is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The number of elements in the input sequence.</returns>
    public static int Count<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Count(source);
    }

    /// <summary>Returns a number that represents how many elements in the specified sequence satisfy a condition.</summary>
    /// <param name="source">A sequence that contains elements to be tested and counted.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements in <paramref name="source" /> is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>A number that represents how many elements in the sequence satisfy the condition in the predicate function.</returns>
    public static int Count<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Count(source,predicate);
    }

    /// <summary>Returns the count of elements in the source sequence grouped by key.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="keyComparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys with.</param>
    /// <typeparam name="TSource">The type of elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <returns>An enumerable containing the frequencies of each key occurrence in <paramref name="source" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<TKey,int>> CountBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? keyComparer = null)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
        return Enumerable.CountBy(source,keySelector,keyComparer);
    }

    /// <summary>Returns the elements of the specified sequence or the type parameter's default value in a singleton collection if the sequence is empty.</summary>
    /// <param name="source">The sequence to return a default value for if it is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> object that contains the default value for the <typeparamref name="TSource" /> type if <paramref name="source" /> is empty; otherwise, <paramref name="source" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource?> DefaultIfEmpty<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.DefaultIfEmpty(source);
    }

    /// <summary>Returns the elements of the specified sequence or the specified value in a singleton collection if the sequence is empty.</summary>
    /// <param name="source">The sequence to return the specified value for if it is empty.</param>
    /// <param name="defaultValue">The value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains <paramref name="defaultValue" /> if <paramref name="source" /> is empty; otherwise, <paramref name="source" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> DefaultIfEmpty<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.DefaultIfEmpty(source,defaultValue);
    }

    /// <summary>Returns distinct elements from a sequence according to a specified key selector function.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of key to distinguish elements by.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> DistinctBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.DistinctBy(source,keySelector);
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
    public static global::System.Collections.Generic.IEnumerable<TSource> DistinctBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.DistinctBy(source,keySelector,comparer);
    }

    /// <summary>Returns distinct elements from a sequence by using the default equality comparer to compare values.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Distinct<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Distinct(source);
    }

    /// <summary>Returns distinct elements from a sequence by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="source">The sequence to remove duplicate elements from.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains distinct elements from the source sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Distinct<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Distinct(source,comparer);
    }

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The index of the element to retrieve, which is either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" /> if <paramref name="index" /> is outside the bounds of the <paramref name="source" /> sequence; otherwise, the element at the specified position in the <paramref name="source" /> sequence.</returns>
    public static TSource? ElementAtOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Index index)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ElementAtOrDefault(source,index);
    }

    /// <summary>Returns the element at a specified index in a sequence or a default value if the index is out of range.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the index is outside the bounds of the source sequence; otherwise, the element at the specified position in the source sequence.</returns>
    public static TSource? ElementAtOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int index)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ElementAtOrDefault(source,index);
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
    public static TSource ElementAt<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Index index)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ElementAt(source,index);
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
    public static TSource ElementAt<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int index)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ElementAt(source,index);
    }

    /// <summary>Produces the set difference of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> ExceptBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey> {
        return Enumerable.ExceptBy(first,second,keySelector);
    }

    /// <summary>Produces the set difference of two sequences according to a specified key selector function.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose keys that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequence.</typeparam>
    /// <typeparam name="TKey">The type of key to identify elements by.</typeparam>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> ExceptBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey> {
        return Enumerable.ExceptBy(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set difference of two sequences by using the default equality comparer to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Except<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Except(first,second);
    }

    /// <summary>Produces the set difference of two sequences by using the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that are not also in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements that also occur in the first sequence will cause those elements to be removed from the returned sequence.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the set difference of the elements of two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Except<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Except(first,second,comparer);
    }

    /// <summary>Returns the first element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if <paramref name="source" /> is empty; otherwise, the first element in <paramref name="source" />.</returns>
    public static TSource? FirstOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.FirstOrDefault(source);
    }

    /// <summary>Returns the first element of a sequence, or a specified default value if the sequence contains no elements.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if <paramref name="source" /> is empty; otherwise, the first element in <paramref name="source" />.</returns>
    public static TSource FirstOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.FirstOrDefault(source,defaultValue);
    }

    /// <summary>Returns the first element of the sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if <paramref name="source" /> is empty or if no element passes the test specified by <paramref name="predicate" />; otherwise, the first element in <paramref name="source" /> that passes the test specified by <paramref name="predicate" />.</returns>
    public static TSource? FirstOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.FirstOrDefault(source,predicate);
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
    public static TSource FirstOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.FirstOrDefault(source,predicate,defaultValue);
    }

    /// <summary>Returns the first element of a sequence.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the first element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The source sequence is empty.</exception>
    /// <returns>The first element in the specified sequence.</returns>
    public static TSource First<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.First(source);
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
    public static TSource First<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.First(source,predicate);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TSource&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TSource))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a sequence of objects and a key.</returns>
    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and compares the keys by using a specified comparer.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TSource&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TSource))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a collection of objects and a key.</returns>
    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,comparer);
    }

    /// <summary>Groups the elements of a sequence according to a specified key selector function and projects the elements for each group by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in the <see cref="T:System.Linq.IGrouping`2" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the <see cref="T:System.Linq.IGrouping`2" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TElement&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TElement))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a collection of objects of type <typeparamref name="TElement" /> and a key.</returns>
    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,elementSelector);
    }

    /// <summary>Groups the elements of a sequence according to a key selector function. The keys are compared by using a comparer and each group's elements are projected by using a specified function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements to group.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to map each source element to an element in an <see cref="T:System.Linq.IGrouping`2" />.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the <see cref="T:System.Linq.IGrouping`2" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <c>IEnumerable&lt;IGrouping&lt;TKey, TElement&gt;&gt;</c> in C# or <c>IEnumerable(Of IGrouping(Of TKey, TElement))</c> in Visual Basic where each <see cref="T:System.Linq.IGrouping`2" /> object contains a collection of objects of type <typeparamref name="TElement" /> and a key.</returns>
    public static global::System.Collections.Generic.IEnumerable<global::System.Linq.IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,elementSelector,comparer);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TKey,global::System.Collections.Generic.IEnumerable<TSource>,TResult> resultSelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> GroupBy<TSource, TKey, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TKey,global::System.Collections.Generic.IEnumerable<TSource>,TResult> resultSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,resultSelector,comparer);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector,global::System.Func<TKey,global::System.Collections.Generic.IEnumerable<TElement>,TResult> resultSelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,elementSelector,resultSelector);
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>A collection of elements of type <typeparamref name="TResult" /> where each element represents a projection over a group and its key.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector,global::System.Func<TKey,global::System.Collections.Generic.IEnumerable<TElement>,TResult> resultSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.GroupBy(source,keySelector,elementSelector,resultSelector,comparer);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter,global::System.Collections.Generic.IEnumerable<TInner>,TResult> resultSelector)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.GroupJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter,global::System.Collections.Generic.IEnumerable<TInner>,TResult> resultSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.GroupJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns an enumerable that incorporates the element's index into a tuple.</summary>
    /// <param name="source">The source enumerable providing the elements.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An enumerable that incorporates each element index into a tuple.</returns>
    public static global::System.Collections.Generic.IEnumerable<(int Index,TSource Item)> Index<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Index(source);
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
    public static global::System.Collections.Generic.IEnumerable<TSource> IntersectBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey> {
        return Enumerable.IntersectBy(first,second,keySelector);
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
    public static global::System.Collections.Generic.IEnumerable<TSource> IntersectBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TKey> second,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : global::System.IEquatable<TKey> {
        return Enumerable.IntersectBy(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set intersection of two sequences by using the default equality comparer to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Intersect<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Intersect(first,second);
    }

    /// <summary>Produces the set intersection of two sequences by using the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in <paramref name="second" /> will be returned.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements that also appear in the first sequence will be returned.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>A sequence that contains the elements that form the set intersection of two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Intersect<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Intersect(first,second,comparer);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter,TInner,TResult> resultSelector)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.Join(outer,inner,outerKeySelector,innerKeySelector,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter,TInner,TResult> resultSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.Join(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns the last element of a sequence, or a default value if the sequence contains no elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the source sequence is empty; otherwise, the last element in the <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static TSource? LastOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.LastOrDefault(source);
    }

    /// <summary>Returns the last element of a sequence, or a specified default value if the sequence contains no elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <paramref name="defaultValue" /> if the source sequence is empty; otherwise, the last element in the <see cref="T:System.Collections.Generic.IEnumerable`1" />.</returns>
    public static TSource LastOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.LastOrDefault(source,defaultValue);
    }

    /// <summary>Returns the last element of a sequence that satisfies a condition or a default value if no such element is found.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return an element from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="default" />(<typeparamref name="TSource" />) if the sequence is empty or if no elements pass the test in the predicate function; otherwise, the last element that passes the test in the predicate function.</returns>
    public static TSource? LastOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.LastOrDefault(source,predicate);
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
    public static TSource LastOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.LastOrDefault(source,predicate,defaultValue);
    }

    /// <summary>Returns the last element of a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the last element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The source sequence is empty.</exception>
    /// <returns>The value at the last position in the source sequence.</returns>
    public static TSource Last<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Last(source);
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
    public static TSource Last<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="outer" /> or <paramref name="inner" /> or <paramref name="outerKeySelector" /> or <paramref name="innerKeySelector" /> or <paramref name="resultSelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that has elements of type <typeparamref name="TResult" /> that are obtained by performing a left outer join on two sequences.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter,TInner?,TResult> resultSelector)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.LeftJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter,TInner?,TResult> resultSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.LeftJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Returns an <see cref="T:System.Int64" /> that represents the total number of elements in a sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to be counted.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of elements exceeds <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The number of elements in the source sequence.</returns>
    public static long LongCount<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.LongCount(source);
    }

    /// <summary>Returns an <see cref="T:System.Int64" /> that represents how many elements in a sequence satisfy a condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements to be counted.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The number of matching elements exceeds <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>A number that represents how many elements in the sequence satisfy the condition in the predicate function.</returns>
    public static long LongCount<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.LongCount(source,predicate);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static decimal Max(this global::Seamless.Collections.IHashSet<decimal> source) {
        return Enumerable.Max(source);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static double Max(this global::Seamless.Collections.IHashSet<double> source) {
        return Enumerable.Max(source);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static int Max(this global::Seamless.Collections.IHashSet<int> source) {
        return Enumerable.Max(source);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static long Max(this global::Seamless.Collections.IHashSet<long> source) {
        return Enumerable.Max(source);
    }

    /// <summary>Returns the maximum value in a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to determine the maximum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static float Max(this global::Seamless.Collections.IHashSet<float> source) {
        return Enumerable.Max(source);
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
    public static TSource? MaxBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.MaxBy(source,keySelector);
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
    public static TSource? MaxBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.MaxBy(source,keySelector,comparer);
    }

    /// <summary>Returns the maximum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TSource? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source);
    }

    /// <summary>Returns the maximum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TSource? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,comparer);
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
    public static decimal Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
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
    public static double Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
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
    public static int Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
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
    public static long Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Decimal&gt;</c> in C# or <c>Nullable(Of Decimal)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static decimal? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Double&gt;</c> in C# or <c>Nullable(Of Double)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static double? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value of type <c>Nullable&lt;Int32&gt;</c> in C# or <c>Nullable(Of Int32)</c> in Visual Basic that corresponds to the maximum value in the sequence.</returns>
    public static int? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the maximum value in the sequence.</returns>
    public static long? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the maximum nullable <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the maximum value in the sequence.</returns>
    public static float? Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
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
    public static float Max<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a generic sequence and returns the maximum resulting value.</summary>
    /// <param name="source">A sequence of values to determine the maximum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The maximum value in the sequence.</returns>
    public static TResult? Max<TSource, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TResult> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Max(source,selector);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static decimal Min(this global::Seamless.Collections.IHashSet<decimal> source) {
        return Enumerable.Min(source);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static double Min(this global::Seamless.Collections.IHashSet<double> source) {
        return Enumerable.Min(source);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static int Min(this global::Seamless.Collections.IHashSet<int> source) {
        return Enumerable.Min(source);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static long Min(this global::Seamless.Collections.IHashSet<long> source) {
        return Enumerable.Min(source);
    }

    /// <summary>Returns the minimum value in a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to determine the minimum value of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    ///   <paramref name="source" /> contains no elements.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static float Min(this global::Seamless.Collections.IHashSet<float> source) {
        return Enumerable.Min(source);
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
    public static TSource? MinBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.MinBy(source,keySelector);
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
    public static TSource? MinBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.MinBy(source,keySelector,comparer);
    }

    /// <summary>Returns the minimum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TSource? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source);
    }

    /// <summary>Returns the minimum value in a generic sequence.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">No object in <paramref name="source" /> implements the <see cref="T:System.IComparable" /> or <see cref="T:System.IComparable`1" /> interface.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TSource? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,comparer);
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
    public static decimal Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
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
    public static double Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
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
    public static int Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
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
    public static long Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Decimal" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static decimal? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Double" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static double? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Int32" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static int? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Int64" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static long? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a sequence and returns the minimum nullable <see cref="T:System.Single" /> value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The value that corresponds to the minimum value in the sequence.</returns>
    public static float? Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
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
    public static float Min<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Invokes a transform function on each element of a generic sequence and returns the minimum resulting value.</summary>
    /// <param name="source">A sequence of values to determine the minimum value of.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The minimum value in the sequence.</returns>
    public static TResult? Min<TSource, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TResult> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Min(source,selector);
    }

    /// <summary>Sorts the elements of a sequence in descending order according to a key.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static global::System.Linq.IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.OrderByDescending(source,keySelector);
    }

    /// <summary>Sorts the elements of a sequence in descending order by using a specified comparer.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted in descending order according to a key.</returns>
    public static global::System.Linq.IOrderedEnumerable<TSource> OrderByDescending<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.OrderByDescending(source,keySelector,comparer);
    }

    /// <summary>Sorts the elements of a sequence in ascending order according to a key.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static global::System.Linq.IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.OrderBy(source,keySelector);
    }

    /// <summary>Sorts the elements of a sequence in ascending order by using a specified comparer.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted according to a key.</returns>
    public static global::System.Linq.IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.OrderBy(source,keySelector,comparer);
    }

    /// <summary>Sorts the elements of a sequence in descending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static global::System.Linq.IOrderedEnumerable<T> OrderDescending<T>(this global::Seamless.Collections.IHashSet<T> source)
        where T : global::System.IEquatable<T> {
        return Enumerable.OrderDescending(source);
    }

    /// <summary>Sorts the elements of a sequence in descending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static global::System.Linq.IOrderedEnumerable<T> OrderDescending<T>(this global::Seamless.Collections.IHashSet<T> source,global::System.Collections.Generic.IComparer<T>? comparer)
        where T : global::System.IEquatable<T> {
        return Enumerable.OrderDescending(source,comparer);
    }

    /// <summary>Sorts the elements of a sequence in ascending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static global::System.Linq.IOrderedEnumerable<T> Order<T>(this global::Seamless.Collections.IHashSet<T> source)
        where T : global::System.IEquatable<T> {
        return Enumerable.Order(source);
    }

    /// <summary>Sorts the elements of a sequence in ascending order.</summary>
    /// <param name="source">A sequence of values to order.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IComparer`1" /> to compare keys.</param>
    /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Linq.IOrderedEnumerable`1" /> whose elements are sorted.</returns>
    public static global::System.Linq.IOrderedEnumerable<T> Order<T>(this global::Seamless.Collections.IHashSet<T> source,global::System.Collections.Generic.IComparer<T>? comparer)
        where T : global::System.IEquatable<T> {
        return Enumerable.Order(source,comparer);
    }

    /// <summary>Adds a value to the beginning of the sequence.</summary>
    /// <param name="source">A sequence of values.</param>
    /// <param name="element">The value to prepend to <paramref name="source" />.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new sequence that begins with <paramref name="element" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Prepend<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource element)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Prepend(source,element);
    }

    /// <summary>Inverts the order of the elements in a sequence.</summary>
    /// <param name="source">A sequence of values to reverse.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A sequence whose elements correspond to those of the input sequence in reverse order.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Reverse<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Reverse(source);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter?,TInner,TResult> resultSelector)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.RightJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this global::Seamless.Collections.IHashSet<TOuter> outer,global::Seamless.Collections.IHashSet<TInner> inner,global::System.Func<TOuter,TKey> outerKeySelector,global::System.Func<TInner,TKey> innerKeySelector,global::System.Func<TOuter?,TInner,TResult> resultSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TOuter : global::System.IEquatable<TOuter>
        where TInner : global::System.IEquatable<TInner> {
        return Enumerable.RightJoin(outer,inner,outerKeySelector,innerKeySelector,resultSelector,comparer);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" /> and flattens the resulting sequences into one sequence.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function on each element of the input sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> SelectMany<TSource, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,global::System.Collections.Generic.IEnumerable<TResult>> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SelectMany(source,selector);
    }

    /// <summary>Projects each element of a sequence to an <see cref="T:System.Collections.Generic.IEnumerable`1" />, and flattens the resulting sequences into one sequence. The index of each source element is used in the projected form of that element.</summary>
    /// <param name="source">A sequence of values to project.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the elements of the sequence returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the one-to-many transform function on each element of an input sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> SelectMany<TSource, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int,global::System.Collections.Generic.IEnumerable<TResult>> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SelectMany(source,selector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,global::System.Collections.Generic.IEnumerable<TCollection>> collectionSelector,global::System.Func<TSource,TCollection,TResult> resultSelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SelectMany(source,collectionSelector,resultSelector);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int,global::System.Collections.Generic.IEnumerable<TCollection>> collectionSelector,global::System.Func<TSource,TCollection,TResult> resultSelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SelectMany(source,collectionSelector,resultSelector);
    }

    /// <summary>Projects each element of a sequence into a new form by incorporating the element's index.</summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of <paramref name="source" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> Select<TSource, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int,TResult> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Select(source,selector);
    }

    /// <summary>Projects each element of a sequence into a new form.</summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <paramref name="selector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose elements are the result of invoking the transform function on each element of <paramref name="source" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TResult> Select<TSource, TResult>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TResult> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Select(source,selector);
    }

    /// <summary>Determines whether two sequences are equal by comparing the elements by using the default equality comparer for their type.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to <paramref name="second" />.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to compare to the first sequence.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>
    ///   <see langword="true" /> if the two source sequences are of equal length and their corresponding elements are equal according to the default equality comparer for their type; otherwise, <see langword="false" />.</returns>
    public static bool SequenceEqual<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SequenceEqual(first,second);
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
    public static bool SequenceEqual<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SequenceEqual(first,second,comparer);
    }

    /// <summary>Shuffles the order of the elements of a sequence.</summary>
    /// <param name="source">A sequence of values to shuffle.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>A sequence whose elements correspond to those of the input sequence in randomized order.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Shuffle<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Shuffle(source);
    }

    /// <summary>Returns the only element of a sequence, or a default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.</exception>
    /// <returns>The single element of the input sequence, or <see langword="default" />(<typeparamref name="TSource" />) if the sequence contains no elements.</returns>
    public static TSource? SingleOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SingleOrDefault(source);
    }

    /// <summary>Returns the only element of a sequence, or a specified default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return the single element of.</param>
    /// <param name="defaultValue">The default value to return if the sequence is empty.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">The input sequence contains more than one element.</exception>
    /// <returns>The single element of the input sequence, or <paramref name="defaultValue" /> if the sequence contains no elements.</returns>
    public static TSource SingleOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SingleOrDefault(source,defaultValue);
    }

    /// <summary>Returns the only element of a sequence that satisfies a specified condition or a default value if no such element exists; this method throws an exception if more than one element satisfies the condition.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return a single element from.</param>
    /// <param name="predicate">A function to test an element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">More than one element satisfies the condition in <paramref name="predicate" />.</exception>
    /// <returns>The single element of the input sequence that satisfies the condition, or <see langword="default" />(<typeparamref name="TSource" />) if no such element is found.</returns>
    public static TSource? SingleOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SingleOrDefault(source,predicate);
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
    public static TSource SingleOrDefault<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate,TSource defaultValue)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SingleOrDefault(source,predicate,defaultValue);
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
    public static TSource Single<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Single(source);
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
    public static TSource Single<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Single(source,predicate);
    }

    /// <summary>Returns a new enumerable collection that contains the elements from <paramref name="source" /> with the last <paramref name="count" /> elements of the source collection omitted.</summary>
    /// <param name="source">An enumerable collection instance.</param>
    /// <param name="count">The number of elements to omit from the end of the collection.</param>
    /// <typeparam name="TSource">The type of the elements in the enumerable collection.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new enumerable collection that contains the elements from <paramref name="source" /> minus <paramref name="count" /> elements from the end of the collection.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> SkipLast<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int count)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SkipLast(source,count);
    }

    /// <summary>Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by <paramref name="predicate" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> SkipWhile<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SkipWhile(source,predicate);
    }

    /// <summary>Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements. The element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by <paramref name="predicate" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> SkipWhile<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.SkipWhile(source,predicate);
    }

    /// <summary>Bypasses a specified number of elements in a sequence and then returns the remaining elements.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to return elements from.</param>
    /// <param name="count">The number of elements to skip before returning the remaining elements.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements that occur after the specified index in the input sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Skip<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int count)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Skip(source,count);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Decimal" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Decimal" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static decimal Sum(this global::Seamless.Collections.IHashSet<decimal> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Double" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Double" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static double Sum(this global::Seamless.Collections.IHashSet<double> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Int32" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int32" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static int Sum(this global::Seamless.Collections.IHashSet<int> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Int64" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Int64" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static long Sum(this global::Seamless.Collections.IHashSet<long> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of a sequence of <see cref="T:System.Single" /> values.</summary>
    /// <param name="source">A sequence of <see cref="T:System.Single" /> values to calculate the sum of.</param>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the values in the sequence.</returns>
    public static float Sum(this global::Seamless.Collections.IHashSet<float> source) {
        return Enumerable.Sum(source);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static decimal Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static double Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static int Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static long Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Decimal" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Decimal.MaxValue">Decimal.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static decimal? Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,decimal?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Double" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Double.MaxValue">Double.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static double? Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,double?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Int32" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int32.MaxValue">Int32.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static int? Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Int64" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <exception cref="T:System.OverflowException">The sum is larger than <see cref="F:System.Int64.MaxValue">Int64.MaxValue</see>.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static long? Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,long?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of nullable <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static float? Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float?> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Computes the sum of the sequence of <see cref="T:System.Single" /> values that are obtained by invoking a transform function on each element of the input sequence.</summary>
    /// <param name="source">A sequence of values that are used to calculate a sum.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="selector" /> is <see langword="null" />.</exception>
    /// <returns>The sum of the projected values.</returns>
    public static float Sum<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,float> selector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Sum(source,selector);
    }

    /// <summary>Returns a new enumerable collection that contains the last <paramref name="count" /> elements from <paramref name="source" />.</summary>
    /// <param name="source">An enumerable collection instance.</param>
    /// <param name="count">The number of elements to take from the end of the collection.</param>
    /// <typeparam name="TSource">The type of the elements in the enumerable collection.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A new enumerable collection that contains the last <paramref name="count" /> elements from <paramref name="source" />.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> TakeLast<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int count)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.TakeLast(source,count);
    }

    /// <summary>Returns elements from a sequence as long as a specified condition is true.</summary>
    /// <param name="source">A sequence to return elements from.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from the input sequence that occur before the element at which the test no longer passes.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> TakeWhile<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.TakeWhile(source,predicate);
    }

    /// <summary>Returns elements from a sequence as long as a specified condition is true. The element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that occur before the element at which the test no longer passes.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> TakeWhile<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.TakeWhile(source,predicate);
    }

    /// <summary>Returns a specified number of contiguous elements from the start of a sequence.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="count">The number of elements to return.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the specified number of elements from the start of the input sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Take<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,int count)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Take(source,count);
    }

    /// <summary>Returns a specified range of contiguous elements from a sequence.</summary>
    /// <param name="source">The sequence to return elements from.</param>
    /// <param name="range">The range of elements to return, which has start and end indexes either from the beginning or the end of the sequence.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the specified range of elements from the <paramref name="source" /> sequence.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Take<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Range range)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Take(source,range);
    }

    /// <summary>Creates an array from a <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create an array from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>An array that contains the elements from the input sequence.</returns>
    public static TSource[] ToArray<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToArray(source);
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
    public static global::System.Collections.Generic.Dictionary<TKey,TSource> ToDictionary<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector);
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
    public static global::System.Collections.Generic.Dictionary<TKey,TSource> ToDictionary<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector,comparer);
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
    public static global::System.Collections.Generic.Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.
    ///
    ///  -or-
    ///
    ///  <paramref name="keySelector" /> produces a key that is <see langword="null" />.</exception>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="keySelector" /> produces duplicate keys for two elements.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.Dictionary`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static global::System.Collections.Generic.Dictionary<TKey,TElement> ToDictionary<TSource, TKey, TElement>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource>
        where TKey : notnull {
        return Enumerable.ToDictionary(source,keySelector,elementSelector,comparer);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.HashSet`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.HashSet`1" /> from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.HashSet`1" /> that contains values of type <typeparamref name="TSource" /> selected from the input sequence.</returns>
    public static global::System.Collections.Generic.HashSet<TSource> ToHashSet<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToHashSet(source);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.HashSet`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> using the <paramref name="comparer" /> to compare keys.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.HashSet`1" /> from.</param>
    /// <param name="comparer">An <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare keys.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.HashSet`1" /> that contains values of type <typeparamref name="TSource" /> selected from the input sequence.</returns>
    public static global::System.Collections.Generic.HashSet<TSource> ToHashSet<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToHashSet(source,comparer);
    }

    /// <summary>Creates a <see cref="T:System.Collections.Generic.List`1" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" />.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Collections.Generic.List`1" /> from.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Collections.Generic.List`1" /> that contains elements from the input sequence.</returns>
    public static global::System.Collections.Generic.List<TSource> ToList<TSource>(this global::Seamless.Collections.IHashSet<TSource> source)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToList(source);
    }

    /// <summary>Creates a <see cref="T:System.Linq.Lookup`2" /> from an <see cref="T:System.Collections.Generic.IEnumerable`1" /> according to a specified key selector function.</summary>
    /// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="T:System.Linq.Lookup`2" /> from.</param>
    /// <param name="keySelector">A function to extract a key from each element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains keys and values. The values within each group are in the same order as in <paramref name="source" />.</returns>
    public static global::System.Linq.ILookup<TKey,TSource> ToLookup<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToLookup(source,keySelector);
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
    public static global::System.Linq.ILookup<TKey,TSource> ToLookup<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToLookup(source,keySelector,comparer);
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
    public static global::System.Linq.ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector)
        where TSource : global::System.IEquatable<TSource> {
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
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="keySelector" /> or <paramref name="elementSelector" /> is <see langword="null" />.</exception>
    /// <returns>A <see cref="T:System.Linq.Lookup`2" /> that contains values of type <typeparamref name="TElement" /> selected from the input sequence.</returns>
    public static global::System.Linq.ILookup<TKey,TElement> ToLookup<TSource, TKey, TElement>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,TKey> keySelector,global::System.Func<TSource,TElement> elementSelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.ToLookup(source,keySelector,elementSelector,comparer);
    }

    /// <summary>Attempts to determine the number of elements in a sequence without forcing an enumeration.</summary>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <param name="count">When this method returns, contains the number of elements in <paramref name="source" />, or 0 if the count couldn't be determined without enumeration.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <returns>
    ///   <see langword="true" /> if the count of <paramref name="source" /> can be determined without enumeration; otherwise, <see langword="false" />.</returns>
    public static bool TryGetNonEnumeratedCount<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,out int count)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.TryGetNonEnumeratedCount(source,out count);
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
    public static global::System.Collections.Generic.IEnumerable<TSource> UnionBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Func<TSource,TKey> keySelector)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.UnionBy(first,second,keySelector);
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
    public static global::System.Collections.Generic.IEnumerable<TSource> UnionBy<TSource, TKey>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Func<TSource,TKey> keySelector,global::System.Collections.Generic.IEqualityComparer<TKey>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.UnionBy(first,second,keySelector,comparer);
    }

    /// <summary>Produces the set union of two sequences by using the default equality comparer.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Union<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Union(first,second);
    }

    /// <summary>Produces the set union of two sequences by using a specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
    /// <param name="first">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the first set for the union.</param>
    /// <param name="second">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> whose distinct elements form the second set for the union.</param>
    /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> to compare values.</param>
    /// <typeparam name="TSource">The type of the elements of the input sequences.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="first" /> or <paramref name="second" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains the elements from both input sequences, excluding duplicates.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Union<TSource>(this global::Seamless.Collections.IHashSet<TSource> first,global::Seamless.Collections.IHashSet<TSource> second,global::System.Collections.Generic.IEqualityComparer<TSource>? comparer)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Union(first,second,comparer);
    }

    /// <summary>Filters a sequence of values based on a predicate.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to filter.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Where<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Where(source,predicate);
    }

    /// <summary>Filters a sequence of values based on a predicate. Each element's index is used in the logic of the predicate function.</summary>
    /// <param name="source">An <see cref="T:System.Collections.Generic.IEnumerable`1" /> to filter.</param>
    /// <param name="predicate">A function to test each source element for a condition; the second parameter of the function represents the index of the source element.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
    /// <exception cref="T:System.ArgumentNullException">
    ///   <paramref name="source" /> or <paramref name="predicate" /> is <see langword="null" />.</exception>
    /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements from the input sequence that satisfy the condition.</returns>
    public static global::System.Collections.Generic.IEnumerable<TSource> Where<TSource>(this global::Seamless.Collections.IHashSet<TSource> source,global::System.Func<TSource,int,bool> predicate)
        where TSource : global::System.IEquatable<TSource> {
        return Enumerable.Where(source,predicate);
    }

    /// <summary>Produces a sequence of tuples with elements from the two specified sequences.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <returns>A sequence of tuples with elements taken from the first and second sequences, in that order.</returns>
    public static global::System.Collections.Generic.IEnumerable<(TFirst First,TSecond Second)> Zip<TFirst, TSecond>(this global::Seamless.Collections.IHashSet<TFirst> first,global::Seamless.Collections.IHashSet<TSecond> second)
        where TFirst : global::System.IEquatable<TFirst>
        where TSecond : global::System.IEquatable<TSecond> {
        return Enumerable.Zip(first,second);
    }

    /// <summary>Produces a sequence of tuples with elements from the three specified sequences.</summary>
    /// <param name="first">The first sequence to merge.</param>
    /// <param name="second">The second sequence to merge.</param>
    /// <param name="third">The third sequence to merge.</param>
    /// <typeparam name="TFirst">The type of the elements of the first input sequence.</typeparam>
    /// <typeparam name="TSecond">The type of the elements of the second input sequence.</typeparam>
    /// <typeparam name="TThird">The type of the elements of the third input sequence.</typeparam>
    /// <returns>A sequence of tuples with elements taken from the first, second, and third sequences, in that order.</returns>
    public static global::System.Collections.Generic.IEnumerable<(TFirst First,TSecond Second,TThird Third)> Zip<TFirst, TSecond, TThird>(this global::Seamless.Collections.IHashSet<TFirst> first,global::Seamless.Collections.IHashSet<TSecond> second,global::Seamless.Collections.IHashSet<TThird> third)
        where TFirst : global::System.IEquatable<TFirst>
        where TSecond : global::System.IEquatable<TSecond>
        where TThird : global::System.IEquatable<TThird> {
        return Enumerable.Zip(first,second,third);
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
    public static global::System.Collections.Generic.IEnumerable<TResult> Zip<TFirst, TSecond, TResult>(this global::Seamless.Collections.IHashSet<TFirst> first,global::Seamless.Collections.IHashSet<TSecond> second,global::System.Func<TFirst,TSecond,TResult> resultSelector)
        where TFirst : global::System.IEquatable<TFirst>
        where TSecond : global::System.IEquatable<TSecond> {
        return Enumerable.Zip(first,second,resultSelector);
    }
}
