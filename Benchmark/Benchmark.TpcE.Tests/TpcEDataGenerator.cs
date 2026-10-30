using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
public sealed class TpcEDataGenerator{
    private static readonly TpcEScale Small=new(){Customers=100,InitialTradeDays=1,MarketDays=20,NewsItemBytes=64};
    private static IReadOnlyDictionary<string,TableDigest> Digests(ulong seed){
        var sink=new CountingLoadSink();
        new Core.TpcEDataGenerator(new TpcEModel(seed,Small)).Generate(sink);
        var result=new System.Collections.Generic.Dictionary<string,TableDigest>(StringComparer.Ordinal);
        foreach(var pair in sink.Tables){
            result.Add(pair.Key,pair.Value.Build());
        }
        return result;
    }
    [Fact,Trait("Category","Unit")]public void Generate(){
        {
            //同じ種なら全表が同じ、違う種なら違う
            var first=Digests(1);
            var second=Digests(1);
            var other=Digests(2);
            Assert.Equal(33,first.Count);
            foreach(var pair in first){
                Assert.Equal(pair.Value,second[pair.Key]);
            }
            Assert.NotEqual(first["trade"],other["trade"]);
        }
        {
            //件数比(2.6.1): 顧客 100 人なら口座は 5 倍前後、会社 50、銘柄 68、ブローカー 1、初期取引は 1 日 5,760 件
            var digests=Digests(1);
            Assert.Equal(100,digests["customer"].Count);
            Assert.InRange(digests["customer_account"].Count,350,650);
            Assert.Equal(50,digests["company"].Count);
            Assert.Equal(68,digests["security"].Count);
            Assert.Equal(68,digests["last_trade"].Count);
            Assert.Equal(68*20,digests["daily_market"].Count);
            Assert.Equal(5760,digests["trade"].Count);
            Assert.Equal(200,digests["customer_taxrate"].Count);
            Assert.Equal(15,digests["charge"].Count);
            Assert.Equal(240,digests["commission_rate"].Count);
            Assert.Equal(320,digests["taxrate"].Count);
            Assert.Equal(14741,digests["zip_code"].Count);
            Assert.Equal(100+50+4,digests["address"].Count);
            Assert.InRange(digests["trade_history"].Count,5760*2,5760*3);
            Assert.InRange(digests["settlement"].Count,5000,5760);
        }
    }
    [Fact,Trait("Category","Integration")]public void GenerateIntoEngines(){
        {
            //1 回の生成を 4 通りへ配ると、全表と全索引のダイジェストが一致する
            var stores=new List<Store>();
            var engines=new List<ITpcEEngine>();
            try{
                foreach(var variant in VariantCatalog.All){
                    var store=Store.CreateTemporary();
                    stores.Add(store);
                    engines.Add(variant.Open(store,Small));
                }
                foreach(var engine in engines){
                    engine.Database.BeginBulkLoad();
                }
                new Core.TpcEDataGenerator(new TpcEModel(1,Small)).Generate(new FanOutLoadSink([..engines.Select(static engine=>engine.Loader)]));
                foreach(var engine in engines){
                    engine.Database.EndBulkLoad();
                }
                var expected=StateDigest.Of(engines[0].Database);
                Assert.DoesNotContain(expected.Tables,static table=>table.Name.EndsWith("#count-mismatch",StringComparison.Ordinal));
                for(var index=1;index<engines.Count;index++){
                    var actual=StateDigest.Of(engines[index].Database);
                    Assert.Empty(expected.Differences(actual));
                }
                // 索引も入っている(口座→取引の総数=取引の件数)。
                var trades=engines[0].Database.Trade.Count;
                Assert.Equal(trades,expected.Indexes.Single(static index=>index.Name=="x07-account-trades").Count);
            }finally{
                foreach(var engine in engines){
                    engine.Dispose();
                }
                foreach(var store in stores){
                    store.Dispose();
                }
            }
        }
    }
}
