using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZLinq;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SelectFromEnumerableArray
{
    readonly Consumer consumer = new();

    int[] array = Enumerable.Range(1, 1000000).ToArray();
    List<int> list = new List<int>(Enumerable.Range(1, 1000000));
    IReadOnlyCollection<int> readOnlyCollection = new ReadOnlyCollection<int>(Enumerable.Range(1, 1000000).ToList());
    IReadOnlyList<int> enumerableReadOnlyList = Enumerable.Range(1, 1000000).ToArray();
    IEnumerable<int> enumerableArray = Enumerable.Range(1, 1000000).ToArray();
    IEnumerable<int> enumerableNotCollection = ForceNotCollection(Enumerable.Range(1, 1000000));

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqSelect()
    {
        array.Select(x => x + x)
             .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqSelect_FromList()
    {
        list.Select(x => x + x)
            .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqSelect_FromReadOnlyCollection()
    {
        readOnlyCollection.Select(x => x + x)
                          .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqSelect_FromIReadOnlyListArray()
    {
        enumerableReadOnlyList.Select(x => x + x)
                       .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqSelect_FromEnumerableArray()
    {
        enumerableArray.Select(x => x + x)
                       .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LinqSelect_FromEnumerableNotCollection()
    {
        enumerableNotCollection.Select(x => x + x)
                               .Consume(consumer);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqSelect()
    {
        array.AsValueEnumerable()
             .Select(x => x + x)
             .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqSelect_FromList()
    {
        list.AsValueEnumerable()
            .Select(x => x + x)
            .Consume(consumer);
    }

    // FromEnumerable

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqSelect_FromReadOnlyCollection()
    {
        readOnlyCollection.AsValueEnumerable()
                          .Select(x => x + x)
                          .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqSelect_FromIReadOnlyListArray()
    {
        enumerableReadOnlyList.AsValueEnumerable()
                              .Select(x => x + x)
                              .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqSelect_FromEnumerableArray()
    {
        enumerableArray.AsValueEnumerable()
                       .Select(x => x + x)
                       .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinqSelect_FromEnumerableNotCollection()
    {
        enumerableNotCollection.AsValueEnumerable()
                               .Select(x => x + x)
                               .Consume(consumer);
    }

    static IEnumerable<T> ForceNotCollection<T>(IEnumerable<T> source)
    {
        foreach (T item in source)
            yield return item;
    }
}
