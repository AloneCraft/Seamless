namespace Shared.TpcE.Transactions;
/// <summary>Trade-Cleanup の問い合わせ(LINQ はここだけ)。</summary>
internal sealed class TradeCleanupQueries{
    /// <summary>発動待ちの注文の取引 ID を昇順に(本物でも TRADE_REQUEST は全件を走査する)。</summary>
    public long[] RequestIds(TpcEDatabase db){
        return db.TradeRequest.Rows.OrderBy(row=>row.TradeId).Select(row=>row.TradeId).ToArray();
    }
    /// <summary>起点以降で提出済みのままの取引 ID を昇順に。</summary>
    public long[] SubmittedIds(TpcEDatabase db,long firstTradeId,Code8 submittedId){
        return db.Trade.Rows.Where(row=>row.Id>=firstTradeId&&row.StatusId==submittedId).OrderBy(row=>row.Id).Select(row=>row.Id).ToArray();
    }
}
