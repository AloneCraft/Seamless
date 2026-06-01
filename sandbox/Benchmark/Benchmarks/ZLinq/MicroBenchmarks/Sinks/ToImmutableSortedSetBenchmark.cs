using ZLinq;

namespace Benchmark.ZLinq;

[BenchmarkCategory(Categories.Methods.ToImmutableSortedSet)]
[BenchmarkCategory(Categories.Filters.NET8_0_OR_GREATER)]
[BenchmarkCategory(Categories.Filters.ZLINQ_1_2_0_OR_GREATER)]
public partial class ToImmutableSortedSetBenchmark<T> : EnumerableBenchmarkBase_WithBasicTypes<T>
{
#if !USE_ZLINQ_NUGET_PACKAGE || ZLINQ_1_2_0_OR_GREATER
#if NET8_0_OR_GREATER
    [Benchmark]
    [BenchmarkCategory(Categories.From.Default)]
    public void ToImmutableSortedSet()
    {
        _ = source.Default
                  .AsValueEnumerable()
                  .ToImmutableSortedSet();
    }
#endif
#endif
}
