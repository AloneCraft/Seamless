using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Tests;
public sealed class TpcCEngine{
    private static readonly Core.TpcCMode[] Modes=[Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql];
    /// <summary>倉庫 3・区画 2(倉庫 1・2 が区画 0、倉庫 3 が区画 1)、品目 200・地区あたり顧客 30。</summary>
    private static Core.TpcCScale Scale=>TestDatabases.Mini(3,2);
    private static void ForEach(Core.TpcCScale scale,Action<Core.ITpcCVariant,Core.TpcCMode,Core.ITpcCEngine> action){
        foreach(var variant in TestDatabases.Variants){
            foreach(var mode in Modes){
                var (store,engine)=TestDatabases.Open(variant,scale,mode);
                try{
                    action(variant,mode,engine);
                }finally{
                    TestDatabases.Close(store,engine);
                }
            }
        }
    }
    private static int ExpectedQuantity(Core.TpcCMode mode,int quantity,int ordered){
        var remaining=quantity-ordered;
        if(mode==Core.TpcCMode.Spec){
            // 仕様: 在庫数 − 数量 が 10 以上なら引くだけ、そうでなければ 91 を足す。
            if(remaining<10){
                remaining+=91;
            }
            return remaining;
        }
        // HammerDB(Q-030): 在庫数が数量より大きければ引くだけ、そうでなければ 91 を足す。
        if(quantity<=ordered){
            remaining+=91;
        }
        return remaining;
    }
    private static Core.TpcCResult Run(Core.ITpcCEngine engine,Core.TpcCRequest request)=>engine.Execute(request);
    [Fact,Trait("Category","Integration")]public void Execute_NewOrder(){
        {
            //家の倉庫だけの注文: 地区の次の注文番号が進み、注文・新規注文・明細が入り、在庫が規則どおりに変わる
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,1);
                var before=engine.SumNextOrderIds();
                var stockBefore=partition.Stock.Get(new Core.StockKey(1,5));
                var item=partition.Item.Get(new Core.ItemKey(5));
                var request=new Core.NewOrderRequest(1,1000,2,3,[new Core.NewOrderLine(5,1,4),new Core.NewOrderLine(6,1,2)],1);
                var result=(Core.NewOrderResult)Run(engine,request);
                Assert.False(result.RolledBack,label);
                Assert.Equal(31,result.OrderId);
                Assert.Equal(before+1,engine.SumNextOrderIds());
                Assert.Equal(32,partition.District.Get(new Core.DistrictKey(1,2)).NextOrderId);
                var order=partition.Order.Get(new Core.OrderKey(1,2,31));
                Assert.Equal((3,0,2,1,1000L),(order.CustomerId,order.CarrierId,order.LineCount,order.AllLocal,order.EntryDate));
                Assert.True(partition.NewOrder.Contains(new Core.NewOrderKey(1,2,31)),label);
                Assert.Equal(31L,partition.NewOrderQueue.TopDesc(new Core.DistrictOwner(1,2),1)[0]);
                Assert.Equal(31L,partition.CustomerOrders.TopDesc(new Core.CustomerOwner(1,2,3,0),1)[0]);
                var stockAfter=partition.Stock.Get(new Core.StockKey(1,5));
                Assert.Equal(ExpectedQuantity(mode,stockBefore.Quantity,4),stockAfter.Quantity);
                var line=partition.OrderLine.Get(new Core.OrderLineKey(1,2,31,1));
                Assert.Equal(4*item.Price,line.Amount);
                Assert.Equal(stockAfter.DistInfoOf(2),line.DistInfo);
                Assert.Equal(0L,line.DeliveryDate);
                Assert.True(partition.OrderLine.Contains(new Core.OrderLineKey(1,2,31,2)),label);
                if(mode==Core.TpcCMode.Spec){
                    Assert.Equal((stockBefore.Ytd+4,stockBefore.OrderCount+1,stockBefore.RemoteCount),(stockAfter.Ytd,stockAfter.OrderCount,stockAfter.RemoteCount));
                    Assert.True(result.TotalAmount>0m,label);
                }else{
                    Assert.Equal((stockBefore.Ytd,stockBefore.OrderCount,stockBefore.RemoteCount),(stockAfter.Ytd,stockAfter.OrderCount,stockAfter.RemoteCount));
                    Assert.Equal(0m,result.TotalAmount);
                }
            });
        }
        {
            //存在しない品目を含む注文は業務上の巻き戻し: 表も索引も元に戻り、D_NEXT_O_ID(NOPM の元)も増えない
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var before=Core.TpcCStateDigest.Of(engine.Database);
                var nextBefore=engine.SumNextOrderIds();
                var request=new Core.NewOrderRequest(1,1000,2,3,[new Core.NewOrderLine(5,1,4),new Core.NewOrderLine(201,1,2)],1);
                var result=(Core.NewOrderResult)Run(engine,request);
                Assert.True(result.RolledBack,label);
                Assert.Empty(Core.TpcCStateDigest.Of(engine.Database).Differences(before));
                Assert.Equal(nextBefore,engine.SumNextOrderIds());
                //巻き戻しの後も取引が通り、注文番号は欠番にならない
                var next=(Core.NewOrderResult)Run(engine,new Core.NewOrderRequest(1,1001,2,3,[new Core.NewOrderLine(5,1,1)],1));
                Assert.Equal(31,next.OrderId);
            });
        }
        {
            //他の区画の倉庫から出す明細: 家の区画に注文を、供給倉庫の区画に在庫を書く。巻き戻すと両方の区画が戻る
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var supply=TestDatabases.PartitionOf(engine,3);
                Assert.NotSame(supply,TestDatabases.PartitionOf(engine,1));
                var stockBefore=supply.Stock.Get(new Core.StockKey(3,9));
                var result=(Core.NewOrderResult)Run(engine,new Core.NewOrderRequest(1,1000,4,5,[new Core.NewOrderLine(9,3,3),new Core.NewOrderLine(10,1,1)],0));
                Assert.False(result.RolledBack,label);
                var stockAfter=supply.Stock.Get(new Core.StockKey(3,9));
                Assert.Equal(ExpectedQuantity(mode,stockBefore.Quantity,3),stockAfter.Quantity);
                if(mode==Core.TpcCMode.Spec){
                    Assert.Equal(stockBefore.RemoteCount+1,stockAfter.RemoteCount);
                }else{
                    Assert.Equal(stockBefore.RemoteCount,stockAfter.RemoteCount);
                }
                var line=TestDatabases.PartitionOf(engine,1).OrderLine.Get(new Core.OrderLineKey(1,4,31,1));
                Assert.Equal(3,line.SupplyWarehouseId);
                Assert.Equal(stockAfter.DistInfoOf(4),line.DistInfo);
                var digest=Core.TpcCStateDigest.Of(engine.Database);
                var rolledBack=(Core.NewOrderResult)Run(engine,new Core.NewOrderRequest(1,1002,4,5,[new Core.NewOrderLine(11,3,3),new Core.NewOrderLine(201,1,1)],0));
                Assert.True(rolledBack.RolledBack,label);
                Assert.Empty(Core.TpcCStateDigest.Of(engine.Database).Differences(digest));
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_Payment(){
        {
            //番号で引く Payment: 顧客の残高が減り、履歴が入り、地区と倉庫の YTD が増える。仕様の形だけが顧客の累計と回数も更新する
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,1);
                var good=partition.Customer.EnumerateRows().First(row=>row.WarehouseId==1&&row.DistrictId==2&&row.Credit=="GC");
                var warehouseBefore=partition.Warehouse.Get(new Core.WarehouseKey(1));
                var districtBefore=partition.District.Get(new Core.DistrictKey(1,2));
                var result=(Core.PaymentResult)Run(engine,new Core.PaymentRequest(1,2000,2,1,2,good.Id,null,125m));
                Assert.Equal(good.Id,result.CustomerId);
                var after=partition.Customer.Get(new Core.CustomerKey(1,2,good.Id));
                Assert.Equal(good.Balance-125m,after.Balance);
                Assert.Equal(after.Balance,result.CustomerBalance);
                Assert.Equal(warehouseBefore.Ytd+125m,partition.Warehouse.Get(new Core.WarehouseKey(1)).Ytd);
                var districtAfter=partition.District.Get(new Core.DistrictKey(1,2));
                Assert.Equal((districtBefore.Ytd+125m,districtBefore.NextHistoryId+1),(districtAfter.Ytd,districtAfter.NextHistoryId));
                var history=partition.History.Get(new Core.HistoryKey(1,2,districtBefore.NextHistoryId));
                Assert.Equal((good.Id,125m,2000L),(history.CustomerId,history.Amount,history.Date));
                if(mode==Core.TpcCMode.Spec){
                    Assert.Equal((good.YtdPayment+125m,good.PaymentCount+1),(after.YtdPayment,after.PaymentCount));
                    Assert.Equal(good.Data,after.Data);
                    Assert.Equal(warehouseBefore.Name+"    "+districtBefore.Name,history.Data);
                }else{
                    //HammerDB: 累計と回数は更新せず(Q-035)、信用が BC でない顧客の C_DATA を信用の値で上書きし(Q-036)、H_DATA は空白だけになる(Q-038)
                    Assert.Equal((good.YtdPayment,good.PaymentCount),(after.YtdPayment,after.PaymentCount));
                    Assert.Equal("GC",after.Data);
                    Assert.Equal(new string(' ',24),history.Data);
                }
            });
        }
        {
            //信用不良(BC)の顧客: C_DATA の先頭に足して 500 文字で切る。HammerDB の足す部分は 219 文字(Q-037)
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,2);
                var bad=partition.Customer.EnumerateRows().First(row=>row.WarehouseId==2&&row.DistrictId==3&&row.Credit=="BC");
                Run(engine,new Core.PaymentRequest(2,2000,3,2,3,bad.Id,null,40m));
                var after=partition.Customer.Get(new Core.CustomerKey(2,3,bad.Id));
                Assert.True(after.Data.Length<=500,label);
                if(mode==Core.TpcCMode.Spec){
                    Assert.StartsWith($"{bad.Id} 3 2 3 2 40 ",after.Data);
                    Assert.EndsWith(bad.Data[..Math.Min(bad.Data.Length,500-$"{bad.Id} 3 2 3 2 40 ".Length)],after.Data);
                }else{
                    Assert.Equal(500,after.Data.Length);
                    Assert.Equal(bad.Data[..Math.Min(bad.Data.Length,500-219)],after.Data[219..Math.Min(500,219+bad.Data.Length)]);
                    Assert.Equal(new string(' ',11)+" "+new string(' ',11)+" ",after.Data[(219-24)..219]);
                }
            });
        }
        {
            //遠隔の顧客(別の区画の倉庫): 顧客の行は顧客の区画に、履歴・地区・倉庫は家の区画に書かれる
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var homePartition=TestDatabases.PartitionOf(engine,1);
                var customerPartition=TestDatabases.PartitionOf(engine,3);
                Assert.NotSame(homePartition,customerPartition);
                var before=customerPartition.Customer.Get(new Core.CustomerKey(3,5,7));
                var districtBefore=homePartition.District.Get(new Core.DistrictKey(1,2));
                Run(engine,new Core.PaymentRequest(1,2000,2,3,5,7,null,60m));
                Assert.Equal(before.Balance-60m,customerPartition.Customer.Get(new Core.CustomerKey(3,5,7)).Balance);
                var history=homePartition.History.Get(new Core.HistoryKey(1,2,districtBefore.NextHistoryId));
                Assert.Equal((3,5,7),(history.CustomerWarehouseId,history.CustomerDistrictId,history.CustomerId));
                Assert.Equal(districtBefore.Ytd+60m,homePartition.District.Get(new Core.DistrictKey(1,2)).Ytd);
                Assert.DoesNotContain(Core.TpcCConsistency.Check(engine.Database,deep:false,initial:false),result=>!result.Passed);
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_PaymentByName(){
        {
            //同じ姓が複数いるとき、名(大文字にそろえた序数比較)の昇順に並べた ceil(n/2) 番目の顧客が選ばれる。n が奇数も偶数も
            var scale=new Core.TpcCScale{Warehouses=1,Items=100,CustomersPerDistrict=1500};
            ForEach(scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,1);
                var groups=partition.Customer.EnumerateRows().Where(row=>row.DistrictId==4).GroupBy(row=>row.Last).Where(group=>group.Count()>=2).ToList();
                var tested=new HashSet<int>();
                foreach(var group in groups){
                    var sorted=group.OrderBy(row=>row.First.ToUpperInvariant(),StringComparer.Ordinal).ThenBy(row=>row.Id).ToList();
                    var n=sorted.Count;
                    if(n>6||!tested.Add(n)){
                        continue;
                    }
                    var expected=sorted[(n+1)/2-1];
                    var result=(Core.PaymentResult)Run(engine,new Core.PaymentRequest(1,2000,4,1,4,1,group.Key,10m));
                    Assert.Equal(expected.Id,result.CustomerId);
                    var order=(Core.OrderStatusResult)Run(engine,new Core.OrderStatusRequest(1,2000,4,1,group.Key));
                    Assert.Equal(expected.Id,order.CustomerId);
                }
                Assert.True(tested.Count>=2,$"{label}: group sizes {string.Join(",",tested)}");
                Assert.Contains(tested,n=>n%2==1);
                Assert.Contains(tested,n=>n%2==0);
            });
        }
        {
            //該当する顧客がいない姓は、番号のまま進む
            ForEach(Scale,(variant,mode,engine)=>{
                var result=(Core.PaymentResult)Run(engine,new Core.PaymentRequest(1,2000,2,1,2,9,"EINGEINGEING",5m));
                Assert.Equal(9,result.CustomerId);
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_OrderStatus(){
        {
            //顧客の最新の注文と明細を返す。注文のない顧客は OrderId が 0 で明細も空。その顧客が注文すると、その注文が返る
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,1);
                var withOrders=partition.Order.EnumerateRows().Where(row=>row.WarehouseId==1&&row.DistrictId==2).Select(row=>row.CustomerId).ToHashSet();
                var customerWith=withOrders.Min();
                var latest=partition.Order.EnumerateRows().Where(row=>row.WarehouseId==1&&row.DistrictId==2&&row.CustomerId==customerWith).OrderBy(row=>row.Id).Last();
                var result=(Core.OrderStatusResult)Run(engine,new Core.OrderStatusRequest(1,3000,2,customerWith,null));
                Assert.Equal((latest.Id,latest.LineCount,latest.CarrierId),(result.OrderId,result.Lines.Length,result.CarrierId));
                Assert.Equal(partition.Customer.Get(new Core.CustomerKey(1,2,customerWith)).Balance,result.CustomerBalance);
                var without=Enumerable.Range(1,30).FirstOrDefault(id=>!withOrders.Contains(id));
                if(without!=0){
                    //HammerDB の形は注文の顧客が順列でないので、注文のない顧客がいる(Q-025)
                    Assert.Equal(Core.TpcCMode.HammerDbMssql,mode);
                    var empty=(Core.OrderStatusResult)Run(engine,new Core.OrderStatusRequest(1,3000,2,without,null));
                    Assert.Equal(0,empty.OrderId);
                    Assert.Empty(empty.Lines);
                    Run(engine,new Core.NewOrderRequest(1,3001,2,without,[new Core.NewOrderLine(3,1,1),new Core.NewOrderLine(4,1,2)],1));
                    var created=(Core.OrderStatusResult)Run(engine,new Core.OrderStatusRequest(1,3002,2,without,null));
                    Assert.Equal(31,created.OrderId);
                    Assert.Equal(new[]{3,4},created.Lines.Select(line=>line.ItemId).ToArray());
                }else{
                    Assert.Equal(Core.TpcCMode.Spec,mode);
                }
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_Delivery(){
        {
            //各地区で最も古い新規注文が配達される: 新規注文が消え、注文に配送業者が付き、明細に配達日が付き、顧客の残高に明細の合計が足される
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,2);
                var first=engine.Database.Scale.FirstInitialNewOrder;
                var customerBefore=partition.Customer.EnumerateRows().ToDictionary(row=>(row.WarehouseId,row.DistrictId,row.Id));
                var result=(Core.DeliveryResult)Run(engine,new Core.DeliveryRequest(2,4000,7));
                Assert.Equal(10,result.Delivered.Length);
                Assert.Equal(0,result.SkippedDistricts);
                foreach(var delivered in result.Delivered){
                    Assert.Equal(first,delivered.OrderId);
                    Assert.False(partition.NewOrder.Contains(new Core.NewOrderKey(2,delivered.DistrictId,first)),label);
                    Assert.Equal(first+1L,partition.NewOrderQueue.From(new Core.DistrictOwner(2,delivered.DistrictId),0,1)[0]);
                    var order=partition.Order.Get(new Core.OrderKey(2,delivered.DistrictId,first));
                    Assert.Equal(7,order.CarrierId);
                    var lines=Enumerable.Range(1,order.LineCount).Select(number=>partition.OrderLine.Get(new Core.OrderLineKey(2,delivered.DistrictId,first,number))).ToList();
                    Assert.All(lines,line=>Assert.Equal(4000L,line.DeliveryDate));
                    var customer=partition.Customer.Get(new Core.CustomerKey(2,delivered.DistrictId,order.CustomerId));
                    var before=customerBefore[(2,delivered.DistrictId,order.CustomerId)];
                    Assert.Equal(before.Balance+delivered.Total,customer.Balance);
                    if(mode==Core.TpcCMode.Spec){
                        Assert.Equal(before.DeliveryCount+1,customer.DeliveryCount);
                        Assert.Equal(lines.Sum(line=>line.Amount),delivered.Total);
                    }else{
                        Assert.Equal(before.DeliveryCount,customer.DeliveryCount);
                    }
                }
            });
        }
        {
            //合計の丸め: 仕様は正確な合計(1.50+2.50+3.40=7.40)、HammerDB は明細ごとに整数へ丸めた和(2+3+3=8、Q-040)
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,1);
                var first=engine.Database.Scale.FirstInitialNewOrder;
                var order=partition.Order.Get(new Core.OrderKey(1,1,first));
                var amounts=new[]{1.50m,2.50m,3.40m};
                for(var number=1;number<=order.LineCount;number++){
                    var line=partition.OrderLine.Get(new Core.OrderLineKey(1,1,first,number));
                    var amount=0m;
                    if(number<=amounts.Length){
                        amount=amounts[number-1];
                    }
                    TestDatabases.PutRaw(partition.OrderLine,line with{Amount=amount});
                }
                var result=(Core.DeliveryResult)Run(engine,new Core.DeliveryRequest(1,4000,1));
                var total=result.Delivered.Single(delivered=>delivered.DistrictId==1).Total;
                if(mode==Core.TpcCMode.Spec){
                    Assert.Equal(7.40m,total);
                }else{
                    Assert.Equal(8m,total);
                }
            });
        }
        {
            //新規注文が無い地区: 仕様の形はとばす。HammerDB の形は前の地区で配達した顧客と同じ番号の、この地区の顧客の残高に前の地区の合計を足してしまう(Q-041)
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,1);
                var first=engine.Database.Scale.FirstInitialNewOrder;
                var last=engine.Database.Scale.InitialOrdersPerDistrict;
                partition.Log.Begin();
                for(var order=first;order<=last;order++){
                    partition.NewOrder.Delete(new Core.NewOrderKey(1,2,order));
                }
                partition.Log.Commit();
                var order1=partition.Order.Get(new Core.OrderKey(1,1,first));
                for(var number=1;number<=order1.LineCount;number++){
                    var line=partition.OrderLine.Get(new Core.OrderLineKey(1,1,first,number));
                    TestDatabases.PutRaw(partition.OrderLine,line with{Amount=10m});
                }
                var stale=partition.Customer.Get(new Core.CustomerKey(1,2,order1.CustomerId));
                var result=(Core.DeliveryResult)Run(engine,new Core.DeliveryRequest(1,4000,3));
                Assert.Equal(1,result.SkippedDistricts);
                Assert.Equal(9,result.Delivered.Length);
                var total=result.Delivered.Single(delivered=>delivered.DistrictId==1).Total;
                Assert.Equal(order1.LineCount*10m,total);
                var staleAfter=partition.Customer.Get(new Core.CustomerKey(1,2,order1.CustomerId));
                if(mode==Core.TpcCMode.HammerDbMssql){
                    Assert.Equal(stale.Balance+total,staleAfter.Balance);
                }else{
                    Assert.Equal(stale.Balance,staleAfter.Balance);
                }
            });
        }
        {
            //最初の地区(1)が空なら、前の地区の値が無いので何も足されない
            ForEach(Scale,(variant,mode,engine)=>{
                var partition=TestDatabases.PartitionOf(engine,1);
                var first=engine.Database.Scale.FirstInitialNewOrder;
                var last=engine.Database.Scale.InitialOrdersPerDistrict;
                partition.Log.Begin();
                for(var order=first;order<=last;order++){
                    partition.NewOrder.Delete(new Core.NewOrderKey(1,1,order));
                }
                partition.Log.Commit();
                var before=partition.Customer.EnumerateRows().Where(row=>row.WarehouseId==1&&row.DistrictId==1).ToDictionary(row=>row.Id);
                var result=(Core.DeliveryResult)Run(engine,new Core.DeliveryRequest(1,4000,3));
                Assert.Equal(1,result.SkippedDistricts);
                Assert.All(partition.Customer.EnumerateRows().Where(row=>row.WarehouseId==1&&row.DistrictId==1),row=>Assert.Equal(before[row.Id],row));
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_StockLevel(){
        {
            //直近 20 件の注文の明細にある品目のうち、在庫がしきい値より少ないものの数(重複は 1 つに数える)
            ForEach(Scale,(variant,mode,engine)=>{
                var label=$"{variant.ShortName}/{mode}";
                var partition=TestDatabases.PartitionOf(engine,2);
                var next=partition.District.Get(new Core.DistrictKey(2,6)).NextOrderId;
                var items=partition.OrderLine.EnumerateRows().Where(row=>row.WarehouseId==2&&row.DistrictId==6&&row.OrderId>=next-20&&row.OrderId<next).Select(row=>row.ItemId).Distinct().ToList();
                foreach(var threshold in new[]{10,15,20,60,101}){
                    var expected=items.Count(item=>partition.Stock.Get(new Core.StockKey(2,item)).Quantity<threshold);
                    var result=(Core.StockLevelResult)Run(engine,new Core.StockLevelRequest(2,5000,6,threshold));
                    Assert.Equal(expected,result.LowStock);
                }
                Assert.Equal(items.Count,((Core.StockLevelResult)Run(engine,new Core.StockLevelRequest(2,5000,6,101))).LowStock);
            });
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_Workload(){
        {
            //生成した入力を流し続けても、全区画の一貫性が保たれる。仕様の形は全条件(5〜10・12 も)、HammerDB の形は条件 10・12 を除く(初めから外れている: Q-046)。NOPM の元の D_NEXT_O_ID は確定した New-Order の数だけ増える
            foreach(var variant in TestDatabases.Variants){
                foreach(var mode in Modes){
                    var (store,engine)=TestDatabases.Open(variant,Scale,mode);
                    try{
                        var label=$"{variant.ShortName}/{mode}";
                        var generators=Enumerable.Range(0,4).Select(terminal=>new Core.TpcCInputGenerator(Scale,mode,7,terminal,Core.TpcCNuRandConstants.For(mode,7,new Core.TpcCDataGenerator(Scale,Core.TpcCLoadRules.For(mode),1).LastNameCLoad))).ToArray();
                        var nextBefore=engine.SumNextOrderIds();
                        var committed=0;
                        var rolledBack=0;
                        for(long index=0;index<600;index++){
                            var request=generators[(int)(index%4)].Next(index/4,10_000+index);
                            var result=engine.Execute(request);
                            if(request is Core.NewOrderRequest){
                                if(result.RolledBack){
                                    rolledBack++;
                                }else{
                                    committed++;
                                }
                            }
                        }
                        Assert.Equal(nextBefore+committed,engine.SumNextOrderIds());
                        var results=Core.TpcCConsistency.Check(engine.Database,deep:true,initial:false);
                        foreach(var result in results){
                            var expectedToFail=mode==Core.TpcCMode.HammerDbMssql&&(result.Condition==10||result.Condition==12);
                            Assert.True(result.Passed!=expectedToFail,$"{label} CC{result.Condition}: {result.Detail}");
                        }
                        Assert.Empty(Core.TpcCStateDigest.ItemReplicaDifferences(engine.Database));
                    }finally{
                        TestDatabases.Close(store,engine);
                    }
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void Execute_Agrees(){
        {
            //同じ入力を 4 つの組み合わせへ流すと、結果も状態(全表と全索引のダイジェスト、保存して開き直す前)も同じ
            foreach(var mode in Modes){
                var stores=new List<Store>();
                var engines=new List<Core.ITpcCEngine>();
                try{
                    foreach(var variant in TestDatabases.Variants){
                        var (store,engine)=TestDatabases.Open(variant,Scale,mode);
                        stores.Add(store);
                        engines.Add(engine);
                    }
                    var generators=Enumerable.Range(0,4).Select(terminal=>new Core.TpcCInputGenerator(Scale,mode,11,terminal,Core.TpcCNuRandConstants.For(mode,11,new Core.TpcCDataGenerator(Scale,Core.TpcCLoadRules.For(mode),1).LastNameCLoad))).ToArray();
                    for(long index=0;index<400;index++){
                        var request=generators[(int)(index%4)].Next(index/4,20_000+index);
                        byte[]? expected=null;
                        for(var position=0;position<engines.Count;position++){
                            var bytes=CanonicalWriter.ToBytes(engines[position].Execute(request));
                            if(expected is null){
                                expected=bytes;
                            }else{
                                Assert.True(expected.AsSpan().SequenceEqual(bytes),$"{mode} request {index} ({request.Kind}) differs in {TestDatabases.Variants[position].ShortName}");
                            }
                        }
                        if(index%100==99){
                            var reference=Core.TpcCStateDigest.Of(engines[0].Database);
                            for(var position=1;position<engines.Count;position++){
                                Assert.Empty(reference.Differences(Core.TpcCStateDigest.Of(engines[position].Database)));
                            }
                        }
                    }
                }finally{
                    for(var position=0;position<engines.Count;position++){
                        TestDatabases.Close(stores[position],engines[position]);
                    }
                }
            }
        }
    }
}
