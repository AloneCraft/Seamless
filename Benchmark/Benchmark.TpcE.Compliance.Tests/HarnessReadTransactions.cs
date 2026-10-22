using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>
/// EGenTxnHarness(C++)経由で流した読み取りの取引が、既存の実装(<see cref="ITpcEEngine.Execute"/>)と同じ結果を出すこと。
/// 実データ(EGen)と EGenHarness.dll があるときだけ流れる。
/// </summary>
public sealed class HarnessReadTransactions(EGenEngineFixture fixture):IClassFixture<EGenEngineFixture>{
    private sealed class MarketCollector:IMarketSink{
        public List<byte[]> Requests{get;}=[];
        public void SendToMarket(ReadOnlySpan<byte> tradeRequest)=>this.Requests.Add(tradeRequest.ToArray());
    }
    private int Run(HarnessTransaction transaction,Span<byte> input,Span<byte> output){
        return fixture.Host!.Run(transaction,(IHarnessFrameEngine)fixture.Engine!,new MarketCollector(),input,output);
    }
    [Fact,Trait("Category","Integration")]public void BrokerVolume(){
        {
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var brokers=db.Broker.EnumerateRows().OrderBy(static row=>row.Id).Take(5).Select(static row=>row.Name).ToArray();
            // 未約定の注文(TRADE_REQUEST)が無いと量が 0 になるので、指値の買い注文を何件か出しておく。
            HarnessSupport.PlaceLimitOrders(fixture.Engine!,5);
            foreach(var sector in db.Sector.EnumerateRows().OrderBy(static row=>row.Name).Take(3)){
                var expected=(BrokerVolumeOutput)fixture.Engine!.Execute(new BrokerVolumeInput{BrokerList=brokers,SectorName=sector.Name,Now=1});
                var input=new byte[Compliance.HarnessHost.SizeOf("BrokerVolumeTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("BrokerVolumeTxnOutput")];
                var wrapper=new BrokerVolumeTxnInput(input);
                for(var index=0;index<brokers.Length;index++){
                    wrapper.SetBrokerList(index,brokers[index]);
                }
                wrapper.SectorName=sector.Name;
                var status=this.Run(HarnessTransaction.BrokerVolume,input,output);
                var result=new BrokerVolumeTxnOutput(output);
                Assert.Equal(expected.Status,status);
                Assert.Equal(expected.ListLength,result.ListLen);
                for(var index=0;index<expected.Entries.Length;index++){
                    // TxnOutput は volume と list_len だけを返す(ブローカーの名前は Frame1Output にあり、ハーネスは写さない)。
                    Assert.Equal((double)expected.Entries[index].Volume,result.GetVolume(index));
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void MarketWatch(){
        {
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var day=db.DailyMarket.EnumerateRows().Max(static row=>row.Date)-10;
            var account=db.HoldingSummary.EnumerateRows().OrderBy(static row=>row.AccountId).First().AccountId;
            var customer=db.WatchList.EnumerateRows().OrderBy(static row=>row.Id).First().CustomerId;
            foreach(var (accountId,customerId) in new[]{(account,0L),(0L,customer)}){
                var expected=(MarketWatchOutput)fixture.Engine!.Execute(new MarketWatchInput{AccountId=accountId,CustomerId=customerId,StartDate=day,Now=1});
                var input=new byte[Compliance.HarnessHost.SizeOf("MarketWatchTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("MarketWatchTxnOutput")];
                var wrapper=new MarketWatchTxnInput(input);
                wrapper.AcctId=accountId;
                wrapper.CId=customerId;
                wrapper.StartDay=EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(day));
                var status=this.Run(HarnessTransaction.MarketWatch,input,output);
                Assert.Equal(expected.Status,status);
                Assert.Equal((double)expected.PctChange,new MarketWatchTxnOutput(output).PctChange);
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void SecurityDetail(){
        {
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var day=db.DailyMarket.EnumerateRows().Max(static row=>row.Date)-25;
            var symbol=db.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First().Symbol;
            foreach(var lob in new[]{false,true}){
                var expected=(SecurityDetailOutput)fixture.Engine!.Execute(new SecurityDetailInput{Symbol=symbol,StartDay=day,MaxRowsToReturn=20,AccessLobFlag=lob,Now=1});
                var input=new byte[Compliance.HarnessHost.SizeOf("SecurityDetailTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("SecurityDetailTxnOutput")];
                var wrapper=new SecurityDetailTxnInput(input);
                wrapper.Symbol=symbol.ToString();
                wrapper.StartDay=EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(day));
                wrapper.MaxRowsToReturn=20;
                wrapper.AccessLobFlag=lob;
                var status=this.Run(HarnessTransaction.SecurityDetail,input,output);
                var result=new SecurityDetailTxnOutput(output);
                Assert.Equal(expected.Status,status);
                Assert.Equal(expected.LastVolume,result.LastVol);
                Assert.Equal(expected.News.Length,result.NewsLen);
            }
        }
    }
}
