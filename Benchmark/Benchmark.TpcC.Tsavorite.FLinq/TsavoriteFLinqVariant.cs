namespace Benchmark.TpcC.Tsavorite.FLinq;
/// <summary>Tsavorite × FLinq の組み合わせの入口。</summary>
public sealed class TsavoriteFLinqVariant:global::Benchmark.TpcC.Core.ITpcCVariant{
    public string Name=>"tsavorite-flinq";
    public string ShortName=>"TF";
    public global::Benchmark.TpcC.Core.ITpcCEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcC.Core.TpcCScale scale,global::Benchmark.TpcC.Core.TpcCMode mode){
        return new global::Shared.TpcC.Transactions.TpcCEngine(new TpcCDatabase(store,scale),mode);
    }
}
