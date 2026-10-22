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
            HarnessSupport.PlaceLimitOrders(fixture.Engine!,5,910_000_000_000_000);
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
    [Fact,Trait("Category","Integration")]public void TradeLookup(){
        {
            //frame_to_execute 1〜4 のそれぞれを、ハーネス経由と既存の実装で流し、件数・取引の並び・is_cash・状態コードが一致すること
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var busiest=db.Trade.EnumerateRows().GroupBy(static row=>row.AccountId).OrderByDescending(static group=>group.Count()).ThenBy(static group=>group.Key).First();
            var symbol=db.Trade.EnumerateRows().GroupBy(static row=>row.Symbol).OrderByDescending(static group=>group.Count()).ThenBy(static group=>group.Key).First().Key;
            var firstIds=db.Trade.EnumerateRows().Select(static row=>row.Id).OrderBy(static id=>id).Take(20).ToArray();
            var start=TpcETime.FromDateTime(new DateTime(2005,1,3));
            var end=TpcETime.FromDateTime(new DateTime(2005,1,4));
            var maxAccount=db.CustomerAccount.EnumerateRows().Max(static row=>row.Id);
            var history=db.HoldingHistory.EnumerateRows().OrderBy(static row=>row.TradeId).First();
            var historyTrade=db.Trade.Get(new TradeKey(history.TradeId));
            var cases=new[]{
                new TradeLookupInput{FrameToExecute=1,TradeIds=firstIds,MaxTrades=20,Now=1},
                new TradeLookupInput{FrameToExecute=2,AccountId=busiest.Key,MaxTrades=20,StartTradeDts=start,EndTradeDts=end,Now=1},
                new TradeLookupInput{FrameToExecute=3,Symbol=symbol,MaxAccountId=maxAccount,MaxTrades=20,StartTradeDts=start,EndTradeDts=end,Now=1},
                new TradeLookupInput{FrameToExecute=4,AccountId=historyTrade.AccountId,StartTradeDts=historyTrade.Dts,Now=1},
            };
            foreach(var request in cases){
                var expected=(TradeLookupOutput)fixture.Engine!.Execute(request);
                var input=new byte[Compliance.HarnessHost.SizeOf("TradeLookupTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("TradeLookupTxnOutput")];
                var wrapper=new TradeLookupTxnInput(input);
                wrapper.FrameToExecute=request.FrameToExecute;
                for(var index=0;index<request.TradeIds.Length;index++){
                    wrapper.SetTradeId(index,request.TradeIds[index]);
                }
                wrapper.AcctId=request.AccountId;
                wrapper.MaxAcctId=request.MaxAccountId;
                wrapper.MaxTrades=request.MaxTrades;
                wrapper.StartTradeDts=EGenTimestamp.FromMilliseconds(request.StartTradeDts);
                wrapper.EndTradeDts=EGenTimestamp.FromMilliseconds(request.EndTradeDts);
                wrapper.Symbol=request.Symbol.ToString();
                var status=this.Run(HarnessTransaction.TradeLookup,input,output);
                var result=new TradeLookupTxnOutput(output);
                var label=$"frame {request.FrameToExecute}";
                Assert.True(expected.Status==status,$"{label}: status expected {expected.Status} but was {status}");
                Assert.Equal(request.FrameToExecute,result.FrameExecuted);
                Assert.True(expected.NumFound==result.NumFound,$"{label}: num_found expected {expected.NumFound} but was {result.NumFound}");
                if(request.FrameToExecute==4){
                    Assert.Equal(expected.TradeId,result.GetTradeList(0));
                    continue;
                }
                for(var index=0;index<expected.Trades.Length;index++){
                    Assert.Equal(expected.Trades[index].IsCash,result.GetIsCash(index));
                    if(request.FrameToExecute!=1){
                        Assert.Equal(expected.Trades[index].TradeId,result.GetTradeList(index));
                    }else{
                        Assert.Equal(expected.Trades[index].IsMarket,result.GetIsMarket(index));
                    }
                }
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
