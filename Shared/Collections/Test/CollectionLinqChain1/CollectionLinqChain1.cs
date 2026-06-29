using Collection.Strategy.CollectionLinq;

namespace Collection.Test.CollectionLinqChain1;

public abstract class CollectionLinqChain1<TStrategy,TSource,TElement,TKey,TValue>
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
    public void Where() {
        using var source = TStrategy.Create(ElementCount);
        var count = TStrategy.AsEnumerable(source)
            .Where(static element => (TStrategy.Number(element)&1)==0)
            .Count();
        Assert.Equal(ElementCount/2,count);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Select() {
        using var source = TStrategy.Create(ElementCount);
        var values = TStrategy.AsEnumerable(source)
            .Select(static element => TStrategy.Number(element)+1)
            .ToArray();
        Assert.Equal(ElementCount,values.Length);
        Assert.Contains(2,values);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Any() {
        using var source = TStrategy.Create(ElementCount);
        var found = TStrategy.AsEnumerable(source)
            .Any(static element => TStrategy.Number(element)==ElementCount/2);
        Assert.True(found);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void Count() {
        using var source = TStrategy.Create(ElementCount);
        var count = TStrategy.AsEnumerable(source).Count();
        Assert.Equal(ElementCount,count);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void First() {
        using var source = TStrategy.Create(ElementCount);
        var first = TStrategy.AsEnumerable(source).First();
        var number = TStrategy.Number(first);
        Assert.True(number>=1&&number<=ElementCount);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ToArray() {
        using var source = TStrategy.Create(ElementCount);
        var values = TStrategy.AsEnumerable(source).ToArray();
        Assert.Equal(ElementCount,values.Length);
    }

    [BenchFact,Benchmark]
    [BenchmarkCategory(Categories.Linq)]
    public void ToList() {
        using var source = TStrategy.Create(ElementCount);
        var values = TStrategy.AsEnumerable(source).ToList();
        Assert.Equal(ElementCount,values.Count);
    }
}
