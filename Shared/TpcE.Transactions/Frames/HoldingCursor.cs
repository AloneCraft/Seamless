namespace Shared.TpcE.Transactions;
/// <summary>口座×銘柄の保有を、LIFO(新しい順)か FIFO(古い順)で読む(Trade-Order と Trade-Result で共通。LINQ は使わない)。</summary>
internal static class HoldingCursor{
    public static HoldingRow[] Read(TpcEDatabase db,long account,Code16 symbol,bool lifo){
        var ids=db.PositionHoldings.All(new HoldingSummaryKey(account,symbol));
        if(lifo){
            Array.Reverse(ids);
        }
        var rows=new HoldingRow[ids.Length];
        for(var index=0;index<ids.Length;index++){
            rows[index]=db.Holding.Get(new HoldingKey(ids[index].Id));
        }
        return rows;
    }
}
