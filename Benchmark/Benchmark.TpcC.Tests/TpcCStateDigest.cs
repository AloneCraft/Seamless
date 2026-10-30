namespace Benchmark.TpcC.Tests;
public sealed class TpcCStateDigest{
    [Fact,Trait("Category","Integration")]public void Of(){
        {
            //区画の数に依らない: 同じデータなら P=1 と P=3 で全表と全索引のダイジェストが同じ(ITEM は区画 0 の分だけ数える)
            var (store1,db1)=TestDatabases.Load(TestDatabases.Mini(4,1));
            var (store3,db3)=TestDatabases.Load(TestDatabases.Mini(4,3));
            try{
                var one=Core.TpcCStateDigest.Of(db1);
                var three=Core.TpcCStateDigest.Of(db3);
                Assert.Empty(one.Differences(three));
                Assert.Equal(one.Combined,three.Combined);
                Assert.Equal(9,one.Tables.Count);
                Assert.Equal(3,one.Indexes.Count);
                Assert.Equal(200,one.Tables.Single(table=>table.Name=="item").Count);
                Assert.Equal(200,three.Tables.Single(table=>table.Name=="item").Count);
                Assert.Equal(4*200,three.Tables.Single(table=>table.Name=="stock").Count);
                Assert.DoesNotContain(three.Tables,table=>table.Name.Contains("count-mismatch",StringComparison.Ordinal));
            }finally{
                db1.Dispose();
                db3.Dispose();
                store1.Dispose();
                store3.Dispose();
            }
        }
        {
            //区画ごとに別の Store に置いても、1 つの Store に置いたときと同じダイジェスト
            var scale=TestDatabases.Mini(4,2);
            var (storeShared,shared)=TestDatabases.Load(scale);
            var stores=new List<Seamless.Execution.Store>{Seamless.Execution.Store.CreateTemporary(),Seamless.Execution.Store.CreateTemporary()};
            var separate=new Core.TsavoriteTpcCDatabase(stores,scale);
            try{
                separate.BeginBulkLoad();
                new Core.TpcCDataGenerator(scale,Core.TpcCLoadRules.For(Core.TpcCMode.Spec),1).Generate(new Core.TpcCDatabaseLoadSink<Core.TsavoriteTpcCPartition>(separate));
                separate.EndBulkLoad();
                Assert.Empty(Core.TpcCStateDigest.Of(shared).Differences(Core.TpcCStateDigest.Of(separate)));
            }finally{
                shared.Dispose();
                separate.Dispose();
                storeShared.Dispose();
                foreach(var store in stores){
                    store.Dispose();
                }
            }
        }
        {
            //行を 1 つ書き換えるとその表だけが食い違う
            var (store,db)=TestDatabases.Load(TestDatabases.Mini(2,2));
            try{
                var before=Core.TpcCStateDigest.Of(db);
                var table=db.For(2).Warehouse;
                var row=table.Get(new Core.WarehouseKey(2));
                TestDatabases.PutRaw(table,row with{Ytd=row.Ytd+1});
                var differences=Core.TpcCStateDigest.Of(db).Differences(before);
                Assert.Equal(new[]{"warehouse"},differences);
            }finally{
                db.Dispose();
                store.Dispose();
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void ItemReplicaDifferences(){
        {
            //読み込み直後は ITEM の複製がそろっている。1 つの区画の ITEM を書き換えると、その区画が見つかる
            var (store,db)=TestDatabases.Load(TestDatabases.Mini(4,3));
            try{
                Assert.Empty(Core.TpcCStateDigest.ItemReplicaDifferences(db));
                var table=db.Partition(2).Item;
                var row=table.Get(new Core.ItemKey(7));
                TestDatabases.PutRaw(table,row with{Price=row.Price+1});
                Assert.Equal(new[]{2},Core.TpcCStateDigest.ItemReplicaDifferences(db));
            }finally{
                db.Dispose();
                store.Dispose();
            }
        }
    }
}
