namespace Benchmark.TpcE.Core;
// Trade-Order(TPC-E 3.3.7)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Trade-Order の入力。</summary>
public sealed record TradeOrderInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeOrder;
}
/// <summary>Trade-Order の出力。</summary>
public sealed record TradeOrderOutput:TxnResult;
