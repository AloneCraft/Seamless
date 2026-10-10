namespace Benchmark.TpcE.Core;
// Market-Watch(TPC-E 3.3.4)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Market-Watch の入力。</summary>
public sealed record MarketWatchInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.MarketWatch;
}
/// <summary>Market-Watch の出力。</summary>
public sealed record MarketWatchOutput:TxnResult;
