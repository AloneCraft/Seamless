using Benchmark.TpcE.Core;
namespace Benchmark.TpcE;
/// <summary>既定の4つと明示選択するMVCC比較の入口。比較の先頭がMEEへ結果を渡す基準。</summary>
public static class VariantCatalog{
    public static IReadOnlyList<ITpcEVariant> All{get;}=[
        new Benchmark.TpcE.Tsavorite.Linq.TsavoriteLinqVariant(),
        new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant(),
        new Benchmark.TpcE.Persisted.Linq.PersistedLinqVariant(),
        new Benchmark.TpcE.Persisted.FLinq.PersistedFLinqVariant(),
    ];
    public static IReadOnlyList<ITpcEVariant> Versioned{get;}=[new Benchmark.TpcE.Mvcc.Linq.MvccLinqVariant(),new Benchmark.TpcE.Mvcc.FLinq.MvccFLinqVariant()];
    public static IEnumerable<ITpcEVariant> Available=>All.Concat(Versioned);
    public static ITpcEVariant ByShortName(string shortName){
        foreach(var variant in Available){
            if(string.Equals(variant.ShortName,shortName,StringComparison.OrdinalIgnoreCase)){
                return variant;
            }
        }
        throw new ArgumentException($"Unknown variant {shortName}.");
    }
}
