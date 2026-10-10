namespace Shared.TpcE.Transactions;
/// <summary>Market-Watch(TPC-E 3.3.4)の手順。LINQ は書かない(問い合わせは MarketWatchQueries に書く)。仮の実装。</summary>
internal sealed class MarketWatchFrames{
    public MarketWatchOutput Run(TpcEDatabase db,MarketWatchInput input){
        throw new NotImplementedException("Market-Watch is not implemented yet.");
    }
}
