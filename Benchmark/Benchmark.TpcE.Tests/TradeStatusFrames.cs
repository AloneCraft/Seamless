using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>Trade-Status(TPC-E 3.3.9)の手順の検査。小さな手作りのデータを 4 通りのエンジンに入れて流す。</summary>
public sealed class TradeStatusFrames{
    private static readonly long Start=TpcETime.FromDateTime(new DateTime(2005,1,3,9,0,0));
    [Fact,Trait("Category","Unit")]public void Run(){
        var outputs=RunEverywhere(Load,[
            new TradeStatusInput{AccountId=10},
            new TradeStatusInput{AccountId=11},
        ]);
        {
            //口座 10 の取引 52 件のうち T_DTS の降順に 50 件(取引 52〜3)。取引 51 と 52 は同時刻なので取引 ID の降順
            var output=(TradeStatusOutput)outputs[0];
            Assert.Equal(0,output.Status);
            Assert.Equal(50,output.NumFound);
            Assert.Equal(50,output.Trades.Length);
            Assert.Equal([52L,51L,50L],output.Trades.Take(3).Select(t=>t.TradeId).ToArray());
            Assert.Equal(3,output.Trades[^1].TradeId);
            //各行は TRADE の項目と、STATUS_TYPE・TRADE_TYPE・SECURITY・EXCHANGE の名前
            Assert.Equal(new TradeStatusEntry(52,TradeDts(52),"Submitted","Market-Sell",Code16.From("AAAA"),152,"Taro Tanaka",5.20m,"Alpha Common","New York Stock Exchange"),output.Trades[0]);
            Assert.Equal(new TradeStatusEntry(51,TradeDts(51),"Pending","Limit-Buy",Code16.From("AAAB"),151,"Taro Tanaka",5.10m,"Beta Common","NASDAQ Stock Market"),output.Trades[1]);
            Assert.Equal(new TradeStatusEntry(50,TradeDts(50),"Completed","Market-Buy",Code16.From("AAAA"),150,"Taro Tanaka",5.00m,"Alpha Common","New York Stock Exchange"),output.Trades[2]);
            //口座の持ち主とブローカー
            Assert.Equal("Tanaka",output.CustomerLastName);
            Assert.Equal("Taro",output.CustomerFirstName);
            Assert.Equal("Zed Brown",output.BrokerName);
        }
        {
            //取引が 50 件に届かない口座は -911(規模が小さいときに起きる正常な結果)。見つかった分は返す
            var output=(TradeStatusOutput)outputs[1];
            Assert.Equal(-911,output.Status);
            Assert.Equal(3,output.NumFound);
            Assert.Equal([103L,102L,101L],output.Trades.Select(t=>t.TradeId).ToArray());
            Assert.Equal("Amy Lee",output.BrokerName);
        }
    }
    [Fact,Trait("Category","Unit")]public void TradeStatus(){
        {
            //CE の入力: 実在の顧客の実在の口座
            var model=new TpcEModel(7,new TpcEScale{Customers=1000});
            var generator=new CeInputGenerator(new CeContext(model));
            var rng=new Core.Rng(17);
            for(var index=0;index<1000;index++){
                var input=generator.TradeStatus(ref rng);
                var customer=TpcEModel.CustomerOfAccount(input.AccountId);
                Assert.InRange(customer,1,model.CustomerCount);
                Assert.InRange(input.AccountId-customer*10,0,model.AccountCountOf(customer)-1);
            }
        }
    }
    private static long TradeDts(int trade){
        if(trade==52){
            return TradeDts(51);
        }
        return Start+trade*1000L;
    }
    /// <summary>口座 10(顧客 1・ブローカー 1)に取引 1〜52、口座 11(顧客 1・ブローカー 2)に取引 101〜103。</summary>
    private static void Load(ILoadSink sink){
        sink.Load(new CustomerRow(1,Code24.From("C000000000001"),Core.Code8.From("ACTV"),"Tanaka","Taro",null,"M",2,0,1,null,null,null,null,null,null,null,null,null,null,null,null,null,null));
        sink.Load(new CustomerAccountRow(10,1,1,"main",1,1000.00m));
        sink.Load(new CustomerAccountRow(11,2,1,"sub",1,10.00m));
        sink.Load(new BrokerRow(1,Core.Code8.From("ACTV"),"Zed Brown",0,0m));
        sink.Load(new BrokerRow(2,Core.Code8.From("ACTV"),"Amy Lee",0,0m));
        sink.Load(new StatusTypeRow(Core.Code8.From("CMPT"),"Completed"));
        sink.Load(new StatusTypeRow(Core.Code8.From("ACTV"),"Active"));
        sink.Load(new StatusTypeRow(Core.Code8.From("SBMT"),"Submitted"));
        sink.Load(new StatusTypeRow(Core.Code8.From("PNDG"),"Pending"));
        sink.Load(new StatusTypeRow(Core.Code8.From("CNCL"),"Canceled"));
        sink.Load(new TradeTypeRow(Core.Code8.From("TMB"),"Market-Buy",false,true));
        sink.Load(new TradeTypeRow(Core.Code8.From("TMS"),"Market-Sell",true,true));
        sink.Load(new TradeTypeRow(Core.Code8.From("TLB"),"Limit-Buy",false,false));
        sink.Load(new TradeTypeRow(Core.Code8.From("TLS"),"Limit-Sell",true,false));
        sink.Load(new TradeTypeRow(Core.Code8.From("TSL"),"Stop-Loss",true,false));
        sink.Load(new ExchangeRow(Core.Code8.From("NYSE"),"New York Stock Exchange",1,930,1600,null,1));
        sink.Load(new ExchangeRow(Core.Code8.From("NASDAQ"),"NASDAQ Stock Market",1,930,1600,null,2));
        sink.Load(Security("AAAA","Alpha Common","NYSE"));
        sink.Load(Security("AAAB","Beta Common","NASDAQ"));
        for(var trade=1;trade<=52;trade++){
            sink.Load(Trade(trade,TradeDts(trade),10));
        }
        for(var trade=101;trade<=103;trade++){
            sink.Load(Trade(trade,Start+trade*1000L,11));
        }
    }
    /// <summary>取引 ID から状態・種別・銘柄を決めた取引。ID を 3 で割った余りで状態と種別(0: PNDG・TLB、1: SBMT・TMS、2: CMPT・TMB)、偶奇で銘柄(偶数 AAAA、奇数 AAAB)、手数料は ID/10。</summary>
    private static TradeRow Trade(int trade,long dts,long accountId){
        string[] statuses=["PNDG","SBMT","CMPT"];
        string[] types=["TLB","TMS","TMB"];
        var symbol="AAAA";
        if(trade%2==1){
            symbol="AAAB";
        }
        return new(trade,dts,Core.Code8.From(statuses[trade%3]),Core.Code8.From(types[trade%3]),true,Code16.From(symbol),100+trade,20.00m,accountId,"Taro Tanaka",null,trade/10m,1.00m,0m,false);
    }
    private static SecurityRow Security(string symbol,string name,string exchange){
        return new(Code16.From(symbol),Core.Code8.From("COMMON"),Core.Code8.From("ACTV"),name,Core.Code8.From(exchange),1,1000,0,0,10m,30m,0,20m,0,1m,1m);
    }
    /// <summary>
    /// 4 通りのエンジンに同じ行を入れて入力を順に流し、4 通りの出力の正規化バイト列が一致することと、DB が変わらないこと(読み取りだけの取引)を確かめて、先頭のエンジンの出力を返す。
    /// </summary>
    private static TxnResult[] RunEverywhere(Action<ILoadSink> load,TxnRequest[] inputs){
        var results=new List<(string Name,TxnResult[] Outputs)>();
        foreach(var variant in VariantCatalog.All){
            using var store=Store.CreateTemporary();
            using var engine=variant.Open(store,new TpcEScale{Customers=10});
            load(engine.Loader);
            var before=StateDigest.Of(engine.Database);
            var outputs=new TxnResult[inputs.Length];
            for(var index=0;index<inputs.Length;index++){
                outputs[index]=engine.Execute(inputs[index]);
            }
            Assert.Empty(before.Differences(StateDigest.Of(engine.Database)));
            results.Add((variant.Name,outputs));
        }
        foreach(var (name,outputs) in results){
            for(var index=0;index<inputs.Length;index++){
                Assert.True(Core.CanonicalWriter.ToBytes(results[0].Outputs[index]).AsSpan().SequenceEqual(Core.CanonicalWriter.ToBytes(outputs[index])),$"{name}: output #{index} differs from {results[0].Name}: {outputs[index]}");
            }
        }
        return results[0].Outputs;
    }
}
