namespace Shared.TpcE.Transactions;
/// <summary>Customer-Position(TPC-E 3.3.2)の問い合わせ。LINQ はここにだけ書く(手順は CustomerPositionFrames)。</summary>
internal sealed class CustomerPositionQueries{
    /// <summary>口座の保有(HOLDING_SUMMARY、索引 X5)ごとの数量と銘柄の現在値(LAST_TRADE)。</summary>
    public HoldingValue[] HoldingValues(TpcEDatabase db,long accountId){
        return db.AccountHoldingSummaries.All(accountId)
            .Select(symbol=>new HoldingValue(accountId,db.HoldingSummary.Get(new HoldingSummaryKey(accountId,symbol)).Quantity,db.LastTrade.Get(new LastTradeKey(symbol)).Price))
            .ToArray();
    }
    /// <summary>
    /// 口座ごとの資産(CUSTOMER_ACCOUNT と HOLDING_SUMMARY×LAST_TRADE の左外部結合を口座で束ね、sum(HS_QTY×LT_PRICE)、無ければ 0)。
    /// 資産の昇順、同額は口座 ID の昇順(仕様は同額の並びを決めていない)。先頭 max_acct_len 件まで。
    /// </summary>
    public CustomerAccountPosition[] Positions(TpcEDatabase db,CustomerAccountRow[] accounts,HoldingValue[] holdings){
        return accounts
            .GroupJoin(holdings,a=>a.Id,h=>h.AccountId,(a,owned)=>new CustomerAccountPosition(a.Id,a.Balance,owned.Sum(h=>h.Quantity*h.Price)))
            .OrderBy(p=>p.AssetTotal)
            .ThenBy(p=>p.AccountId)
            .Take(TpcESpec.MaxAccountLength)
            .ToArray();
    }
    /// <summary>状態の名前(STATUS_TYPE は小さい表なので走査する)。ID の昇順。</summary>
    public StatusTypeRow[] StatusTypes(TpcEDatabase db){
        return db.StatusType.Rows
            .OrderBy(s=>s.Id)
            .ToArray();
    }
    /// <summary>
    /// 直近の取引(TRADE)とその状態の記録(TRADE_HISTORY)と状態名(STATUS_TYPE)を結合し、TH_DTS の降順に先頭 max_hist_len 件。
    /// 同時刻は取引 ID の降順、次に状態名の Ordinal 昇順(仕様は同時刻の並びを決めていない)。
    /// </summary>
    public CustomerPositionHistory[] History(TpcEDatabase db,TradeRow[] trades,TradeHistoryRow[] histories,StatusTypeRow[] statuses){
        return histories
            .Join(trades,h=>h.TradeId,t=>t.Id,(h,t)=>new TradeHistoryEvent(t,h))
            .Join(statuses,e=>e.History.StatusId,s=>s.Id,(e,s)=>new CustomerPositionHistory(e.Trade.Id,e.Trade.Symbol,e.Trade.Quantity,s.Name,e.History.Dts))
            .OrderByDescending(h=>h.HistoryDts)
            .ThenByDescending(h=>h.TradeId)
            .ThenBy(h=>h.TradeStatus,StringComparer.Ordinal)
            .Take(TpcESpec.MaxHistoryLength)
            .ToArray();
    }
}
