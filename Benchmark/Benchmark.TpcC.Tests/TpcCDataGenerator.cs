namespace Benchmark.TpcC.Tests;
public sealed class TpcCDataGenerator{
    private static Core.TpcCCountingLoadSink Count(Core.TpcCScale scale,Core.TpcCMode mode=Core.TpcCMode.Spec,ulong seed=1){
        var sink=new Core.TpcCCountingLoadSink();
        new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(mode),seed).Generate(sink);
        return sink;
    }
    [Fact,Trait("Category","Unit")]public void Generate(){
        {
            //縮小した規模(倉庫 2・品目 200・地区あたり顧客 30)の件数: 倉庫 2、地区 20、顧客 600、履歴 600、注文 600、未配送 9 件/地区、在庫 400、品目 200
            var sink=Count(TestDatabases.Mini(2,1));
            Assert.Equal(2,sink.Warehouses);
            Assert.Equal(20,sink.Districts);
            Assert.Equal(600,sink.Customers);
            Assert.Equal(600,sink.Histories);
            Assert.Equal(600,sink.Orders);
            Assert.Equal(20*9,sink.NewOrders);
            Assert.Equal(400,sink.Stocks);
            Assert.Equal(200,sink.Items);
            Assert.InRange(sink.OrderLines,600*5,600*15);
        }
        {
            //同じ種なら全表と全索引が同じダイジェスト、違う種なら違う
            var scale=TestDatabases.Mini(2,2);
            var (store1,db1)=TestDatabases.Load(scale);
            var (store2,db2)=TestDatabases.Load(scale);
            var (store3,db3)=TestDatabases.Load(scale,seed:2);
            try{
                var d1=Core.TpcCStateDigest.Of(db1);
                Assert.Empty(d1.Differences(Core.TpcCStateDigest.Of(db2)));
                Assert.NotEmpty(d1.Differences(Core.TpcCStateDigest.Of(db3)));
            }finally{
                db1.Dispose();
                db2.Dispose();
                db3.Dispose();
                store1.Dispose();
                store2.Dispose();
                store3.Dispose();
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void GenerateSpecCardinalities(){
        {
            //仕様の規模(倉庫 1)の件数(4.2.1): 在庫と品目は 100,000、顧客・履歴・注文は 30,000、未配送は 9,000、注文明細は平均 15 万 ×2 前後
            var sink=Count(new Core.TpcCScale{Warehouses=1});
            Assert.Equal(1,sink.Warehouses);
            Assert.Equal(10,sink.Districts);
            Assert.Equal(30_000,sink.Customers);
            Assert.Equal(30_000,sink.Histories);
            Assert.Equal(30_000,sink.Orders);
            Assert.Equal(9_000,sink.NewOrders);
            Assert.Equal(100_000,sink.Stocks);
            Assert.Equal(100_000,sink.Items);
            Assert.InRange(sink.OrderLines,297_000,303_000);
        }
    }
    [Fact,Trait("Category","Unit")]public void LastName(){
        {
            //番号 0〜999 を 3 つの音節にする(4.3.2.3)
            Assert.Equal("BARBARBAR",Core.TpcCDataGenerator.LastName(0));
            Assert.Equal("BARBAROUGHT",Core.TpcCDataGenerator.LastName(1));
            Assert.Equal("EINGEINGEING",Core.TpcCDataGenerator.LastName(999));
            Assert.Equal("PRIPRESESE",Core.TpcCDataGenerator.LastName(345));
        }
    }
    [Fact,Trait("Category","Integration")]public void GenerateRules(){
        {
            //仕様の形は BC が約 10%、HammerDB の形(Q-024)は約 50%。最初の 1,000 人の姓は番号順
            var scale=new Core.TpcCScale{Warehouses=1,Items=2_000,CustomersPerDistrict=3_000};
            foreach(var (mode,low,high) in new[]{(Core.TpcCMode.Spec,0.07,0.13),(Core.TpcCMode.HammerDbMssql,0.46,0.54)}){
                var (store,db)=TestDatabases.Load(scale,mode);
                try{
                    var customers=db.For(1).Customer.EnumerateRows().ToList();
                    Assert.Equal(30_000,customers.Count);
                    var bad=customers.Count(row=>row.Credit=="BC")/(double)customers.Count;
                    Assert.InRange(bad,low,high);
                    foreach(var row in customers.Where(row=>row.Id<=1000)){
                        Assert.Equal(Core.TpcCDataGenerator.LastName(row.Id-1),row.Last);
                    }
                    //税と割引の刻み: 仕様は 0.0001 刻み、HammerDB は 0.01 刻み(Q-023、税は 0.10〜0.20、割引は 0.00〜0.50)
                    var taxes=db.For(1).District.EnumerateRows().Select(row=>row.Tax).Append(db.For(1).Warehouse.Get(new Core.WarehouseKey(1)).Tax).ToList();
                    var discounts=customers.Select(row=>row.Discount).ToList();
                    if(mode==Core.TpcCMode.HammerDbMssql){
                        Assert.All(taxes,tax=>Assert.True(tax>=0.10m&&tax<=0.20m&&tax*100m==decimal.Truncate(tax*100m),$"tax {tax}"));
                        Assert.All(discounts,discount=>Assert.True(discount>=0m&&discount<=0.50m&&discount*100m==decimal.Truncate(discount*100m),$"discount {discount}"));
                        Assert.Contains(discounts,discount=>discount!=0m);
                    }else{
                        Assert.Contains(taxes,tax=>tax*100m!=decimal.Truncate(tax*100m));
                    }
                    //I_DATA の印: 仕様は大文字の ORIGINAL、HammerDB は小文字の original(Q-022)
                    var items=db.For(1).Item.EnumerateRows().ToList();
                    var upper=items.Count(row=>row.Data.Contains("ORIGINAL",StringComparison.Ordinal));
                    var lower=items.Count(row=>row.Data.Contains("original",StringComparison.Ordinal));
                    if(mode==Core.TpcCMode.HammerDbMssql){
                        Assert.Equal(0,upper);
                        Assert.InRange(lower,100,300);
                    }else{
                        Assert.Equal(0,lower);
                        Assert.InRange(upper,100,300);
                    }
                    //初期の注文の顧客: 仕様は順列(全員が 1 件)、HammerDB は注文 i の顧客を i..3000 から選ぶので約半数の顧客に注文が無い(Q-025)
                    var withOrders=db.For(1).Order.EnumerateRows().Where(row=>row.DistrictId==4).Select(row=>row.CustomerId).Distinct().Count();
                    if(mode==Core.TpcCMode.HammerDbMssql){
                        Assert.InRange(withOrders,1_300,1_700);
                        Assert.All(db.For(1).Order.EnumerateRows().Where(row=>row.DistrictId==4),row=>Assert.True(row.CustomerId>=row.Id));
                    }else{
                        Assert.Equal(3_000,withOrders);
                    }
                }finally{
                    db.Dispose();
                    store.Dispose();
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void GenerateAmounts(){
        {
            //初期の明細の金額: 仕様は配達済みが 0.00・未配達が 0.01〜9,999.99、HammerDB は逆で配達済みが 0.10〜100.00・未配達が 0(Q-027)。配達日は配達済みだけに入る
            var scale=TestDatabases.Mini(1,1);
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                var (store,db)=TestDatabases.Load(scale,mode);
                try{
                    var lines=db.For(1).OrderLine.EnumerateRows().ToList();
                    var delivered=lines.Where(line=>line.OrderId<scale.FirstInitialNewOrder).ToList();
                    var pending=lines.Where(line=>line.OrderId>=scale.FirstInitialNewOrder).ToList();
                    Assert.NotEmpty(delivered);
                    Assert.NotEmpty(pending);
                    Assert.All(delivered,line=>Assert.NotEqual(0L,line.DeliveryDate));
                    Assert.All(pending,line=>Assert.Equal(0L,line.DeliveryDate));
                    if(mode==Core.TpcCMode.Spec){
                        Assert.All(delivered,line=>Assert.Equal(0m,line.Amount));
                        Assert.All(pending,line=>Assert.InRange(line.Amount,0.01m,9999.99m));
                    }else{
                        Assert.All(delivered,line=>Assert.InRange(line.Amount,0.10m,100.00m));
                        Assert.All(pending,line=>Assert.Equal(0m,line.Amount));
                    }
                }finally{
                    db.Dispose();
                    store.Dispose();
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void GeneratePadding(){
        {
            //HammerDB の形は C_DATA を 500 文字、I_DATA と S_DATA を 50 文字に詰める(固定長の列)。仕様の形は詰めない
            var scale=TestDatabases.Mini(1,1);
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                var (store,db)=TestDatabases.Load(scale,mode);
                try{
                    var customers=db.For(1).Customer.EnumerateRows().ToList();
                    var items=db.For(1).Item.EnumerateRows().ToList();
                    var stocks=db.For(1).Stock.EnumerateRows().ToList();
                    if(mode==Core.TpcCMode.HammerDbMssql){
                        Assert.All(customers,row=>Assert.Equal(500,row.Data.Length));
                        Assert.All(items,row=>Assert.Equal(50,row.Data.Length));
                        Assert.All(stocks,row=>Assert.Equal(50,row.Data.Length));
                    }else{
                        Assert.Contains(customers,row=>row.Data.Length<500);
                        Assert.Contains(items,row=>row.Data.Length<50);
                    }
                }finally{
                    db.Dispose();
                    store.Dispose();
                }
            }
        }
    }
}
