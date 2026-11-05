namespace Benchmark.TpcE.Mvcc.FLinq;
/// <summary>共通MVCC/WALを明示して比較する入口。Rowsの静的型を保つ。</summary>
public sealed class MvccFLinqVariant:global::Benchmark.TpcE.Core.ITpcEVariant{
    public string Name=>"mvcc-flinq";
    public string ShortName=>"VF";
    public bool UsesVersionedTransactions=>true;
    public global::Benchmark.TpcE.Core.ITpcEEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcE.Core.TpcEScale scale)=>new global::Shared.TpcE.Transactions.TpcEEngine(global::Benchmark.TpcE.Core.TpcEMvccOptions.Open(store,scale));
}
