namespace Shared.TpcE.Transactions;
/// <summary>Trade-Status(TPC-E 3.3.9)の問い合わせ。LINQ はここにだけ書く(手順は TradeStatusFrames)。</summary>
internal sealed class TradeStatusQueries{
    /// <summary>
    /// 直近の取引(TRADE)を STATUS_TYPE・TRADE_TYPE・SECURITY・EXCHANGE と主キーで結合し、T_DTS の降順に並べる。
    /// 同時刻は取引 ID の降順(仕様は同時刻の並びを決めていない)。
    /// </summary>
    public TradeStatusEntry[] Recent(TpcEDatabase db,TimedId[] recent){
        return recent
            .Select(r=>db.Trade.Get(new TradeKey(r.Id)))
            .Select(t=>new TradeWithSecurity(t,db.Security.Get(new SecurityKey(t.Symbol))))
            .Select(x=>new TradeStatusEntry(
                x.Trade.Id,
                x.Trade.Dts,
                db.StatusType.Get(new StatusTypeKey(x.Trade.StatusId)).Name,
                db.TradeType.Get(new TradeTypeKey(x.Trade.TypeId)).Name,
                x.Trade.Symbol,
                x.Trade.Quantity,
                x.Trade.ExecName,
                x.Trade.Charge,
                x.Security.Name,
                db.Exchange.Get(new ExchangeKey(x.Security.ExchangeId)).Name))
            .OrderByDescending(e=>e.TradeDts)
            .ThenByDescending(e=>e.TradeId)
            .ToArray();
    }
}
