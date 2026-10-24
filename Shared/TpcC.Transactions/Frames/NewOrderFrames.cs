namespace Shared.TpcC.Transactions;
/// <summary>
/// New-Order(仕様 2.4.2、HammerDB の neword は QUIRKS Q-029〜Q-033)。
/// 家の倉庫の区画に顧客・地区・注文・新規注文・注文明細を、供給倉庫の区画に在庫を書く。存在しない品目があれば業務上の巻き戻し(呼び出し側が全区画を戻す)。
/// </summary>
internal sealed class NewOrderFrames(TpcCMode mode,NewOrderQueries queries){
    public NewOrderResult Run(TpcCDatabase db,NewOrderRequest request){
        var warehouse=request.WarehouseId;
        var home=db.For(warehouse);
        var customer=home.Customer.Get(new CustomerKey(warehouse,request.DistrictId,request.CustomerId));
        var district=home.District.Get(new DistrictKey(warehouse,request.DistrictId));
        var orderId=district.NextOrderId;
        home.District.Update(district with{NextOrderId=orderId+1});
        var rolledBack=false;
        var amounts=new List<decimal>(request.Lines.Length);
        for(var index=0;index<request.Lines.Length;index++){
            var line=request.Lines[index];
            if(!home.Item.TryGet(new ItemKey(line.ItemId),out var item)){
                // 存在しない品目(最後の明細): 在庫も明細も無いまま、取引全体を巻き戻す。
                rolledBack=true;
                continue;
            }
            var supply=db.For(line.SupplyWarehouseId);
            var stock=supply.Stock.Get(new StockKey(line.SupplyWarehouseId,line.ItemId));
            var remaining=stock.Quantity-line.Quantity;
            StockRow updated;
            if(mode==TpcCMode.Spec){
                // 仕様 2.4.2.2: 在庫数 − 数量 が 10 以上なら引くだけ、そうでなければ 91 を足す。S_YTD・S_ORDER_CNT・S_REMOTE_CNT も更新する。
                if(remaining<10){
                    remaining+=91;
                }
                var remote=0;
                if(line.SupplyWarehouseId!=warehouse){
                    remote=1;
                }
                updated=stock with{Quantity=remaining,Ytd=stock.Ytd+line.Quantity,OrderCount=stock.OrderCount+1,RemoteCount=stock.RemoteCount+remote};
            }else{
                // HammerDB: 在庫数が数量より大きければ引くだけ、そうでなければ 91 を足す(Q-030)。集計列は更新しない(Q-031)。
                if(stock.Quantity<=line.Quantity){
                    remaining+=91;
                }
                updated=stock with{Quantity=remaining};
            }
            supply.Stock.Update(updated);
            var amount=line.Quantity*item.Price;
            amounts.Add(amount);
            home.OrderLine.Insert(new OrderLineRow(warehouse,request.DistrictId,orderId,index+1,line.ItemId,line.SupplyWarehouseId,0,line.Quantity,amount,updated.DistInfoOf(request.DistrictId)));
        }
        home.Order.Insert(new OrderRow(warehouse,request.DistrictId,orderId,request.CustomerId,request.Now,0,request.Lines.Length,request.AllLocal));
        home.NewOrder.Insert(new NewOrderRow(warehouse,request.DistrictId,orderId));
        var warehouseTax=home.Warehouse.Get(new WarehouseKey(warehouse)).Tax;
        var total=0m;
        if(mode==TpcCMode.Spec){
            total=queries.Total(amounts.ToArray(),customer.Discount,warehouseTax,district.Tax);
        }
        return new NewOrderResult(rolledBack,orderId,customer.Last,customer.Credit,customer.Discount,warehouseTax,district.Tax,request.Lines.Length,total);
    }
}
