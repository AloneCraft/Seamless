namespace Benchmark.TpcE.Core;
// Trade-Cleanup(TPC-E 3.3.12)の入出力。
/// <summary>Trade-Cleanup の入力(3.3.12.1)。状態 ID は入力で受け取る。</summary>
public sealed record TradeCleanupInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeCleanup;
    public Code8 StatusCanceled{get;init;}
    public Code8 StatusPending{get;init;}
    public Code8 StatusSubmitted{get;init;}
    /// <summary>提出済みの取引を探す起点の取引 ID(trade_id)。</summary>
    public long TradeId{get;init;}
}
/// <summary>Trade-Cleanup の出力。取り消した件数を比較に使う。</summary>
public sealed record TradeCleanupOutput:TxnResult{
    public int CanceledRequests{get;init;}
    public int CanceledSubmitted{get;init;}
}
