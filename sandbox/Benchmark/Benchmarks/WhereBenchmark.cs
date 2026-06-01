using Benchmark.ZLinq;
using ZLinq;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[GenericTypeArguments(typeof(int))]    // ValueType
[GenericTypeArguments(typeof(string))] // ReferenceType
public class WhereBenchmark<T> : EnumerableBenchmarkBase<T>
{
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [BenchmarkCategory(Categories.From.Array)]
    public void FromArray_Linq()
    {
        source.ArrayData
              .Where(x => true)
              .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    [BenchmarkCategory(Categories.From.List)]
    public void FromList_Linq()
    {
        source.ListData
              .Where(x => true)
              .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    [BenchmarkCategory(Categories.From.Array)]
    public void FromArray_ZLinq()
    {
        source.ArrayData
              .AsValueEnumerable()
              .Where(x => true)
              .Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    [BenchmarkCategory(Categories.From.List)]
    public void FromList_ZLinq()
    {
        source.ListData
              .AsValueEnumerable()
              .Where(x => true)
              .Consume(consumer);
    }
}
