using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class RedoLogReader{
    private static readonly TpcEScale Small=new(){Customers=100,InitialTradeDays=1,MarketDays=20,NewsItemBytes=64};
    private static ITpcEEngine OpenLoaded(Store store){
        var engine=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant().Open(store,Small);
        engine.Database.BeginBulkLoad();
        new TpcEDataGenerator(new TpcEModel(1,Small)).Generate(engine.Loader);
        engine.Database.EndBulkLoad();
        return engine;
    }
    [Fact,Trait("Category","Integration")]public void ReplayReproducesTablesAndIndexes(){
        {
            //同じ初期データから出発して、片方の書き込みをログ経由でもう片方へ当てると、全表と全索引が一致する(巻き戻しを含む)
            var directory=Path.Combine(Path.GetTempPath(),"tpce-redo-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,"redo.log");
            using var storeA=Store.CreateTemporary();
            using var storeB=Store.CreateTemporary();
            using var a=OpenLoaded(storeA);
            using var b=OpenLoaded(storeB);
            try{
                Assert.Empty(StateDigest.Of(a.Database).Differences(StateDigest.Of(b.Database)));
                var observer=new RedoObserver(a.Database);
                a.Database.Log.Observer=observer;
                using(var writer=new Tpc.Server.RedoLogWriter(path,0)){
                    var db=a.Database;
                    var trade=db.Trade.EnumerateRows().OrderBy(static row=>row.Id).First();
                    var other=db.Trade.EnumerateRows().OrderBy(static row=>row.Id).Skip(1).First();
                    // 取引 1: 約定の更新と、履歴・指値注文の追加(索引のある表)。
                    db.Log.Begin();
                    db.Trade.Update(trade with{Quantity=trade.Quantity+1});
                    db.TradeRequest.Insert(new TradeRequestRow(9_000_001,trade.TypeId,trade.Symbol,10,12.34m,1));
                    db.Log.Commit();
                    writer.Append(observer.Seal(1)!);
                    // 取引 2: 書いてから巻き戻す(書き込みと取り消しの両方がログに載る)。
                    db.Log.Begin();
                    db.TradeRequest.Delete(new TradeRequestKey(9_000_001));
                    db.Trade.Update(other with{Quantity=other.Quantity+5});
                    db.Log.Rollback();
                    writer.Append(observer.Seal(2)!);
                    // 取引 3: 削除。
                    db.Log.Begin();
                    db.TradeRequest.Delete(new TradeRequestKey(9_000_001));
                    db.Log.Commit();
                    writer.Append(observer.Seal(3)!);
                    writer.Sync();
                }
                Assert.NotEmpty(StateDigest.Of(a.Database).Differences(StateDigest.Of(b.Database)));
                var recovery=Tpc.Server.RedoLogReader.Replay(b.Database,path,0);
                Assert.Equal(3,recovery.AppliedTransactions);
                Assert.Equal(3,recovery.LastSequence);
                Assert.Empty(StateDigest.Of(a.Database).Differences(StateDigest.Of(b.Database)));
                // チェックポイントより前の分は当て直さない。
                Assert.Equal(0,Tpc.Server.RedoLogReader.Replay(b.Database,path,3).AppliedTransactions);
            }finally{
                try{
                    Directory.Delete(directory,true);
                }catch(IOException){
                }
            }
        }
    }
}
