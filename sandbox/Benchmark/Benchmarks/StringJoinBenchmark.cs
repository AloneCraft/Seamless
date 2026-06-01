using Benchmark.ZLinq;
using BenchmarkDotNet.Attributes;
using ZLinq;
using ZLinq.Internal;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[GenericTypeArguments(typeof(int))]    // ValueType
[GenericTypeArguments(typeof(string))] // ReferenceType
public class StringJoinBenchmark<T> : EnumerableBenchmarkBase<T>
{
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void SystemLinq()
    {
        _ = string.Join(',', source.ArrayData);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq()
    {
        _ = source.ArrayData
                  .AsValueEnumerable()
                  .JoinToString(',');
    }
}
