namespace Benchmark.TpcE.Persisted.Linq;
/// <summary>Persisted × 標準LINQ の組み合わせの入口。</summary>
public sealed class PersistedLinqVariant:global::Benchmark.TpcE.Core.ITpcEVariant{
    public string Name=>"persisted-linq";
    public string ShortName=>"PL";
    public global::Benchmark.TpcE.Core.ITpcEEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcE.Core.TpcEScale scale){
        return new global::Shared.TpcE.Transactions.TpcEEngine(new TpcEDatabase(store,scale));
    }
}
