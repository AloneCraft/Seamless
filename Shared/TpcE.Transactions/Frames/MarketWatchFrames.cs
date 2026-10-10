namespace Shared.TpcE.Transactions;
/// <summary>Market-Watch(TPC-E 3.3.4)の手順。LINQ は書かない(問い合わせは MarketWatchQueries に書く)。</summary>
internal sealed class MarketWatchFrames{
    private readonly MarketWatchQueries queries=new();
    public MarketWatchOutput Run(TpcEDatabase db,MarketWatchInput input){
        // 呼ぶ前の検査(3.3.4.3)。どの指し方も無ければ Frame を呼ばない。
        if(input.AccountId==0&&input.CustomerId==0&&string.IsNullOrEmpty(input.IndustryName)){
            return new MarketWatchOutput{Status=-411};
        }
        // Frame 1(3.3.4.3)。銘柄群を cust_id → industry_name → acct_id の順で決める。
        Code16[] symbols;
        if(input.CustomerId!=0){
            symbols=this.queries.WatchListSymbols(db,input.CustomerId);
        }else if(!string.IsNullOrEmpty(input.IndustryName)){
            var list=new List<Code16>();
            foreach(var industryId in this.queries.IndustryIds(db,input.IndustryName)){
                list.AddRange(this.queries.IndustrySymbols(db,industryId,input.StartingCompanyId,input.EndingCompanyId));
            }
            symbols=list.ToArray();
        }else{
            // 口座の保有の要約の銘柄(索引 X5)。
            symbols=db.AccountHoldingSummaries.All(input.AccountId);
        }
        var capitalization=this.queries.Capitalization(db,symbols,input.StartDate);
        // 開始日の時価総額が 0(銘柄が無い等)なら変化率は 0。
        var pctChange=0m;
        if(capitalization.Old!=0m){
            pctChange=decimal.Round(100m*(capitalization.New/capitalization.Old-1m),2,MidpointRounding.AwayFromZero);
        }
        return new MarketWatchOutput{PctChange=pctChange};
    }
}
