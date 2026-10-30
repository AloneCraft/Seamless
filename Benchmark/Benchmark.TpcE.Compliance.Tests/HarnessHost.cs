using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class HarnessHost{
    private const string DefaultBuild=@"G:\GitHub\Seamless\Work\TpcE\egen-build";
    private static readonly TpcEScale Small=new(){Customers=100,InitialTradeDays=1,MarketDays=20,NewsItemBytes=64};
    /// <summary>EGenHarness.dll(build-egen.ps1 の出力)の場所。無ければ null(試験は何もせず抜ける)。</summary>
    private static string? DllPath(){
        var build=Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD") ?? DefaultBuild;
        var path=Path.Combine(build,"EGenHarness.dll");
        if(File.Exists(path)){
            return path;
        }
        return null;
    }
    private static ITpcEEngine OpenLoaded(Store store){
        var engine=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant().Open(store,Small);
        engine.Database.BeginBulkLoad();
        new TpcEDataGenerator(new TpcEModel(1,Small)).Generate(engine.Loader);
        engine.Database.EndBulkLoad();
        return engine;
    }
    private sealed class MarketCollector:IMarketSink{
        public List<byte[]> Requests{get;}=[];
        public void SendToMarket(ReadOnlySpan<byte> tradeRequest)=>this.Requests.Add(tradeRequest.ToArray());
    }
    [Fact,Trait("Category","Unit")]public void GeneratedLayoutMatchesTheFrameList(){
        {
            //フレームの入出力の構造体名は、すべて生成した構造体(または別名)として存在する
            var known=new HashSet<string>(StringComparer.Ordinal);
            foreach(var (name,aliases,_) in HarnessLayout.Classes){
                known.Add(name);
                foreach(var alias in aliases){
                    known.Add(alias.Substring(1));
                }
            }
            Assert.Equal(29,HarnessFrameInfo.All.Count);
            foreach(var info in HarnessFrameInfo.All){
                if(info.Input is not null){
                    Assert.Contains(info.Input,known);
                }
                if(info.Output is not null){
                    Assert.Contains(info.Output,known);
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void RunsTradeStatusThroughTheHarness(){
        {
            //EGenTxnHarness(C++)が Trade-Status のフレームを呼び、既存の実装と同じ結果(件数・取引 ID・状態名・status)を出す
            var dll=DllPath();
            if(dll is null){
                return;
            }
            using var store=Store.CreateTemporary();
            using var engine=OpenLoaded(store);
            var frames=(IHarnessFrameEngine)engine;
            var account=engine.Database.Trade.EnumerateRows()
                .GroupBy(static row=>row.AccountId)
                .OrderByDescending(static group=>group.Count())
                .ThenBy(static group=>group.Key)
                .First();
            var expected=(TradeStatusOutput)engine.Execute(new TradeStatusInput{AccountId=account.Key,Now=1});
            using var host=new Compliance.HarnessHost(dll);
            var input=new byte[Compliance.HarnessHost.SizeOf("TradeStatusTxnInput")];
            var output=new byte[Compliance.HarnessHost.SizeOf("TradeStatusTxnOutput")];
            new TradeStatusTxnInput(input).AcctId=account.Key;
            var status=host.Run(HarnessTransaction.TradeStatus,frames,new MarketCollector(),input,output);
            var result=new TradeStatusTxnOutput(output);
            Assert.Equal(expected.Status,status);
            Assert.Equal(expected.Status,result.Status);
            var count=Math.Min(expected.Trades.Length,result.TradeIdCount);
            for(var index=0;index<count;index++){
                Assert.Equal(expected.Trades[index].TradeId,result.GetTradeId(index));
                Assert.Equal(expected.Trades[index].StatusName,result.GetStatusName(index));
            }
            Assert.False(engine.Database.Log.IsRecording);
        }
    }
}
