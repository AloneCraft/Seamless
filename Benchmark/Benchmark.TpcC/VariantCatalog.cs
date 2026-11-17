using Benchmark.TpcC.Core;
namespace Benchmark.TpcC;
/// <summary>2 つの組み合わせの入口。並びは既定の比較の順(先頭が比較の基準)。</summary>
public static class VariantCatalog{
    public static IReadOnlyList<ITpcCVariant> All{get;}=[
        new Benchmark.TpcC.Tsavorite.Linq.TsavoriteLinqVariant(),
        new Benchmark.TpcC.Tsavorite.FLinq.TsavoriteFLinqVariant(),
    ];
    public static ITpcCVariant ByShortName(string shortName){
        foreach(var variant in All){
            if(string.Equals(variant.ShortName,shortName,StringComparison.OrdinalIgnoreCase)){
                return variant;
            }
        }
        throw new ArgumentException($"Unknown variant {shortName}.");
    }
}
