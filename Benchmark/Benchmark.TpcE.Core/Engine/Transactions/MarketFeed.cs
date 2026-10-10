namespace Benchmark.TpcE.Core;
// Market-Feed(TPC-E 3.3.3)の入出力。仮の型(取引の実装で項目を足す)。
/// <summary>Market-Feed の入力。</summary>
public sealed record MarketFeedInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.MarketFeed;
}
/// <summary>Market-Feed の出力。</summary>
public sealed record MarketFeedOutput:TxnResult;
