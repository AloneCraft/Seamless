namespace Benchmark.TpcE.Persisted.FLinq;
/// <summary>Persisted × FLinq の組み合わせの入口。</summary>
public sealed class PersistedFLinqVariant:global::Benchmark.TpcE.Core.ITpcEVariant{
    public string Name=>"persisted-flinq";
    public string ShortName=>"PF";
    public global::Benchmark.TpcE.Core.ITpcEEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcE.Core.TpcEScale scale){
        return new global::Shared.TpcE.Transactions.TpcEEngine(new TpcEDatabase(store,scale));
    }
}
