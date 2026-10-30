namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Update(TPC-E 3.3.10)の問い合わせ。LINQ はここにだけ書き、TradeUpdateFrames から呼ぶ。
/// 表と入力はすべて引数で受け取り、確定した配列を返す(書き込みは手順の側で行う)。
/// </summary>
internal sealed class TradeUpdateQueries{
    /// <summary>状態の ID の一覧(STATUS_TYPE を走査し、ID の昇順)。TRADE_HISTORY を主キー (取引, 状態) で当てるのに使う。</summary>
    public Code8[] StatusIds(TpcEDatabase db){
        return db.StatusType.Rows.Select(row=>row.Id).OrderBy(statusId=>statusId).ToArray();
    }
    /// <summary>取引の履歴を TH_DTS の昇順に最大 3 行返す(状態ごとに主キーで当てる。同時刻は状態の ID の昇順)。</summary>
    public TradeUpdateHistory[] History(TpcEDatabase db,long tradeId,Code8[] statusIds){
        return statusIds.Select(statusId=>db.TradeHistory.Find(new TradeHistoryKey(tradeId,statusId))).Where(row=>row.HasValue).Select(row=>row.GetValueOrDefault()).OrderBy(row=>row.Dts).ThenBy(row=>row.StatusId).Take(TpcESpec.TradeUpdateMaxTradeHistoryRows).Select(row=>new TradeUpdateHistory(row.Dts,row.StatusId)).ToArray();
    }
    /// <summary>口座の取引のうち T_DTS が [start, end] のものを、T_DTS の昇順(同時刻は取引 ID の昇順)に最大 maxTrades 件返す(Frame 2)。</summary>
    public TradeRow[] AccountTrades(TpcEDatabase db,long accountId,long start,long end,int maxTrades){
        return db.AccountTrades.Range(accountId,new TimedId(start,long.MinValue),new TimedId(end,long.MaxValue),maxTrades).Select(entry=>db.Trade.Get(new TradeKey(entry.Id))).ToArray();
    }
    /// <summary>銘柄の取引のうち T_DTS が [start, end] のものを、T_DTS の昇順(同時刻は取引 ID の昇順)に最大 maxTrades 件返す(Frame 3)。</summary>
    public TradeRow[] SymbolTrades(TpcEDatabase db,Code16 symbol,long start,long end,int maxTrades){
        return db.SymbolTrades.Range(symbol,new TimedId(start,long.MinValue),new TimedId(end,long.MaxValue),maxTrades).Select(entry=>db.Trade.Get(new TradeKey(entry.Id))).ToArray();
    }
    /// <summary>書き戻す値を主キー(取引 ID)の昇順に並べる(取引 ID は重ならない)。</summary>
    public TradeUpdateWrite[] SortWrites(TpcEDatabase db,TradeUpdateWrite[] writes){
        return writes.OrderBy(write=>write.TradeId).ToArray();
    }
}
