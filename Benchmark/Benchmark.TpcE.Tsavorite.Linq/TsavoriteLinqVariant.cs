namespace Benchmark.TpcE.Tsavorite.Linq;
/// <summary>Tsavorite × 標準LINQ の組み合わせの入口。</summary>
public sealed class TsavoriteLinqVariant:global::Benchmark.TpcE.Core.ITpcEVariant{
    public string Name=>"tsavorite-linq";
    public string ShortName=>"TL";
    public global::Benchmark.TpcE.Core.ITpcEEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcE.Core.TpcEScale scale){
        return new global::Shared.TpcE.Transactions.TpcEEngine(new TpcEDatabase(store,scale));
    }
}
