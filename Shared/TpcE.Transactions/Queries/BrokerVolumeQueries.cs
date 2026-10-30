namespace Shared.TpcE.Transactions;
/// <summary>Broker-Volume(TPC-E 3.3.1)の問い合わせ。LINQ はここにだけ書く(手順は BrokerVolumeFrames)。</summary>
internal sealed class BrokerVolumeQueries{
    /// <summary>部門名から部門 ID を引く。SECTOR は小さい表なので走査する。</summary>
    public Code8[] SectorIds(TpcEDatabase db,string sectorName){
        return db.Sector.Rows
            .Where(s=>string.Equals(s.Name,sectorName,StringComparison.Ordinal))
            .Select(s=>s.Id)
            .OrderBy(id=>id)
            .ToArray();
    }
    /// <summary>部門に属する業種の ID。INDUSTRY は小さい表なので走査する。</summary>
    public Code8[] IndustryIds(TpcEDatabase db,Code8 sectorId){
        return db.Industry.Rows
            .Where(i=>i.SectorId==sectorId)
            .Select(i=>i.Id)
            .OrderBy(id=>id)
            .ToArray();
    }
    /// <summary>名前が一覧にあるブローカー(B_NAME ∈ broker_list)。BROKER は小さい表なので走査する。ID の昇順。</summary>
    public BrokerRow[] Brokers(TpcEDatabase db,string[] brokerNames){
        return db.Broker.Rows
            .Join(brokerNames,b=>b.Name,name=>name,(b,name)=>b,StringComparer.Ordinal)
            .OrderBy(b=>b.Id)
            .ToArray();
    }
    /// <summary>
    /// TRADE_REQUEST を走査し、部門の銘柄(TR_S_SYMB)と一覧のブローカー(TR_B_ID)に当たる注文の TR_QTY×TR_BID_PRICE をブローカー名ごとに合計する。
    /// 合計の降順、同額はブローカー名の Ordinal 昇順(仕様は同額の並びを決めていない)。
    /// </summary>
    public BrokerVolumeEntry[] Volumes(TpcEDatabase db,Code16[] symbols,BrokerRow[] brokers){
        return db.TradeRequest.Rows
            .Join(symbols,r=>r.Symbol,symbol=>symbol,(r,symbol)=>r)
            .Join(brokers,r=>r.BrokerId,b=>b.Id,(r,b)=>new BrokerTradeAmount(b.Name,r.Quantity*r.BidPrice))
            .GroupBy(a=>a.BrokerName,StringComparer.Ordinal)
            .Select(g=>new BrokerVolumeEntry(g.Key,g.Sum(a=>a.Amount)))
            .OrderByDescending(e=>e.Volume)
            .ThenBy(e=>e.BrokerName,StringComparer.Ordinal)
            .ToArray();
    }
}
