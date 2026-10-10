namespace Benchmark.TpcE.Core;
// Trade-Cleanup(TPC-E 3.3.12)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Trade-Cleanup の入力。</summary>
public sealed record TradeCleanupInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeCleanup;
}
/// <summary>Trade-Cleanup の出力。</summary>
public sealed record TradeCleanupOutput:TxnResult;
