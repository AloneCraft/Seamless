namespace Shared.TpcC.Transactions;
/// <summary>
/// Delivery(仕様 2.7.4、HammerDB の delivery は Q-014・Q-040〜Q-043)。10 地区を 1 つの取引でその場で処理する(遅延実行しない)。
/// 地区ごとに最も古い新規注文を消し、注文に配送業者を付け、明細に配達日を付け、顧客の残高に明細の金額の合計を足す。
/// </summary>
internal sealed class DeliveryFrames(TpcCMode mode,DeliveryQueries queries){
    public DeliveryResult Run(TpcCDatabase db,DeliveryRequest request){
        var warehouse=request.WarehouseId;
        var partition=db.For(warehouse);
        var delivered=new List<DeliveredOrder>();
        var skipped=0;
        // HammerDB の変数は、新規注文が無い地区でも前の地区の値のまま残る(Q-041)。
        var staleCustomer=0;
        var staleTotal=0m;
        for(var district=1;district<=TpcCScale.DistrictsPerWarehouse;district++){
            var queue=partition.NewOrderQueue.From(new DistrictOwner(warehouse,district),0,1);
            if(queue.Length==0){
                skipped++;
                if(mode==TpcCMode.HammerDbMssql&&staleCustomer!=0&&partition.Customer.Find(new CustomerKey(warehouse,district,staleCustomer)) is CustomerRow other){
                    // 前の地区で配達した顧客と同じ番号の、この地区の顧客の残高に、前の地区の合計が足される。
                    partition.Customer.Update(other with{Balance=other.Balance+staleTotal});
                }
                continue;
            }
            var orderId=(int)queue[0];
            partition.NewOrder.Delete(new NewOrderKey(warehouse,district,orderId));
            var order=partition.Order.Get(new OrderKey(warehouse,district,orderId));
            partition.Order.Update(order with{CarrierId=request.CarrierId});
            var lines=new OrderLineRow[order.LineCount];
            for(var number=1;number<=order.LineCount;number++){
                var line=partition.OrderLine.Get(new OrderLineKey(warehouse,district,orderId,number));
                lines[number-1]=line;
                partition.OrderLine.Update(line with{DeliveryDate=request.Now});
            }
            decimal total;
            if(mode==TpcCMode.Spec){
                total=queries.ExactTotal(lines);
            }else{
                total=queries.RoundedTotal(lines);
            }
            var customer=partition.Customer.Get(new CustomerKey(warehouse,district,order.CustomerId));
            if(mode==TpcCMode.Spec){
                partition.Customer.Update(customer with{Balance=customer.Balance+total,DeliveryCount=customer.DeliveryCount+1});
            }else{
                // C_DELIVERY_CNT は更新しない(Q-042)。
                partition.Customer.Update(customer with{Balance=customer.Balance+total});
            }
            staleCustomer=order.CustomerId;
            staleTotal=total;
            delivered.Add(new DeliveredOrder(district,orderId,order.CustomerId,total));
        }
        return new DeliveryResult(false,delivered.ToArray(),skipped);
    }
}
