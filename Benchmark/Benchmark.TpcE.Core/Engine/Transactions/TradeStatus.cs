namespace Benchmark.TpcE.Core;
// Trade-Status(TPC-E 3.3.9)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Trade-Status の入力。</summary>
public sealed record TradeStatusInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeStatus;
}
/// <summary>Trade-Status の出力。</summary>
public sealed record TradeStatusOutput:TxnResult;
