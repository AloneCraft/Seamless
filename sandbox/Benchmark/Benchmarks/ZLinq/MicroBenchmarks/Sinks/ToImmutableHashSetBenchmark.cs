using ZLinq;

namespace Benchmark.ZLinq;

[BenchmarkCategory(Categories.Methods.ToImmutableHashSet)]
[BenchmarkCategory(Categories.Filters.NET8_0_OR_GREATER)]
[BenchmarkCategory(Categories.Filters.ZLINQ_1_2_0_OR_GREATER)]
public partial class ToImmutableHashSetBenchmark<T> : EnumerableBenchmarkBase_WithBasicTypes<T>
{
#if NET8_0_OR_GREATER
#if !USE_ZLINQ_NUGET_PACKAGE || ZLINQ_1_2_0_OR_GREATER
    [Benchmark]
    [BenchmarkCategory(Categories.From.Default)]
    public void ToImmutableHashSet()
    {
        _ = source.Default
                  .AsValueEnumerable()
                  .ToImmutableHashSet();
    }
#endif
#endif
}
