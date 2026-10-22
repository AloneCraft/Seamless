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
    [Fact,Trait("Category","Integration")]public void TradeOrder(){
        {
            //成行買い・指値買い・成行売り(保有の取り崩し)・会社名での銘柄検索・巻き戻し・権限なし。
            //取引 ID と時刻を揃えて、既存の実装とハーネス経由で流し、全表一致・出力一致・市場へ送る注文が正しいこと
            if(!fixture.Available){
                return;
            }
            var db=fixture.Engine!.Database;
            var holding=db.HoldingSummary.EnumerateRows().Where(static row=>row.Quantity>0).OrderBy(static row=>row.AccountId).ThenBy(static row=>row.Symbol).First();
            var holdingAccount=db.CustomerAccount.Get(new CustomerAccountKey(holding.AccountId));
            var holdingCustomer=db.Customer.Get(new CustomerKey(holdingAccount.CustomerId));
            var buyer=db.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).Skip(10).First();
            var buyerCustomer=db.Customer.Get(new CustomerKey(buyer.CustomerId));
            var security=db.Security.EnumerateRows().OrderBy(static row=>row.Symbol).Skip(2).First();
            var company=db.Company.Get(new CompanyKey(security.CompanyId));
            var stranger=db.Customer.EnumerateRows().OrderBy(static row=>row.Id).Last();
            TradeOrderInput Order(CustomerAccountRow account,CustomerRow executor,string type,Code16 symbol,string companyName,Code8 issue,int quantity,decimal price,bool rollBack,long tradeId){
                return new TradeOrderInput{
                    AccountId=account.Id,
                    ExecFirstName=executor.FirstName,
                    ExecLastName=executor.LastName,
                    ExecTaxId=executor.TaxId,
                    CompanyName=companyName,
                    Issue=issue,
                    Symbol=symbol,
                    RequestedPrice=price,
                    TradeQuantity=quantity,
                    TradeTypeId=Code8.From(type),
                    IsLifo=true,
                    RollItBack=rollBack,
                    TradeId=tradeId,
                };
            }
            var cases=new[]{
                Order(buyer,buyerCustomer,"TMB",security.Symbol,"",default,100,0m,false,950_000_000_000_001),
                Order(buyer,buyerCustomer,"TLB",security.Symbol,"",default,200,31.25m,false,950_000_000_000_002),
                Order(holdingAccount,holdingCustomer,"TMS",holding.Symbol,"",default,(int)holding.Quantity,0m,false,950_000_000_000_003),
                Order(buyer,buyerCustomer,"TMB",default,company.Name,security.Issue,50,0m,false,950_000_000_000_004),
                Order(buyer,buyerCustomer,"TMB",security.Symbol,"",default,10,0m,true,950_000_000_000_005),
                Order(buyer,stranger,"TMB",security.Symbol,"",default,10,0m,false,950_000_000_000_006),
            };
            var now=Start+20;
            foreach(var template in cases){
                var request=template with{Now=now};
                var expected=(TradeOrderOutput)fixture.Engine!.Execute(request);
                var input=new byte[Compliance.HarnessHost.SizeOf("TradeOrderTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("TradeOrderTxnOutput")];
                var wrapper=new TradeOrderTxnInput(input);
                wrapper.AcctId=request.AccountId;
                wrapper.ExecFName=request.ExecFirstName;
                wrapper.ExecLName=request.ExecLastName;
                wrapper.ExecTaxId=request.ExecTaxId.ToString();
                wrapper.CoName=request.CompanyName;
                wrapper.Issue=request.Issue.ToString();
                wrapper.Symbol=request.Symbol.ToString();
                wrapper.RequestedPrice=(double)request.RequestedPrice;
                wrapper.TradeQty=request.TradeQuantity;
                wrapper.TradeTypeId=request.TradeTypeId.ToString();
                wrapper.TypeIsMargin=Convert.ToInt32(request.TypeIsMargin);
                wrapper.IsLifo=Convert.ToInt32(request.IsLifo);
                wrapper.RollItBack=Convert.ToInt32(request.RollItBack);
                wrapper.StPendingId="PNDG";
                wrapper.StSubmittedId="SBMT";
                UseClock(fixture.Twin!,now);
                ((IHarnessFrameEngine)fixture.Twin!).NextTradeId=request.TradeId;
                var market=new MarketCollector();
                var status=fixture.Host!.Run(HarnessTransaction.TradeOrder,(IHarnessFrameEngine)fixture.Twin!,market,input,output);
                var result=new TradeOrderTxnOutput(output);
                var label=$"{request.TradeTypeId} {request.AccountId} roll={request.RollItBack}";
                // 意図的な巻き戻しの status は、EGen が EXPECTED_ROLLBACK = 1(警告の符号)と定めている(既存の実装は 0)。
                var expectedStatus=expected.Status;
                if(expected.RolledBack){
                    expectedStatus=1;
                }
                Assert.True(expectedStatus==status,$"{label}: status expected {expectedStatus} but was {status}");
                if(expected.Status==0){
                    Assert.Equal(request.TradeId,result.TradeId);
                    Assert.Equal((double)expected.BuyValue,result.BuyValue);
                    Assert.Equal((double)expected.SellValue,result.SellValue);
                    Assert.Equal((double)expected.TaxAmount,result.TaxAmount);
                }
                AssertSameState(fixture.Engine!,fixture.Twin!);
                // 市場(MEE)へ送る注文: 確定した注文だけ。成行は処理(0)、指値は発動条件の登録(1)。
                if(expected.Status==0&&!expected.RolledBack){
                    Assert.Single(market.Requests);
                    var sent=new TradeRequest(market.Requests[0]);
                    Assert.Equal(request.TradeId,sent.TradeId);
                    var action=1;
                    if(expected.IsMarket){
                        action=0;
                    }
                    Assert.Equal(action,sent.EAction);
                }else{
                    Assert.Empty(market.Requests);
                }
                now++;
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void TradeResult(){
        {
            //指値買い 6 件(新しい保有)と、既存の保有を全部売る成行売り。約定価格を変えて流し、全表一致・出力一致を確かめる。存在しない取引は status が一致すること
            if(!fixture.Available){
                return;
            }
            const long tradeIdBase=960_000_000_000_000;
            this.PlaceOrdersOnBoth(tradeIdBase);
            var db=fixture.Engine!.Database;
            var holding=db.HoldingSummary.EnumerateRows().Where(static row=>row.Quantity>0).OrderBy(static row=>row.AccountId).ThenBy(static row=>row.Symbol).First();
            var holdingAccount=db.CustomerAccount.Get(new CustomerAccountKey(holding.AccountId));
            var holdingCustomer=db.Customer.Get(new CustomerKey(holdingAccount.CustomerId));
            var sellId=tradeIdBase+100;
            var sell=new TradeOrderInput{
                AccountId=holdingAccount.Id,
                ExecFirstName=holdingCustomer.FirstName,
                ExecLastName=holdingCustomer.LastName,
                ExecTaxId=holdingCustomer.TaxId,
                Symbol=holding.Symbol,
                TradeQuantity=(int)holding.Quantity,
                TradeTypeId=Code8.From("TMS"),
                IsLifo=true,
                TradeId=sellId,
                Now=TpcETime.FromDateTime(new DateTime(2026,1,2)),
            };
            Assert.Equal(0,fixture.Engine!.Execute(sell).Status);
            Assert.Equal(0,fixture.Twin!.Execute(sell).Status);
            AssertSameState(fixture.Engine!,fixture.Twin!);
            var trades=new List<(long Id,decimal Price)>();
            for(var index=0;index<6;index++){
                trades.Add((tradeIdBase+index,95.25m+index*3.5m));
            }
            trades.Add((sellId,120.75m));
            trades.Add((tradeIdBase+999,10m));
            var now=TpcETime.FromDateTime(new DateTime(2026,1,3));
            foreach(var (id,price) in trades){
                var expected=(TradeResultOutput)fixture.Engine!.Execute(new TradeResultInput{TradeId=id,TradePrice=price,Now=now});
                var input=new byte[Compliance.HarnessHost.SizeOf("TradeResultTxnInput")];
                var output=new byte[Compliance.HarnessHost.SizeOf("TradeResultTxnOutput")];
                var wrapper=new TradeResultTxnInput(input);
                wrapper.TradeId=id;
                wrapper.TradePrice=(double)price;
                UseClock(fixture.Twin!,now);
                var status=fixture.Host!.Run(HarnessTransaction.TradeResult,(IHarnessFrameEngine)fixture.Twin!,new MarketCollector(),input,output);
                var result=new TradeResultTxnOutput(output);
                var label=$"trade {id}";
                Assert.True(expected.Status==status,$"{label}: status expected {expected.Status} but was {status}");
                if(expected.Status==0){
                    Assert.True((double)expected.AccountBalance==result.AcctBal,$"{label}: acct_bal expected {expected.AccountBalance} but was {result.AcctBal}");
                    Assert.True(result.LoadUnit>=1,$"{label}: load_unit {result.LoadUnit}");
                }
                AssertSameState(fixture.Engine!,fixture.Twin!);
                now++;
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
