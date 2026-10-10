namespace Benchmark.TpcE.Core;
// Trade-Update(TPC-E 3.3.10)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Trade-Update の入力。</summary>
public sealed record TradeUpdateInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeUpdate;
}
/// <summary>Trade-Update の出力。</summary>
public sealed record TradeUpdateOutput:TxnResult;
