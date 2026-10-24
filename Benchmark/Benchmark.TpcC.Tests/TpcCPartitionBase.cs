namespace Benchmark.TpcC.Tests;
public sealed class TpcCPartitionBase{
    [Fact,Trait("Category","Integration")]public void Indexes(){
        var (store,db)=TestDatabases.Load(TestDatabases.Mini(2,2));
        try{
            var partition=db.For(2);
            {
                //未配送の注文の列は、地区ごとに先頭が最も古い未配送の注文(FirstInitialNewOrder)から末尾の注文まで連続する
                var queue=partition.NewOrderQueue.All(new Core.DistrictOwner(2,7));
                var first=db.Scale.FirstInitialNewOrder;
                var count=db.Scale.InitialOrdersPerDistrict-first+1;
                Assert.Equal(Enumerable.Range(first,count).Select(order=>(long)order),queue);
            }
            {
                //顧客の注文の列の末尾が最新の注文。初期データでは顧客ごとに注文が 1 件
                var orders=partition.CustomerOrders.All(new Core.CustomerOwner(2,3,9,0));
                Assert.Single(orders);
                Assert.Equal(9,partition.Order.Get(new Core.OrderKey(2,3,(int)orders[0])).CustomerId);
                Assert.Equal(orders,partition.CustomerOrders.TopDesc(new Core.CustomerOwner(2,3,9,0),1));
            }
            {
                //姓の索引は名の昇順に並び、同じ姓の顧客を全員返す
                var rows=partition.Customer.EnumerateRows().Where(row=>row.DistrictId==5).ToList();
                var last=rows[0].Last;
                var expected=rows.Where(row=>row.Last==last).OrderBy(row=>row.First,StringComparer.Ordinal).ThenBy(row=>row.Id).Select(row=>(long)row.Id).ToList();
                var entries=partition.CustomerByName.All(new Core.CustomerNameOwner(2,5,Code16.From(last)));
                Assert.Equal(expected,entries.Select(entry=>entry.CustomerId));
                Assert.Equal(entries.OrderBy(entry=>entry).ToList(),entries.ToList());
            }
        }finally{
            db.Dispose();
            store.Dispose();
        }
    }
    [Fact,Trait("Category","Integration")]public void Writes(){
        var (store,db)=TestDatabases.Load(TestDatabases.Mini(2,2));
        try{
            {
                //取引の書き込みは索引に反映され、巻き戻すと表も索引も元に戻る
                var partition=db.For(1);
                var before=Core.TpcCStateDigest.Of(db);
                partition.Log.Begin();
                var next=db.Scale.InitialOrdersPerDistrict+1;
                partition.NewOrder.Insert(new Core.NewOrderRow(1,3,next));
                partition.Order.Insert(new Core.OrderRow(1,3,next,4,0,0,5,1));
                Assert.Equal(next,partition.NewOrderQueue.TopDesc(new Core.DistrictOwner(1,3),1)[0]);
                Assert.Equal(next,partition.CustomerOrders.TopDesc(new Core.CustomerOwner(1,3,4,0),1)[0]);
                partition.Log.Rollback();
                Assert.Empty(Core.TpcCStateDigest.Of(db).Differences(before));
            }
            {
                //未配送の先頭を消すと、列の先頭が次の注文に進む
                var partition=db.For(1);
                var first=db.Scale.FirstInitialNewOrder;
                partition.Log.Begin();
                partition.NewOrder.Delete(new Core.NewOrderKey(1,3,first));
                Assert.Equal(first+1,partition.NewOrderQueue.From(new Core.DistrictOwner(1,3),0,1)[0]);
                partition.Log.Commit();
            }
            {
                //区画ごとに取り消し記録が別なので、別の区画の取引中でも自分の区画は独立している
                var left=db.Partition(0);
                var right=db.Partition(1);
                left.Log.Begin();
                Assert.False(right.Log.IsRecording);
                left.Log.Rollback();
            }
        }finally{
            db.Dispose();
            store.Dispose();
        }
    }
    [Fact,Trait("Category","Unit")]public void Owns(){
        {
            //区画が持つ倉庫の範囲と物理名
            using var store=Seamless.Execution.Store.CreateTemporary();
            using var db=new Core.TsavoriteTpcCDatabase(store,TestDatabases.Mini(5,2));
            //倉庫 5・区画 2 は [1〜3] と [4〜5] に分かれる
            Assert.True(db.Partition(0).Owns(1));
            Assert.True(db.Partition(0).Owns(3));
            Assert.False(db.Partition(0).Owns(4));
            Assert.False(db.Partition(1).Owns(3));
            Assert.True(db.Partition(1).Owns(4));
            Assert.True(db.Partition(1).Owns(5));
            Assert.Equal((1,3),(db.Partition(0).FirstWarehouse,db.Partition(0).LastWarehouse));
            Assert.Equal((4,5),(db.Partition(1).FirstWarehouse,db.Partition(1).LastWarehouse));
            Assert.Equal("p1-customer",db.Partition(1).PhysicalName("customer"));
            Assert.Equal(5,db.Partition(0).WarehouseCount+db.Partition(1).WarehouseCount);
        }
    }
}
