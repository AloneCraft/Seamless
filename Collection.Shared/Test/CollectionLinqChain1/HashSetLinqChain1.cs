using BenchmarkDotNet.Attributes;
using Collection.Strategy.CollectionLinq;

namespace Collection.Test.CollectionLinqChain1;

public abstract class HashSetLinqChain1<TStrategy,TSource,TElement,TKey,TValue>
    :CollectionLinqChain1<TStrategy,TSource,TElement,TKey,TValue>
    where TStrategy : ICollectionLinqStrategy<TSource,TElement,TKey,TValue>
    where TSource : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
#if BENCH_AS_TEST
    private const int ElementCount = 128;
#else
    private const int ElementCount = 4096;
#endif

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Distinct() {
        using var source = TStrategy.Create(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Concat(TStrategy.AsEnumerable(source))
            .Distinct()
            .Select(static element => TStrategy.Number(element))
            .ToHashSet();
        AssertNumberRange(numbers,1,ElementCount);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Union() {
        using var source = TStrategy.Create(ElementCount);
        using var second = TStrategy.CreateSecond(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Union(TStrategy.AsEnumerable(second))
            .Select(static element => TStrategy.Number(element))
            .ToHashSet();
        AssertNumberRange(numbers,1,ElementCount+ElementCount/2);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Except() {
        using var source = TStrategy.Create(ElementCount);
        using var second = TStrategy.CreateSecond(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Except(TStrategy.AsEnumerable(second))
            .Select(static element => TStrategy.Number(element))
            .ToHashSet();
        AssertNumberRange(numbers,1,ElementCount/2);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Intersect() {
        using var source = TStrategy.Create(ElementCount);
        using var second = TStrategy.CreateSecond(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Intersect(TStrategy.AsEnumerable(second))
            .Select(static element => TStrategy.Number(element))
            .ToHashSet();
        AssertNumberRange(numbers,ElementCount/2+1,ElementCount/2);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ToHashSet() {
        using var source = TStrategy.Create(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Concat(TStrategy.AsEnumerable(source))
            .ToHashSet()
            .Select(static element => TStrategy.Number(element))
            .ToHashSet();
        AssertNumberRange(numbers,1,ElementCount);
    }

    private static void AssertNumberRange(global::System.Collections.Generic.HashSet<int> numbers,int start,int count) {
        Assert.Equal(count,numbers.Count);
        for(var offset = 0;offset<count;offset++) {
            Assert.Contains(start+offset,numbers);
        }
    }
}
