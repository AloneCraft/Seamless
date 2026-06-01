using ZLinq;

namespace Benchmark.ZLinq;

[BenchmarkCategory(Categories.Methods.ToImmutableDictionary)]
[BenchmarkCategory(Categories.Filters.NET8_0_OR_GREATER)]
[BenchmarkCategory(Categories.Filters.ZLINQ_1_2_0_OR_GREATER)]
public partial class ToImmutableDictionaryBenchmark<T> : EnumerableBenchmarkBase_WithBasicTypes<T>
    where T : notnull
{
#if NET8_0_OR_GREATER
#if !USE_ZLINQ_NUGET_PACKAGE || ZLINQ_1_2_0_OR_GREATER
    [Benchmark]
    [BenchmarkCategory(Categories.From.Default)]    
    public void ToImmutableDictionary()
    {
        _ = source.Default
                  .AsValueEnumerable()
                  .Distinct()
                  .ToImmutableDictionary(x => x, x => x);
    }
#endif
#endif
}
