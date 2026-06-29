using Collection.Strategy.CollectionLinq;

namespace Collection.Test.CollectionLinqChain1;

public abstract class ListLinqChain1<TStrategy,TSource,TElement,TKey,TValue>
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
    public void ElementAt() {
        using var source = TStrategy.Create(ElementCount);
        var element = TStrategy.AsEnumerable(source).ElementAt(ElementCount/2);
        Assert.Equal(ElementCount/2+1,TStrategy.Number(element));
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Skip() {
        using var source = TStrategy.Create(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Skip(ElementCount/2)
            .Select(static element => TStrategy.Number(element))
            .ToArray();
        Assert.Equal(ElementCount/2,numbers.Length);
        Assert.Equal(ElementCount/2+1,numbers[0]);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Take() {
        using var source = TStrategy.Create(ElementCount);
        var numbers = TStrategy.AsEnumerable(source)
            .Take(ElementCount/2)
            .Select(static element => TStrategy.Number(element))
            .ToArray();
        Assert.Equal(ElementCount/2,numbers.Length);
        Assert.Equal(1,numbers[0]);
        Assert.Equal(ElementCount/2,numbers[^1]);
    }
}
