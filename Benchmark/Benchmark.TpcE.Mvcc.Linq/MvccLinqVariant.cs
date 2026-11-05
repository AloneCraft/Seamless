namespace Benchmark.TpcE.Mvcc.Linq;
/// <summary>共通MVCC/WALを明示して比較する入口。Rowsの静的型を保つ。</summary>
public sealed class MvccLinqVariant:global::Benchmark.TpcE.Core.ITpcEVariant{
    public string Name=>"mvcc-linq";
    public string ShortName=>"VL";
    public bool UsesVersionedTransactions=>true;
    public global::Benchmark.TpcE.Core.ITpcEEngine Open(global::Seamless.Execution.Store store,global::Benchmark.TpcE.Core.TpcEScale scale)=>new global::Shared.TpcE.Transactions.TpcEEngine(global::Benchmark.TpcE.Core.TpcEMvccOptions.Open(store,scale));
}
