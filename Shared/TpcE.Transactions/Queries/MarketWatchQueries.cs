namespace Shared.TpcE.Transactions;
/// <summary>Market-Watch(TPC-E 3.3.4)の問い合わせ。LINQ はここにだけ書く(手順は MarketWatchFrames)。</summary>
internal sealed class MarketWatchQueries{
    /// <summary>顧客のウォッチリスト(WATCH_LIST、索引 X12)の銘柄(WATCH_ITEM、索引 X13)。リストをまたぐ重複も結合どおり残す。</summary>
    public Code16[] WatchListSymbols(TpcEDatabase db,long customerId){
        return db.CustomerWatchLists.All(customerId)
            .SelectMany(watchListId=>db.WatchListItems.All(watchListId))
            .ToArray();
    }
    /// <summary>業種名から業種 ID を引く。INDUSTRY は小さい表なので走査する。</summary>
    public Code8[] IndustryIds(TpcEDatabase db,string industryName){
        return db.Industry.Rows
            .Where(i=>string.Equals(i.Name,industryName,StringComparison.Ordinal))
            .Select(i=>i.Id)
            .OrderBy(id=>id)
            .ToArray();
    }
    /// <summary>業種の会社(索引 X14)のうち ID が [startingCompanyId, endingCompanyId] のものの銘柄(索引 X15)。</summary>
    public Code16[] IndustrySymbols(TpcEDatabase db,Code8 industryId,long startingCompanyId,long endingCompanyId){
        return db.IndustryCompanies.Range(industryId,startingCompanyId,endingCompanyId,int.MaxValue)
            .SelectMany(companyId=>db.CompanySecurities.All(companyId))
            .ToArray();
    }
    /// <summary>銘柄群の時価総額。開始日の終値(DAILY_MARKET)と現在値(LAST_TRADE)に発行済株数(SECURITY)を掛けて足す。</summary>
    public MarketCapitalization Capitalization(TpcEDatabase db,Code16[] symbols,long startDate){
        return symbols
            .Select(symbol=>new SymbolPrices(
                db.Security.Get(new SecurityKey(symbol)).NumOutstanding,
                db.DailyMarket.Get(new DailyMarketKey(symbol,startDate)).Close,
                db.LastTrade.Get(new LastTradeKey(symbol)).Price))
            .Aggregate(new MarketCapitalization(0m,0m),(total,p)=>new MarketCapitalization(total.Old+p.NumOutstanding*p.OldPrice,total.New+p.NumOutstanding*p.NewPrice));
    }
}
