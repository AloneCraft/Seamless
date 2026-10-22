using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance.Tests;
public sealed class SutServer{
    private static readonly TpcEScale Small=new(){Customers=100,InitialTradeDays=1,MarketDays=20,NewsItemBytes=64};
    private static ITpcEEngine OpenLoaded(Store store){
        var engine=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant().Open(store,Small);
        engine.Database.BeginBulkLoad();
        new TpcEDataGenerator(new TpcEModel(1,Small)).Generate(engine.Loader);
        engine.Database.EndBulkLoad();
        return engine;
    }
    private static JsonElement Call(NetworkStream stream,string json){
        Framing.Write(stream,Encoding.UTF8.GetBytes(json));
        var body=Framing.Read(stream);
        Assert.NotNull(body);
        using var document=JsonDocument.Parse(body!);
        return document.RootElement.Clone();
    }
    [Fact,Trait("Category","Integration")]public void ReturnsTriggeredLimitOrdersToTheMarket(){
        {
            //指値の買い注文を出し、条件を満たす相場を Market-Feed で流すと、応答に発動した注文(trade_requests)が載る。
            //運転側がそれを MEE へ送り返さないと、指値注文は永久に約定しない(Trade-Result が Trade-Order の約 6 割で止まる)
            using var store=Store.CreateTemporary();
            using var engine=OpenLoaded(store);
            var directory=Path.Combine(Path.GetTempPath(),"tpce-sut-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try{
                var account=engine.Database.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).First();
                var customer=engine.Database.Customer.Get(new CustomerKey(account.CustomerId));
                var security=engine.Database.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First();
                var company=engine.Database.Company.Get(new CompanyKey(security.CompanyId));
                using var writer=new Compliance.RedoLogWriter(Path.Combine(directory,"redo.log"),0);
                using var server=new Compliance.SutServer(engine,writer,new RedoCheckpoint(),0,static _=>{},new SutOptions());
                server.Start();
                using var client=new TcpClient("127.0.0.1",server.Port);
                var stream=client.GetStream();
                var order="{\"txn\":\"TradeOrder\",\"input\":{\"acct_id\":"+account.Id+",\"exec_f_name\":\""+customer.FirstName+"\",\"exec_l_name\":\""+customer.LastName+"\",\"exec_tax_id\":\""+customer.TaxId+"\",\"co_name\":\""+company.Name+"\",\"issue\":\""+security.Issue+"\",\"symbol\":\""+security.Symbol+"\",\"requested_price\":100.00,\"trade_qty\":100,\"trade_type_id\":\"TLB\",\"type_is_margin\":0,\"is_lifo\":1,\"roll_it_back\":0}}";
                var placed=Call(stream,order);
                Assert.True(placed.GetProperty("ok").GetBoolean(),placed.ToString());
                var request=placed.GetProperty("trade_request");
                Assert.Equal(1,request.GetProperty("action").GetInt32());
                var tradeId=request.GetProperty("trade_id").GetInt64();
                var feed="{\"txn\":\"MarketFeed\",\"input\":{\"unique_symbols\":1,\"status_submitted\":\"SBMT\",\"type_limit_buy\":\"TLB\",\"type_limit_sell\":\"TLS\",\"type_stop_loss\":\"TSL\",\"entries\":[{\"symbol\":\""+security.Symbol+"\",\"price_quote\":50.00,\"trade_qty\":100}]}}";
                var fed=Call(stream,feed);
                Assert.True(fed.GetProperty("ok").GetBoolean(),fed.ToString());
                var triggered=fed.GetProperty("trade_requests");
                Assert.Single(triggered.EnumerateArray());
                Assert.Equal(tradeId,triggered[0].GetProperty("trade_id").GetInt64());
                Assert.Equal(0,triggered[0].GetProperty("action").GetInt32());
            }finally{
                try{
                    Directory.Delete(directory,true);
                }catch(IOException){
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]public void RunsOverTcpAndLogsDurably(){
        {
            //取引を TCP で流し、再実行ログを別のエンジンへ当てると全表と全索引が一致する。応答を返した取引は fsync 済み
            var directory=Path.Combine(Path.GetTempPath(),"tpce-sut-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,"redo.log");
            using var storeA=Store.CreateTemporary();
            using var storeB=Store.CreateTemporary();
            using var a=OpenLoaded(storeA);
            using var b=OpenLoaded(storeB);
            try{
                var account=a.Database.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).First();
                var customer=a.Database.Customer.Get(new CustomerKey(account.CustomerId));
                var security=a.Database.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First();
                var company=a.Database.Company.Get(new CompanyKey(security.CompanyId));
                long tradeId;
                var writer=new Compliance.RedoLogWriter(path,0);
                using(var server=new Compliance.SutServer(a,writer,new RedoCheckpoint(),0,static _=>{},new SutOptions())){
                    server.Start();
                    using var client=new TcpClient("127.0.0.1",server.Port);
                    var stream=client.GetStream();
                    var status=Call(stream,$$$"""{"txn":"TradeStatus","input":{"acct_id":{{{account.Id}}}}}""");
                    Assert.True(status.GetProperty("ok").GetBoolean(),status.ToString());
                    var order=Call(stream,$$$"""
                        {"txn":"TradeOrder","input":{"acct_id":{{{account.Id}}},"exec_f_name":"{{{customer.FirstName}}}","exec_l_name":"{{{customer.LastName}}}","exec_tax_id":"{{{customer.TaxId}}}","co_name":"{{{company.Name}}}","issue":"{{{security.Issue}}}","symbol":"{{{security.Symbol}}}","requested_price":25.00,"trade_qty":100,"trade_type_id":"TMB","type_is_margin":0,"is_lifo":1,"roll_it_back":0}}
                        """);
                    Assert.True(order.GetProperty("ok").GetBoolean(),order.ToString());
                    var request=order.GetProperty("trade_request");
                    tradeId=request.GetProperty("trade_id").GetInt64();
                    Assert.Equal(0,request.GetProperty("action").GetInt32());
                    Assert.Equal(security.Symbol.ToString(),request.GetProperty("symbol").GetString());
                    var result=Call(stream,$$$"""{"txn":"TradeResult","input":{"trade_id":{{{tradeId}}},"trade_price":25.00}}""");
                    Assert.True(result.GetProperty("ok").GetBoolean(),result.ToString());
                    // 壊れた要求は失敗として返り、サーバーは続く。
                    var broken=Call(stream,"""{"txn":"Nope","input":{}}""");
                    Assert.False(broken.GetProperty("ok").GetBoolean());
                    Assert.Equal(1,server.Executed(TxnKind.TradeOrder));
                    Assert.Equal(1,server.Executed(TxnKind.TradeResult));
                }
                writer.Dispose();
                Assert.NotEmpty(StateDigest.Of(a.Database).Differences(StateDigest.Of(b.Database)));
                var recovery=Compliance.RedoLogReader.Replay(b.Database,path,0);
                Assert.True(recovery.AppliedTransactions>=2);
                Assert.Empty(StateDigest.Of(a.Database).Differences(StateDigest.Of(b.Database)));
                Assert.True(b.Database.Trade.Contains(new TradeKey(tradeId)));
            }finally{
                try{
                    Directory.Delete(directory,true);
                }catch(IOException){
                }
            }
        }
    }
}
