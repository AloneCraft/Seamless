using Benchmark.TpcE.Core;
namespace Benchmark.TpcE;
/// <summary>4 つの組み合わせの入口。並びは既定の比較の順(先頭が MEE へ結果を渡す基準)。</summary>
public static class VariantCatalog{
    public static IReadOnlyList<ITpcEVariant> All{get;}=[
        new Benchmark.TpcE.Tsavorite.Linq.TsavoriteLinqVariant(),
        new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant(),
        new Benchmark.TpcE.Persisted.Linq.PersistedLinqVariant(),
        new Benchmark.TpcE.Persisted.FLinq.PersistedFLinqVariant(),
    ];
    public static ITpcEVariant ByShortName(string shortName){
        foreach(var variant in All){
            if(string.Equals(variant.ShortName,shortName,StringComparison.OrdinalIgnoreCase)){
                return variant;
            }
        }
        throw new ArgumentException($"Unknown variant {shortName}.");
    }
}
