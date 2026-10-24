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
            //仕様の形は BC が約 10%、HammerDB の形(仮)は約 50%。最初の 1,000 人(縮小時は全員)の姓は番号順
            var scale=new Core.TpcCScale{Warehouses=1,Items=100,CustomersPerDistrict=3_000};
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
                }finally{
                    db.Dispose();
                    store.Dispose();
                }
            }
        }
    }
}
