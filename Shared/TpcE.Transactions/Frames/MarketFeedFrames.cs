namespace Shared.TpcE.Transactions;
/// <summary>Market-Feed(TPC-E 3.3.3)の手順。LINQ は書かない(問い合わせは MarketFeedQueries に書く)。</summary>
internal sealed class MarketFeedFrames{
    private readonly MarketFeedQueries queries=new();
    public MarketFeedOutput Run(TpcEDatabase db,MarketFeedInput input){
        var now=input.Now;
        var updated=0;
        var triggered=new List<TriggeredTrade>();
        foreach(var entry in input.Entries){
            // LAST_TRADE を更新(同じ銘柄が複数回出ても項目ごとに更新する)。
            var last=db.LastTrade.Find(new LastTradeKey(entry.Symbol));
            if(last is LastTradeRow lastRow){
                db.LastTrade.Update(lastRow with{Price=entry.PriceQuote,Volume=lastRow.Volume+entry.TradeQuantity,Dts=now});
                updated++;
            }
            // 発動する注文(索引 X9 で銘柄の発動待ちを引く)。書き込みは配列にしてから行う。
            var ids=db.PendingBySymbol.All(entry.Symbol);
            var rows=this.queries.Triggered(db,ids,entry.PriceQuote,input.TypeStopLoss,input.TypeLimitSell,input.TypeLimitBuy);
            foreach(var row in rows){
                var trade=db.Trade.Get(new TradeKey(row.TradeId));
                db.Trade.Update(trade with{Dts=now,StatusId=input.StatusSubmitted});
                db.TradeRequest.Delete(new TradeRequestKey(row.TradeId));
                db.TradeHistory.Insert(new TradeHistoryRow(row.TradeId,now,input.StatusSubmitted));
                triggered.Add(new TriggeredTrade(entry.Symbol,row.TradeId,row.BidPrice,row.Quantity,row.TradeTypeId));
            }
        }
        var status=0;
        if(updated<input.UniqueSymbols){
            status=-311;
        }
        return new MarketFeedOutput{Status=status,NumUpdated=updated,SendLength=triggered.Count,Triggered=[..triggered]};
    }
}
