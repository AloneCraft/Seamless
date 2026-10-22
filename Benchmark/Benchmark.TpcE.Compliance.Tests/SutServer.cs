using System.Net.Sockets;
using System.Text.Json;
using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance.Tests;
/// <summary>SUT を TCP で動かす。EGen の実データと EGenHarness.dll があるときだけ流れる。</summary>
public sealed class SutServer(EGenEngineFixture fixture):IClassFixture<EGenEngineFixture>{
    private static JsonElement Call(NetworkStream stream,byte[] body){
        Framing.Write(stream,body);
        var reply=Framing.Read(stream);
        Assert.NotNull(reply);
        using var document=JsonDocument.Parse(reply!);
        return document.RootElement.Clone();
    }
    private static byte[] MarketFeedRequest(Code16 symbol,double price){
        var input=new byte[Compliance.HarnessHost.SizeOf("MarketFeedTxnInput")];
        var wrapper=new MarketFeedTxnInput(input);
        wrapper.UniqueSymbols=1;
        var types=wrapper.StatusAndTradeType;
        types.StatusSubmitted="SBMT";
        types.TypeLimitBuy="TLB";
        types.TypeLimitSell="TLS";
        types.TypeStopLoss="TSL";
        var entry=wrapper.GetEntries(0);
        entry.Symbol=symbol.ToString();
        entry.PriceQuote=price;
        entry.TradeQty=100;
        return Compliance.HarnessRequest.Build(HarnessTransaction.MarketFeed,input);
    }
    [Fact,Trait("Category","Integration")]public void RunsOverTcpAndLogsDurably(){
        {
            //成行の買い・指値の買い・相場による発動・約定・壊れた要求を TCP で流し、再実行ログを別のエンジン(同じ初期状態)へ当てると全表と全索引が一致する。
            //市場へ送る注文(trade_requests)が応答に載る: 成行は処理(0)、指値は発動条件の登録(1)、発動した指値は処理(0)。運転側がこれを MEE へ送り返さないと指値注文は約定しない
            if(!fixture.Available){
                return;
            }
            var directory=Path.Combine(Path.GetTempPath(),"tpce-sut-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,"redo.log");
            var engine=fixture.Engine!;
            var twin=fixture.Twin!;
            try{
                var database=engine.Database;
                var account=database.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).First();
                var customer=database.Customer.Get(new CustomerKey(account.CustomerId));
                var security=database.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First();
                var company=database.Company.Get(new CompanyKey(security.CompanyId));
                long marketTradeId;
                long limitTradeId;
                var writer=new Compliance.RedoLogWriter(path,0);
                using(var server=new Compliance.SutServer(engine,fixture.Host!,writer,new RedoCheckpoint(),0,static _=>{},new SutOptions())){
                    server.Start();
                    using var client=new TcpClient("127.0.0.1",server.Port);
                    var stream=client.GetStream();
                    var statusInput=new byte[Compliance.HarnessHost.SizeOf("TradeStatusTxnInput")];
                    new TradeStatusTxnInput(statusInput).AcctId=account.Id;
                    var status=Call(stream,Compliance.HarnessRequest.Build(HarnessTransaction.TradeStatus,statusInput));
                    Assert.True(status.GetProperty("ok").GetBoolean(),status.ToString());
                    var market=Call(stream,Compliance.HarnessRequest.TradeOrder(account.Id,customer.FirstName,customer.LastName,customer.TaxId.ToString(),company.Name,security.Issue.ToString(),security.Symbol.ToString(),25.00,"TMB",false));
                    Assert.True(market.GetProperty("ok").GetBoolean(),market.ToString());
                    marketTradeId=market.GetProperty("trade_id").GetInt64();
                    Assert.False(market.GetProperty("rolled_back").GetBoolean());
                    var marketRequest=market.GetProperty("trade_requests")[0];
                    Assert.Equal(marketTradeId,marketRequest.GetProperty("trade_id").GetInt64());
                    Assert.Equal(0,marketRequest.GetProperty("action").GetInt32());
                    Assert.Equal(security.Symbol.ToString(),marketRequest.GetProperty("symbol").GetString());
                    var limit=Call(stream,Compliance.HarnessRequest.TradeOrder(account.Id,customer.FirstName,customer.LastName,customer.TaxId.ToString(),company.Name,security.Issue.ToString(),security.Symbol.ToString(),100.00,"TLB",false));
                    Assert.True(limit.GetProperty("ok").GetBoolean(),limit.ToString());
                    limitTradeId=limit.GetProperty("trade_id").GetInt64();
                    Assert.Equal(1,limit.GetProperty("trade_requests")[0].GetProperty("action").GetInt32());
                    var fed=Call(stream,MarketFeedRequest(security.Symbol,50.00));
                    Assert.True(fed.GetProperty("ok").GetBoolean(),fed.ToString());
                    var triggered=fed.GetProperty("trade_requests");
                    Assert.Single(triggered.EnumerateArray());
                    Assert.Equal(limitTradeId,triggered[0].GetProperty("trade_id").GetInt64());
                    Assert.Equal(0,triggered[0].GetProperty("action").GetInt32());
                    var resultInput=new byte[Compliance.HarnessHost.SizeOf("TradeResultTxnInput")];
                    var resultWrapper=new TradeResultTxnInput(resultInput);
                    resultWrapper.TradeId=marketTradeId;
                    resultWrapper.TradePrice=25.00;
                    var result=Call(stream,Compliance.HarnessRequest.Build(HarnessTransaction.TradeResult,resultInput));
                    Assert.True(result.GetProperty("ok").GetBoolean(),result.ToString());
                    Assert.Equal(0,result.GetProperty("status").GetInt32());
                    // 壊れた要求は失敗として返り、サーバーは続く。
                    var broken=Call(stream,[Compliance.SutServer.TransactionMarker,99,0,0,0]);
                    Assert.False(broken.GetProperty("ok").GetBoolean());
                    var units=Call(stream,System.Text.Encoding.UTF8.GetBytes("""{"admin":"load_units"}"""));
                    Assert.Equal(1,units.GetProperty("load_units").GetArrayLength());
                    Assert.Equal(1,units.GetProperty("load_units")[0].GetInt64());
                    Assert.Equal(2,server.Executed(HarnessTransaction.TradeOrder));
                    Assert.Equal(1,server.Executed(HarnessTransaction.TradeResult));
                }
                writer.Dispose();
                Assert.NotEmpty(StateDigest.Of(engine.Database).Differences(StateDigest.Of(twin.Database)));
                var recovery=Compliance.RedoLogReader.Replay(twin.Database,path,0);
                Assert.True(recovery.AppliedTransactions>=4);
                Assert.Empty(StateDigest.Of(engine.Database).Differences(StateDigest.Of(twin.Database)));
                Assert.True(twin.Database.Trade.Contains(new TradeKey(marketTradeId)));
            }finally{
                try{
                    Directory.Delete(directory,true);
                }catch(IOException){
                }
            }
        }
    }
}
