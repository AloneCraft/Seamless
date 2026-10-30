namespace Benchmark.TpcC.Tsavorite.Linq;
/// <summary>Tsavorite × 標準LINQ の組み合わせの入口。</summary>
public sealed class TsavoriteLinqVariant:global::Benchmark.TpcC.Core.ITpcCVariant{
    public string Name=>"tsavorite-linq";
    public string ShortName=>"TL";
    public global::Benchmark.TpcC.Core.ITpcCEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcC.Core.TpcCScale scale,global::Benchmark.TpcC.Core.TpcCMode mode){
        return new global::Shared.TpcC.Transactions.TpcCEngine(new TpcCDatabase(store,scale),mode);
    }
    public global::Benchmark.TpcC.Core.ITpcCEngine Open(global::System.Collections.Generic.IReadOnlyList<global::Seamless.Execution.Store> stores,global::Benchmark.TpcC.Core.TpcCScale scale,global::Benchmark.TpcC.Core.TpcCMode mode){
        return new global::Shared.TpcC.Transactions.TpcCEngine(new TpcCDatabase(stores,scale),mode);
    }
}
