namespace Shared.TpcC.Transactions;
/// <summary>
/// New-Order(仕様 2.4.2、HammerDB の neword は QUIRKS Q-029〜Q-033)。
/// 家の倉庫の区画に顧客・地区・注文・新規注文・注文明細を、供給倉庫の区画に在庫を書く。存在しない品目があれば業務上の巻き戻し(呼び出し側が全区画を戻す)。
/// </summary>
internal sealed class NewOrderFrames(TpcCMode mode,NewOrderQueries queries){
    private const string Original="ORIGINAL";
    public NewOrderResult Run(TpcCDatabase db,NewOrderRequest request){
        var spec=mode==TpcCMode.Spec;
        var warehouse=request.WarehouseId;
        var home=db.For(warehouse);
        var customer=home.Customer.Get(new CustomerKey(warehouse,request.DistrictId,request.CustomerId));
        var district=home.District.Get(new DistrictKey(warehouse,request.DistrictId));
        var orderId=district.NextOrderId;
        home.District.Update(district with{NextOrderId=orderId+1});
        var rolledBack=false;
        // 合計金額と明細ごとの出力は仕様の形だけが作る(HammerDB は作らない: Q-032)。
        List<decimal>? amounts=null;
        List<NewOrderLineResult>? lineResults=null;
        var allLocal=request.AllLocal;
        if(spec){
            amounts=new List<decimal>(request.Lines.Length);
            lineResults=new List<NewOrderLineResult>(request.Lines.Length);
            // 仕様 2.4.2.2: O_ALL_LOCAL は SUT が明細の供給倉庫から決める。HammerDB は運転側の抽選で決める(W=1 でも 0 になる: Q-029)。
            allLocal=1;
            foreach(var line in request.Lines){
                if(line.SupplyWarehouseId!=warehouse){
                    allLocal=0;
                }
            }
        }
        for(var index=0;index<request.Lines.Length;index++){
            var line=request.Lines[index];
            var supply=db.For(line.SupplyWarehouseId);
            if(!home.Item.TryGet(new ItemKey(line.ItemId),out var item)){
                // 存在しない品目(最後の明細): 明細は入らず、取引全体を巻き戻す。HammerDB は続けて在庫の UPDATE(該当行なし)も実行するので、鍵の探索だけ同じに行う。
                rolledBack=true;
                if(!spec){
                    supply.Stock.TryGet(new StockKey(line.SupplyWarehouseId,line.ItemId),out _);
                }
                continue;
            }
            var stock=supply.Stock.Get(new StockKey(line.SupplyWarehouseId,line.ItemId));
            var remaining=stock.Quantity-line.Quantity;
            StockRow updated;
            if(spec){
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
            if(spec){
                // brand-generic(2.4.2.2): 品目のデータと在庫のデータの両方に ORIGINAL を含むなら "B"、そうでなければ "G"。
                var brand="G";
                if(item.Data.Contains(Original,StringComparison.Ordinal)&&stock.Data.Contains(Original,StringComparison.Ordinal)){
                    brand="B";
                }
                amounts!.Add(amount);
                lineResults!.Add(new NewOrderLineResult(line.ItemId,line.SupplyWarehouseId,item.Name,line.Quantity,updated.Quantity,brand,item.Price,amount));
            }
            home.OrderLine.Insert(new OrderLineRow(warehouse,request.DistrictId,orderId,index+1,line.ItemId,line.SupplyWarehouseId,0,line.Quantity,amount,updated.DistInfoOf(request.DistrictId)));
        }
        home.Order.Insert(new OrderRow(warehouse,request.DistrictId,orderId,request.CustomerId,request.Now,0,request.Lines.Length,allLocal));
        home.NewOrder.Insert(new NewOrderRow(warehouse,request.DistrictId,orderId));
        var warehouseTax=home.Warehouse.Get(new WarehouseKey(warehouse)).Tax;
        var total=0m;
        NewOrderLineResult[] lines=[];
        if(spec){
            total=queries.Total(amounts!.ToArray(),customer.Discount,warehouseTax,district.Tax);
            lines=lineResults!.ToArray();
        }
        return new NewOrderResult(rolledBack,orderId,orderId+1,request.Now,customer.Last,customer.Credit,customer.Discount,warehouseTax,district.Tax,request.Lines.Length,total,lines);
    }
}
