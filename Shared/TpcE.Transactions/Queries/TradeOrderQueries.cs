namespace Shared.TpcE.Transactions;
/// <summary>Trade-Order と Trade-Result で共通の問い合わせ(LINQ はここだけ)。</summary>
internal sealed class TradeOrderQueries{
    /// <summary>顧客の税率の合計(CUSTOMER_TAXRATE の税率 ID を TAXRATE と結合)。</summary>
    public decimal TaxRateSum(TpcEDatabase db,Code8[] taxrateIds){
        return taxrateIds.Join(db.Taxrate.Rows,taxrateId=>taxrateId,row=>row.Id,(taxrateId,row)=>row.Rate).Sum();
    }
    /// <summary>手数料率(層・種別・取引所・数量帯で 1 行に決まる、3.3.7.5 手順 11)。</summary>
    public decimal CommissionRate(TpcEDatabase db,int tier,Code8 tradeTypeId,Code8 exchangeId,int quantity){
        return db.CommissionRate.Rows.Where(row=>row.Tier==tier&&row.TradeTypeId==tradeTypeId&&row.ExchangeId==exchangeId&&row.FromQuantity<=quantity&&row.ToQuantity>=quantity).Single().Rate;
    }
    /// <summary>口座の保有の時価合計(HOLDING_SUMMARY × LAST_TRADE、3.3.7.5 手順 13)。</summary>
    public decimal HoldingAssets(TpcEDatabase db,long accountId,Code16[] symbols){
        return symbols.Select(symbol=>db.HoldingSummary.Get(new HoldingSummaryKey(accountId,symbol)).Quantity*db.LastTrade.Get(new LastTradeKey(symbol)).Price).Sum();
    }
}
