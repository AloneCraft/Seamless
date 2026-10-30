namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Lookup(TPC-E 3.3.6)の問い合わせ。LINQ はここにだけ書き、TradeLookupFrames から呼ぶ。
/// 表と入力はすべて引数で受け取り、確定した配列を返す。
/// </summary>
internal sealed class TradeLookupQueries{
    /// <summary>状態の ID の一覧(STATUS_TYPE を走査し、ID の昇順)。TRADE_HISTORY を主キー (取引, 状態) で当てるのに使う。</summary>
    public Code8[] StatusIds(TpcEDatabase db){
        return db.StatusType.Rows.Select(row=>row.Id).OrderBy(statusId=>statusId).ToArray();
    }
    /// <summary>
    /// 取引の履歴を TH_DTS の昇順に最大 3 行返す(状態ごとに主キーで当てる。同時刻は状態の ID の昇順)。
    /// </summary>
    public TradeLookupHistory[] History(TpcEDatabase db,long tradeId,Code8[] statusIds){
        return statusIds.Select(statusId=>db.TradeHistory.Find(new TradeHistoryKey(tradeId,statusId))).Where(row=>row.HasValue).Select(row=>row.GetValueOrDefault()).OrderBy(row=>row.Dts).ThenBy(row=>row.StatusId).Take(TpcESpec.TradeLookupMaxTradeHistoryRows).Select(row=>new TradeLookupHistory(row.Dts,row.StatusId)).ToArray();
    }
    /// <summary>口座の取引のうち T_DTS が [start, end] のものを、T_DTS の昇順(同時刻は取引 ID の昇順)に最大 maxTrades 件返す(Frame 2)。</summary>
    public TradeRow[] AccountTrades(TpcEDatabase db,long accountId,long start,long end,int maxTrades){
        return db.AccountTrades.Range(accountId,new TimedId(start,long.MinValue),new TimedId(end,long.MaxValue),maxTrades).Select(entry=>db.Trade.Get(new TradeKey(entry.Id))).ToArray();
    }
    /// <summary>銘柄の取引のうち T_DTS が [start, end] のものを、T_DTS の昇順(同時刻は取引 ID の昇順)に最大 maxTrades 件返す(Frame 3)。</summary>
    public TradeRow[] SymbolTrades(TpcEDatabase db,Code16 symbol,long start,long end,int maxTrades){
        return db.SymbolTrades.Range(symbol,new TimedId(start,long.MinValue),new TimedId(end,long.MaxValue),maxTrades).Select(entry=>db.Trade.Get(new TradeKey(entry.Id))).ToArray();
    }
    /// <summary>1 つの保有(HH_H_T_ID)を変えた取引の履歴を、取引 ID の昇順に最大 limit 行返す(Frame 4)。</summary>
    public TradeLookupHoldingHistory[] HoldingChanges(TpcEDatabase db,long holdingTradeId,int limit){
        return db.HoldingHistoryByHolding.From(holdingTradeId,long.MinValue,limit).Select(tradeId=>db.HoldingHistory.Get(new HoldingHistoryKey(holdingTradeId,tradeId))).Select(row=>new TradeLookupHoldingHistory(row.HoldingTradeId,row.TradeId,row.BeforeQuantity,row.AfterQuantity)).ToArray();
    }
}
