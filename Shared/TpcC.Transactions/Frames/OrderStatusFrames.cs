namespace Shared.TpcC.Transactions;
/// <summary>Order-Status(仕様 2.6.2、HammerDB の ostat。読み取りだけ)。顧客の最新の注文と、その明細を返す。</summary>
internal sealed class OrderStatusFrames(CustomerLookup lookup,OrderStatusQueries queries){
    public OrderStatusResult Run(TpcCDatabase db,OrderStatusRequest request){
        var warehouse=request.WarehouseId;
        var partition=db.For(warehouse);
        var customerId=lookup.Resolve(partition,warehouse,request.DistrictId,request.CustomerId,request.CustomerLast);
        var customer=partition.Customer.Get(new CustomerKey(warehouse,request.DistrictId,customerId));
        var latest=partition.CustomerOrders.TopDesc(new CustomerOwner(warehouse,request.DistrictId,customerId,0),1);
        if(latest.Length==0){
            // 初期データの注文の顧客が順列でない HammerDB の形では、注文の無い顧客に当たる(Q-025)。
            return new OrderStatusResult(false,customerId,customer.First,customer.Middle,customer.Last,customer.Balance,0,0,0,[]);
        }
        var order=partition.Order.Get(new OrderKey(warehouse,request.DistrictId,(int)latest[0]));
        var lines=new List<OrderLineRow>(order.LineCount);
        for(var number=1;number<=order.LineCount;number++){
            var line=partition.OrderLine.Find(new OrderLineKey(warehouse,request.DistrictId,order.Id,number));
            if(line is OrderLineRow found){
                lines.Add(found);
            }
        }
        return new OrderStatusResult(false,customerId,customer.First,customer.Middle,customer.Last,customer.Balance,order.Id,order.EntryDate,order.CarrierId,queries.ToLines(lines.ToArray()));
    }
}
