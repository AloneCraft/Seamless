namespace Benchmark.TpcE.Core;
// Trade-Lookup(TPC-E 3.3.6)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Trade-Lookup の入力。</summary>
public sealed record TradeLookupInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeLookup;
}
/// <summary>Trade-Lookup の出力。</summary>
public sealed record TradeLookupOutput:TxnResult;
