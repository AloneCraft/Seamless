namespace Shared.TpcE.Transactions;
/// <summary>Market-Feed(TPC-E 3.3.3)の手順。LINQ は書かない(問い合わせは MarketFeedQueries に書く)。仮の実装。</summary>
internal sealed class MarketFeedFrames{
    public MarketFeedOutput Run(TpcEDatabase db,MarketFeedInput input){
        throw new NotImplementedException("Market-Feed is not implemented yet.");
    }
}
