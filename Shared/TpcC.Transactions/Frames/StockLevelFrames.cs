namespace Shared.TpcC.Transactions;
/// <summary>
/// Stock-Level(仕様 2.8.2、HammerDB の slev。読み取りだけ)。地区の直近 20 件の注文の明細にある品目のうち、在庫がしきい値より少ないものの数。
/// 読む表は DISTRICT・ORDER-LINE・STOCK だけ(仕様 2.8.2.2 も HammerDB の結合も ORDERS は読まない)。明細は注文ごとに行番号 1.. を、無くなるまで引く。
/// </summary>
internal sealed class StockLevelFrames(StockLevelQueries queries){
    private const int RecentOrders=20;
    public StockLevelResult Run(TpcCDatabase db,StockLevelRequest request){
        var warehouse=request.WarehouseId;
        var partition=db.For(warehouse);
        var next=partition.District.Get(new DistrictKey(warehouse,request.DistrictId)).NextOrderId;
        var lines=new List<OrderLineRow>();
        for(var orderId=Math.Max(1,next-RecentOrders);orderId<next;orderId++){
            var number=1;
            while(partition.OrderLine.Find(new OrderLineKey(warehouse,request.DistrictId,orderId,number)) is OrderLineRow line){
                lines.Add(line);
                number++;
            }
        }
        var items=queries.DistinctItems(lines.ToArray());
        var stocks=new List<StockRow>(items.Length);
        foreach(var item in items){
            if(partition.Stock.Find(new StockKey(warehouse,item)) is StockRow stock){
                stocks.Add(stock);
            }
        }
        return new StockLevelResult(false,queries.LowStock(stocks.ToArray(),request.Threshold));
    }
}
