namespace Benchmark.TpcE.Tsavorite.FLinq;
/// <summary>Tsavorite × FLinq の組み合わせの入口。</summary>
public sealed class TsavoriteFLinqVariant:global::Benchmark.TpcE.Core.ITpcEVariant{
    public string Name=>"tsavorite-flinq";
    public string ShortName=>"TF";
    public global::Benchmark.TpcE.Core.ITpcEEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcE.Core.TpcEScale scale){
        return new global::Shared.TpcE.Transactions.TpcEEngine(new TpcEDatabase(store,scale));
    }
}
