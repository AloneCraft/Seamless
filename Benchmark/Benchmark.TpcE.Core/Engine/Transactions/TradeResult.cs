namespace Benchmark.TpcE.Core;
// Trade-Result(TPC-E 3.3.8)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Trade-Result の入力。</summary>
public sealed record TradeResultInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeResult;
}
/// <summary>Trade-Result の出力。</summary>
public sealed record TradeResultOutput:TxnResult;
