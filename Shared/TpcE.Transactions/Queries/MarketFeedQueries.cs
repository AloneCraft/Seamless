namespace Shared.TpcE.Transactions;
/// <summary>Market-Feed の問い合わせ(LINQ はここだけ)。</summary>
internal sealed class MarketFeedQueries{
    /// <summary>発動する注文(3.3.3.3 手順 3.5)。Stop-Loss と Limit-Buy は指値が相場以上、Limit-Sell は指値が相場以下。取引 ID の昇順。</summary>
    public TradeRequestRow[] Triggered(TpcEDatabase db,long[] requestIds,decimal quote,Code8 stopLoss,Code8 limitSell,Code8 limitBuy){
        return requestIds.Select(id=>db.TradeRequest.Get(new TradeRequestKey(id)))
            .Where(row=>(row.TradeTypeId==stopLoss&&row.BidPrice>=quote)||(row.TradeTypeId==limitSell&&row.BidPrice<=quote)||(row.TradeTypeId==limitBuy&&row.BidPrice>=quote))
            .OrderBy(row=>row.TradeId)
            .ToArray();
    }
}
