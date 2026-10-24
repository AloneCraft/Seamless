namespace Benchmark.TpcC.Tests;
public sealed class TpcCConsistency{
    private static void WithDatabase(Core.TpcCScale scale,Action<Core.TsavoriteTpcCDatabase> action,Core.TpcCMode mode=Core.TpcCMode.Spec){
        var (store,db)=TestDatabases.Load(scale,mode);
        try{
            action(db);
        }finally{
            db.Dispose();
            store.Dispose();
        }
    }
    private static Core.ConsistencyResult Condition(IReadOnlyList<Core.ConsistencyResult> results,int number)=>results.Single(result=>result.Condition==number);
    [Fact,Trait("Category","Integration")]public void Check(){
        {
            //初期データは全部の条件(1〜12、条件 11 は初期状態のみ)に合格する。仕様の形も HammerDB の形も
            foreach(var mode in new[]{Core.TpcCMode.Spec,Core.TpcCMode.HammerDbMssql}){
                WithDatabase(TestDatabases.Mini(3,2),db=>{
                    var results=Core.TpcCConsistency.Check(db,deep:true,initial:true);
                    Assert.Equal(Enumerable.Range(1,12),results.Select(result=>result.Condition));
                    foreach(var result in results){
                        Assert.True(result.Passed,$"{mode} CC{result.Condition}: {result.Detail}");
                        Assert.True(result.Checked>0,$"CC{result.Condition} checked nothing");
                    }
                },mode);
            }
        }
        {
            //deep でなければ条件 1〜4 だけ、initial でなければ条件 11 を調べない
            WithDatabase(TestDatabases.Mini(1,1),db=>{
                Assert.Equal(new[]{1,2,3,4},Core.TpcCConsistency.Check(db,deep:false,initial:false).Select(result=>result.Condition));
                Assert.Equal(new[]{1,2,3,4,11},Core.TpcCConsistency.Check(db,deep:false,initial:true).Select(result=>result.Condition));
            });
        }
        {
            //地区の YTD を直接書き換えると、条件 1・9 が食い違う(条件 8 は W_YTD と履歴の合計が合ったまま)
            WithDatabase(TestDatabases.Mini(2,2),db=>{
                var table=db.For(2).District;
                var row=table.Get(new Core.DistrictKey(2,3));
                TestDatabases.PutRaw(table,row with{Ytd=row.Ytd+1});
                var results=Core.TpcCConsistency.Check(db,deep:true,initial:true);
                Assert.False(Condition(results,1).Passed);
                Assert.False(Condition(results,9).Passed);
                Assert.True(Condition(results,8).Passed);
                Assert.Contains("w=2",Condition(results,1).Detail);
            });
        }
        {
            //D_NEXT_O_ID を進めると条件 2 が食い違う
            WithDatabase(TestDatabases.Mini(1,1),db=>{
                var table=db.For(1).District;
                var row=table.Get(new Core.DistrictKey(1,5));
                TestDatabases.PutRaw(table,row with{NextOrderId=row.NextOrderId+1});
                Assert.False(Condition(Core.TpcCConsistency.Check(db,deep:false,initial:false),2).Passed);
            });
        }
        {
            //NEW-ORDER の途中を 1 件消すと、条件 3 が食い違い、条件 5(配送済みでない注文が NEW-ORDER に無い)も食い違う
            WithDatabase(TestDatabases.Mini(1,1),db=>{
                var scale=db.Scale;
                var middle=scale.FirstInitialNewOrder+2;
                TestDatabases.DeleteRaw(db.For(1).NewOrder,new Core.NewOrderKey(1,4,middle));
                var results=Core.TpcCConsistency.Check(db,deep:true,initial:false);
                Assert.False(Condition(results,3).Passed);
                Assert.False(Condition(results,5).Passed);
                Assert.True(Condition(results,2).Passed);
            });
        }
        {
            //注文明細を 1 件消すと、条件 4・6 が食い違う
            WithDatabase(TestDatabases.Mini(1,1),db=>{
                TestDatabases.DeleteRaw(db.For(1).OrderLine,new Core.OrderLineKey(1,2,7,1));
                var results=Core.TpcCConsistency.Check(db,deep:true,initial:false);
                Assert.False(Condition(results,4).Passed);
                Assert.False(Condition(results,6).Passed);
            });
        }
        {
            //顧客の残高を書き換えると条件 10・12 が食い違う
            WithDatabase(TestDatabases.Mini(1,1),db=>{
                var table=db.For(1).Customer;
                var row=table.Get(new Core.CustomerKey(1,1,1));
                TestDatabases.PutRaw(table,row with{Balance=row.Balance+5});
                var results=Core.TpcCConsistency.Check(db,deep:true,initial:false);
                Assert.False(Condition(results,10).Passed);
                Assert.False(Condition(results,12).Passed);
            });
        }
        {
            //新規注文が 1 件も無い地区でも、条件 2 は D_NEXT_O_ID-1 = max(O_ID) だけを調べて合格し、条件 3 の対象にならない
            WithDatabase(TestDatabases.Mini(1,1),db=>{
                var scale=db.Scale;
                for(var order=scale.FirstInitialNewOrder;order<=scale.InitialOrdersPerDistrict;order++){
                    TestDatabases.DeleteRaw(db.For(1).NewOrder,new Core.NewOrderKey(1,6,order));
                }
                var results=Core.TpcCConsistency.Check(db,deep:false,initial:false);
                Assert.True(Condition(results,2).Passed);
                Assert.True(Condition(results,3).Passed);
                Assert.Equal(9,Condition(results,3).Checked);
            });
        }
    }
}
