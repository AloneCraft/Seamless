using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
using Seamless.Transactions;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class SharedTransactions(EGenEngineFixture fixture):IClassFixture<EGenEngineFixture>{
    [Fact,Trait("Category","Integration")]public async Task NativeTradeOrderUsesSharedWalAndReturnsMarketMessagesAfterCommit(){
        Assert.True(fixture.Available,"Unavailable: EGen is required for the shared transaction acceptance test.");
        var engine=fixture.Engine!;var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.TpcE",Guid.NewGuid().ToString("N"));
        var scale=new TpcEScale{Customers=1000,InitialTradeDays=1,MarketDays=1305,NewsItemBytes=100_000,TsavoriteSegmentBytes=NativeTestProgress.SegmentBytes};
        try{
            ((IHarnessFrameEngine)engine).Clock=static ()=>638_000_000_000_001;((IHarnessFrameEngine)fixture.Twin!).Clock=static ()=>638_000_000_000_001;
            await using(var sut=new Compliance.TpcETransactionalSut(engine,scale,path,Path.Combine(Environment.GetEnvironmentVariable("TPCE_EGEN_BUILD")??@"G:\GitHub\Seamless\Work\TpcE\egen-build","EGenHarness.dll"))){
                var database=engine.Database;var account=database.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).First();
                var customer=database.Customer.Get(new CustomerKey(account.CustomerId));var security=database.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First();var company=database.Company.Get(new CompanyKey(security.CompanyId));
                var body=Compliance.HarnessRequest.TradeOrder(account.Id,customer.FirstName,customer.LastName,customer.TaxId.ToString(),company.Name,security.Issue.ToString(),security.Symbol.ToString(),25,"TMB",false);
                var result=await sut.ExecuteHarnessAsync(HarnessTransaction.TradeOrder,body[5..]);
                var legacyOutput=new byte[Compliance.HarnessHost.SizeOf("TradeOrderTxnOutput")];var legacyMarket=new MarketCollector();
                Assert.Equal(0,fixture.Host!.Run(HarnessTransaction.TradeOrder,(IHarnessFrameEngine)fixture.Twin!,legacyMarket,body[5..],legacyOutput));
                Assert.Equal(legacyOutput,result.Result.Output);Assert.Equal(legacyMarket.Requests.Count,result.Result.MarketRequests.Length);
                for(var index=0;index<legacyMarket.Requests.Count;index++){Assert.Equal(MarketRequestContract.Fields(legacyMarket.Requests[index]),MarketRequestContract.Fields(result.Result.MarketRequests[index]));}
                Assert.Equal(0,result.Result.Status);Assert.NotEmpty(result.Result.MarketRequests);Assert.True(database.Trade.Contains(new TradeKey(result.Result.AllocatedTradeId)));
                Assert.Equal(TransactionOutcomeKind.Committed,sut.GetTransactionOutcome(result.TransactionId).Kind);
                await sut.CheckpointAsync();
            }
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
    private sealed class MarketCollector:IMarketSink{
        public List<byte[]> Requests{get;}=[];
        public void SendToMarket(ReadOnlySpan<byte> request)=>this.Requests.Add(request.ToArray());
    }
}
