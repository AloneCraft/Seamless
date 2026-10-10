namespace Shared.TpcE.Transactions;
/// <summary>Trade-Cleanup(TPC-E 3.3.12)の手順。LINQ は書かない(問い合わせは TradeCleanupQueries に書く)。仮の実装。</summary>
internal sealed class TradeCleanupFrames{
    public TradeCleanupOutput Run(TpcEDatabase db,TradeCleanupInput input){
        throw new NotImplementedException("Trade-Cleanup is not implemented yet.");
    }
}
