using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>
/// 状態を書き換える取引を、同じ初期状態の 2 つのエンジンに流す: 一方は既存の実装、もう一方は EGenTxnHarness(C++)経由。
/// 全表と全索引のダイジェストが一致すること(時刻は揃える)。実データ(EGen)と EGenHarness.dll があるときだけ流れる。
/// </summary>
public sealed class HarnessWriteTransactions(EGenEngineFixture fixture):IClassFixture<EGenEngineFixture>{
    private const long Start=638_000_000_000_000;
    private sealed class MarketCollector:IMarketSink{
        public List<byte[]> Requests{get;}=[];
        public void SendToMarket(ReadOnlySpan<byte> tradeRequest)=>this.Requests.Add(tradeRequest.ToArray());
    }
    /// <summary>ハーネス側の時計を、既存の実装に渡す Now と同じ値にする。</summary>
    private static void UseClock(ITpcEEngine engine,long now){
        ((IHarnessFrameEngine)engine).Clock=()=>now;
    }
    private static void AssertSameState(ITpcEEngine expected,ITpcEEngine actual){
        Assert.Empty(StateDigest.Of(expected.Database).Differences(StateDigest.Of(actual.Database)));
    }
    private void PlaceOrdersOnBoth(long tradeIdBase){
        HarnessSupport.PlaceLimitOrders(fixture.Engine!,6,tradeIdBase);
        HarnessSupport.PlaceLimitOrders(fixture.Twin!,6,tradeIdBase);
        AssertSameState(fixture.Engine!,fixture.Twin!);
    }
    [Fact,Trait("Category","Integration")]public void TradeCleanup(){
        {
            if(!fixture.Available){
                return;
            }
            this.PlaceOrdersOnBoth(920_000_000_000_000);
            var now=Start+1;
            fixture.Engine!.Execute(new TradeCleanupInput{
                Now=now,
                StatusCanceled=Code8.From("CNCL"),
                StatusPending=Code8.From("PNDG"),
                StatusSubmitted=Code8.From("SBMT"),
                TradeId=1,
            });
            var input=new byte[Compliance.HarnessHost.SizeOf("TradeCleanupTxnInput")];
            var output=new byte[Compliance.HarnessHost.SizeOf("TradeCleanupTxnOutput")];
            var wrapper=new TradeCleanupTxnInput(input);
            wrapper.StartTradeId=1;
            wrapper.StCanceledId="CNCL";
            wrapper.StPendingId="PNDG";
            wrapper.StSubmittedId="SBMT";
            UseClock(fixture.Twin!,now);
            var status=fixture.Host!.Run(HarnessTransaction.TradeCleanup,(IHarnessFrameEngine)fixture.Twin!,new MarketCollector(),input,output);
            Assert.Equal(0,status);
            AssertSameState(fixture.Engine!,fixture.Twin!);
            Assert.Equal(0,fixture.Engine!.Database.TradeRequest.Count);
        }
    }
    [Fact,Trait("Category","Integration")]public void MarketFeed(){
        {
            if(!fixture.Available){
                return;
            }
            this.PlaceOrdersOnBoth(930_000_000_000_000);
            var symbols=fixture.Engine!.Database.TradeRequest.EnumerateRows().Select(static row=>row.Symbol).Distinct().OrderBy(static symbol=>symbol).ToArray();
            Assert.NotEmpty(symbols);
            var entries=symbols.Select(static symbol=>new Benchmark.TpcE.Core.TickerEntry(symbol,50.00m,100)).ToArray();
            var now=Start+2;
            var expected=(MarketFeedOutput)fixture.Engine!.Execute(new MarketFeedInput{
                Now=now,
                Entries=entries,
                StatusSubmitted=Code8.From("SBMT"),
                TypeLimitBuy=Code8.From("TLB"),
                TypeLimitSell=Code8.From("TLS"),
                TypeStopLoss=Code8.From("TSL"),
                UniqueSymbols=symbols.Length,
            });
            Assert.NotEmpty(expected.Triggered);
            var input=new byte[Compliance.HarnessHost.SizeOf("MarketFeedTxnInput")];
            var output=new byte[Compliance.HarnessHost.SizeOf("MarketFeedTxnOutput")];
            var wrapper=new MarketFeedTxnInput(input);
            wrapper.UniqueSymbols=symbols.Length;
            var types=wrapper.StatusAndTradeType;
            types.StatusSubmitted="SBMT";
            types.TypeLimitBuy="TLB";
            types.TypeLimitSell="TLS";
            types.TypeStopLoss="TSL";
            for(var index=0;index<entries.Length&&index<20;index++){
                var entry=wrapper.GetEntries(index);
                entry.Symbol=entries[index].Symbol.ToString();
                entry.PriceQuote=(double)entries[index].PriceQuote;
                entry.TradeQty=entries[index].TradeQuantity;
            }
            UseClock(fixture.Twin!,now);
            var market=new MarketCollector();
            var status=fixture.Host!.Run(HarnessTransaction.MarketFeed,(IHarnessFrameEngine)fixture.Twin!,market,input,output);
            Assert.Equal(expected.Status,status);
            AssertSameState(fixture.Engine!,fixture.Twin!);
            // 発動した指値注文は、市場(MEE)へ処理させる注文(eAction=0)として、取引 ID の順に送られる。
            Assert.Equal(expected.Triggered.Length,market.Requests.Count);
            for(var index=0;index<expected.Triggered.Length;index++){
                var request=new TradeRequest(market.Requests[index]);
                Assert.Equal(expected.Triggered[index].TradeId,request.TradeId);
                Assert.Equal(0,request.EAction);
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void TradeUpdate(){
        {
            //frame_to_execute 1〜3 のそれぞれ。書き込み(実行者名・現金取引の名前・取引種別名)の後で、2 つのエンジンの全表が一致すること
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var busiest=db.Trade.EnumerateRows().GroupBy(static row=>row.AccountId).OrderByDescending(static group=>group.Count()).ThenBy(static group=>group.Key).First();
            var symbol=db.Trade.EnumerateRows().GroupBy(static row=>row.Symbol).OrderByDescending(static group=>group.Count()).ThenBy(static group=>group.Key).First().Key;
            var firstIds=db.Trade.EnumerateRows().Select(static row=>row.Id).OrderBy(static id=>id).Skip(100).Take(20).ToArray();
            var start=TpcETime.FromDateTime(new DateTime(2005,1,3));
            var end=TpcETime.FromDateTime(new DateTime(2005,1,4));
            var maxAccount=db.CustomerAccount.EnumerateRows().Max(static row=>row.Id);
            var cases=new[]{
                new TradeUpdateInput{FrameToExecute=1,TradeIds=firstIds,MaxTrades=20,MaxUpdates=2},
                new TradeUpdateInput{FrameToExecute=2,AccountId=busiest.Key,MaxTrades=20,MaxUpdates=20,StartTradeDts=start,EndTradeDts=end},
                new TradeUpdateInput{FrameToExecute=3,Symbol=symbol,MaxAccountId=maxAccount,MaxTrades=20,MaxUpdates=2,StartTradeDts=start,EndTradeDts=end},
            };
            var now=Start+10;
            foreach(var template in cases){
                var request=template with{Now=now};
                var expected=(TradeUpdateOutput)fixture.Engine!.Execute(request);
                var input=new byte[Compliance.HarnessHost.SizeOf("TradeUpdateTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("TradeUpdateTxnOutput")];
                var wrapper=new TradeUpdateTxnInput(input);
                wrapper.FrameToExecute=request.FrameToExecute;
                for(var index=0;index<request.TradeIds.Length;index++){
                    wrapper.SetTradeId(index,request.TradeIds[index]);
                }
                wrapper.AcctId=request.AccountId;
                wrapper.MaxAcctId=request.MaxAccountId;
                wrapper.MaxTrades=request.MaxTrades;
                wrapper.MaxUpdates=request.MaxUpdates;
                wrapper.StartTradeDts=EGenTimestamp.FromMilliseconds(request.StartTradeDts);
                wrapper.EndTradeDts=EGenTimestamp.FromMilliseconds(request.EndTradeDts);
                wrapper.Symbol=request.Symbol.ToString();
                UseClock(fixture.Twin!,now);
                var status=fixture.Host!.Run(HarnessTransaction.TradeUpdate,(IHarnessFrameEngine)fixture.Twin!,new MarketCollector(),input,output);
                var result=new TradeUpdateTxnOutput(output);
                var label=$"frame {request.FrameToExecute}";
                Assert.True(expected.Status==status,$"{label}: status expected {expected.Status} but was {status}");
                Assert.Equal(request.FrameToExecute,result.FrameExecuted);
                Assert.True(expected.NumFound==result.NumFound,$"{label}: num_found expected {expected.NumFound} but was {result.NumFound}");
                Assert.True(expected.NumUpdated==result.NumUpdated,$"{label}: num_updated expected {expected.NumUpdated} but was {result.NumUpdated}");
                AssertSameState(fixture.Engine!,fixture.Twin!);
                now++;
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void DataMaintenance(){
        {
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var account=db.AccountPermission.EnumerateRows().OrderBy(static row=>row.AccountId).First().AccountId;
            var customer=db.Customer.EnumerateRows().OrderBy(static row=>row.Id).First().Id;
            var company=db.Company.EnumerateRows().OrderBy(static row=>row.Id).First().Id;
            var security=db.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First().Symbol;
            var taxrate=db.Taxrate.EnumerateRows().OrderBy(static row=>row.Id).First().Id;
            var inputs = new[]{
                new DataMaintenanceInput{TableName=DataMaintenanceInput.AccountPermission,AccountId=account},
                new DataMaintenanceInput{TableName=DataMaintenanceInput.Customer,CustomerId=customer},
                new DataMaintenanceInput{TableName=DataMaintenanceInput.Company,CompanyId=company},
                new DataMaintenanceInput{TableName=DataMaintenanceInput.Security,Symbol=security},
                new DataMaintenanceInput{TableName=DataMaintenanceInput.DailyMarket,Symbol=security,DayOfMonth=3,VolumeIncrement=7},
                new DataMaintenanceInput{TableName=DataMaintenanceInput.Taxrate,TaxId=taxrate},
            };
            var now=Start+3;
            foreach(var template in inputs){
                var request=template with{Now=now};
                fixture.Engine!.Execute(request);
                var input=new byte[Compliance.HarnessHost.SizeOf("DataMaintenanceTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("DataMaintenanceTxnOutput")];
                var wrapper=new DataMaintenanceTxnInput(input);
                wrapper.AcctId=request.AccountId;
                wrapper.CId=request.CustomerId;
                wrapper.CoId=request.CompanyId;
                wrapper.DayOfMonth=request.DayOfMonth;
                wrapper.VolIncr=request.VolumeIncrement;
                wrapper.Symbol=request.Symbol.ToString();
                wrapper.TableName=request.TableName;
                wrapper.TxId=request.TaxId.ToString();
                UseClock(fixture.Twin!,now);
                var status=fixture.Host!.Run(HarnessTransaction.DataMaintenance,(IHarnessFrameEngine)fixture.Twin!,new MarketCollector(),input,output);
                Assert.Equal(0,status);
                AssertSameState(fixture.Engine!,fixture.Twin!);
                now++;
            }
        }
    }
}
