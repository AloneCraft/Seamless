namespace Shared.TpcC.Transactions;
/// <summary>Stock-Level(仕様 2.8.2、HammerDB の slev。読み取りだけ)。地区の直近 20 件の注文の明細にある品目のうち、在庫がしきい値より少ないものの数。</summary>
internal sealed class StockLevelFrames(StockLevelQueries queries){
    private const int RecentOrders=20;
    public StockLevelResult Run(TpcCDatabase db,StockLevelRequest request){
        var warehouse=request.WarehouseId;
        var partition=db.For(warehouse);
        var next=partition.District.Get(new DistrictKey(warehouse,request.DistrictId)).NextOrderId;
        var lines=new List<OrderLineRow>();
        for(var orderId=Math.Max(1,next-RecentOrders);orderId<next;orderId++){
            if(partition.Order.Find(new OrderKey(warehouse,request.DistrictId,orderId)) is not OrderRow order){
                continue;
            }
            for(var number=1;number<=order.LineCount;number++){
                if(partition.OrderLine.Find(new OrderLineKey(warehouse,request.DistrictId,orderId,number)) is OrderLineRow line){
                    lines.Add(line);
                }
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
