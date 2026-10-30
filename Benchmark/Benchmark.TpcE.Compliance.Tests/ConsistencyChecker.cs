using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class ConsistencyChecker{
    private static ITpcEEngine OpenEGen(Store store,string flat,TpcEScale scale){
        var engine=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant().Open(store,scale);
        engine.Database.BeginBulkLoad();
        new Compliance.EGenFlatFileImporter(flat).Import(engine.Loader);
        engine.Database.EndBulkLoad();
        return engine;
    }
    [Fact,Trait("Category","Integration")]public void EGenInitialDataIsConsistent(){
        {
            //仕様 7.3.1.1: EGenLoader が作った初期データは 3 つの一貫性の条件を満たす(取り込みとスキーマの正しさの確認でもある)
            var flat=EGenFixture.Generate(1000,1);
            if(flat is null){
                return;
            }
            try{
                var scale=new TpcEScale{Customers=1000,InitialTradeDays=1,MarketDays=1305,NewsItemBytes=100_000,TsavoriteSegmentBytes=NativeTestProgress.SegmentBytes};
                using var store=Store.CreateTemporary();
                using var engine=OpenEGen(store,flat,scale);
                var results=Compliance.ConsistencyChecker.Check(engine.Database);
                Assert.Equal(3,results.Count);
                foreach(var result in results){
                    Assert.True(result.Passed,$"{result.Clause} {result.Name}: {result.Detail}");
                    Assert.True(result.Checked>0);
                }
            }finally{
                EGenFixture.Delete(flat);
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void DetectsBrokenData(){
        {
            //ブローカーの完了件数と保有の要約をわざとずらすと、該当の条件が不合格になる
            var flat=EGenFixture.Generate(1000,1);
            if(flat is null){
                return;
            }
            try{
                var scale=new TpcEScale{Customers=1000,InitialTradeDays=1,MarketDays=1305,NewsItemBytes=100_000,TsavoriteSegmentBytes=NativeTestProgress.SegmentBytes};
                using var store=Store.CreateTemporary();
                using var engine=OpenEGen(store,flat,scale);
                var db=engine.Database;
                var broker=db.Broker.EnumerateRows().OrderBy(static row=>row.Id).First();
                var summary=db.HoldingSummary.EnumerateRows().OrderBy(static row=>row.AccountId).ThenBy(static row=>row.Symbol).First();
                db.Log.Begin();
                db.Broker.Update(broker with{NumTrades=broker.NumTrades+1});
                db.HoldingSummary.Update(summary with{Quantity=summary.Quantity+7});
                db.Log.Commit();
                var results=Compliance.ConsistencyChecker.Check(db);
                Assert.False(results[0].Passed);
                Assert.True(results[1].Passed);
                Assert.False(results[2].Passed);
                Assert.Contains(broker.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),results[0].Detail);
            }finally{
                EGenFixture.Delete(flat);
            }
        }
    }
}
