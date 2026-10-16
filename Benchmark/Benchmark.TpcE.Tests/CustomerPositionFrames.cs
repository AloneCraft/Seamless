using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>Customer-Position(TPC-E 3.3.2)の手順の検査。小さな手作りのデータを 4 通りのエンジンに入れて流す。</summary>
public sealed class CustomerPositionFrames{
    private static readonly long Start=TpcETime.FromDateTime(new DateTime(2005,1,3,9,0,0));
    [Fact,Trait("Category","Unit")]public void Run(){
        var outputs=RunEverywhere(Load,[
            new CustomerPositionInput{CustomerId=1,GetHistory=false},
            new CustomerPositionInput{TaxId=Code24.From("C000000000001"),GetHistory=true,AccountIdIndex=2},
            new CustomerPositionInput{CustomerId=1,GetHistory=true,AccountIdIndex=1},
            new CustomerPositionInput{CustomerId=3,GetHistory=true,AccountIdIndex=0},
            new CustomerPositionInput{CustomerId=4,GetHistory=true,AccountIdIndex=0},
            new CustomerPositionInput{CustomerId=5,GetHistory=false},
        ]);
        {
            //Frame 1 → Frame 3: 顧客の項目と、口座ごとの現金残高と資産(保有の無い口座も 0 で返す)。資産の昇順
            //口座 10: 100×20.00 + 50×30.50 = 3525.00、口座 11: 10×20.00 = 200.00、口座 12: 保有なし 0(他の顧客の口座 20 は入らない)
            var output=(CustomerPositionOutput)outputs[0];
            Assert.Equal(0,output.Status);
            Assert.Equal(1,output.CustomerId);
            Assert.Equal("Tanaka",output.LastName);
            Assert.Equal("Taro",output.FirstName);
            Assert.Equal("Q",output.MiddleInitial);
            Assert.Equal("M",output.Gender);
            Assert.Equal(2,output.Tier);
            Assert.Equal(Core.Code8.From("ACTV"),output.CustomerStatusId);
            Assert.Equal(TpcETime.DayNumber(1970,5,6),output.DateOfBirth);
            Assert.Equal(101,output.AddressId);
            Assert.Equal("81",output.Country1);
            Assert.Equal("3",output.Area1);
            Assert.Equal("1234-5678",output.Local1);
            Assert.Null(output.Extension1);
            Assert.Equal("taro@example.com",output.Email1);
            Assert.Null(output.Email2);
            Assert.Equal(3,output.AccountLength);
            Assert.Equal([new CustomerAccountPosition(12,7.25m,0m),new CustomerAccountPosition(11,50.00m,200.00m),new CustomerAccountPosition(10,1000.00m,3525.00m)],output.Accounts);
            Assert.Equal(0,output.HistoryLength);
            Assert.Empty(output.History);
        }
        {
            //納税者番号で引き、Frame 2 で口座一覧の 2 番目(資産の昇順で口座 10)の直近 10 取引(取引 12〜3)の状態の記録を TH_DTS の降順に返す
            //取引 12 は指値で 3 件(PNDG・SBMT・CMPT)、取引 11〜3 は成行で 2 件ずつ → 21 件。取引 1・2 は入らない
            var output=(CustomerPositionOutput)outputs[1];
            Assert.Equal(0,output.Status);
            Assert.Equal(1,output.CustomerId);
            Assert.Equal(3,output.AccountLength);
            Assert.Equal(21,output.HistoryLength);
            var aaaa=Code16.From("AAAA");
            Assert.Equal(new CustomerPositionHistory(12,aaaa,112,"Completed",TradeDts(12)),output.History[0]);
            Assert.Equal(new CustomerPositionHistory(12,aaaa,112,"Submitted",TradeDts(12)-1000),output.History[1]);
            Assert.Equal(new CustomerPositionHistory(12,aaaa,112,"Pending",TradeDts(12)-2000),output.History[2]);
            Assert.Equal(new CustomerPositionHistory(11,aaaa,111,"Completed",TradeDts(11)),output.History[3]);
            Assert.Equal(new CustomerPositionHistory(3,aaaa,103,"Submitted",TradeDts(3)-1000),output.History[20]);
            Assert.DoesNotContain(output.History,h=>h.TradeId<3);
            for(var index=1;index<output.History.Length;index++){
                Assert.True(output.History[index-1].HistoryDts>=output.History[index].HistoryDts);
            }
        }
        {
            //取引の無い口座(口座 11)の履歴は 0 件 → hist_len < 10 で -221(規模が小さいときに起きる正常な結果)
            var output=(CustomerPositionOutput)outputs[2];
            Assert.Equal(-221,output.Status);
            Assert.Equal(3,output.AccountLength);
            Assert.Equal(0,output.HistoryLength);
        }
        {
            //口座の無い顧客は acct_len < 1 で -211。Frame 2 は呼ばない
            var output=(CustomerPositionOutput)outputs[3];
            Assert.Equal(-211,output.Status);
            Assert.Equal(0,output.AccountLength);
            Assert.Empty(output.History);
        }
        {
            //履歴が 30 件を超えるとき(10 取引 × 4 件 = 40 件)は TH_DTS の降順に先頭 30 件(取引 210〜204 の 28 件と取引 203 の新しい 2 件)
            var output=(CustomerPositionOutput)outputs[4];
            Assert.Equal(0,output.Status);
            Assert.Equal(30,output.HistoryLength);
            Assert.Equal(new CustomerPositionHistory(210,Code16.From("AAAA"),210,"Completed",TradeDts(210)),output.History[0]);
            Assert.Equal(new CustomerPositionHistory(203,Code16.From("AAAA"),203,"Active",TradeDts(203)-1000),output.History[29]);
        }
        {
            //口座が 10 を超えるとき(11 口座)は資産の昇順に先頭 10 件。同額(口座 52・53)は口座 ID の昇順で、最も資産の多い口座 60 は入らない
            var output=(CustomerPositionOutput)outputs[5];
            Assert.Equal(0,output.Status);
            Assert.Equal(10,output.AccountLength);
            Assert.Equal([50L,51L,52L,53L,54L,55L,56L,57L,58L,59L],output.Accounts.Select(a=>a.AccountId).ToArray());
            Assert.Equal(new CustomerAccountPosition(52,0m,60.00m),output.Accounts[2]);
            Assert.Equal(new CustomerAccountPosition(53,0m,60.00m),output.Accounts[3]);
            Assert.Equal(new CustomerAccountPosition(59,0m,200.00m),output.Accounts[9]);
        }
    }
    [Fact,Trait("Category","Unit")]public void CustomerPosition(){
        {
            //CE の入力: 納税者番号か顧客 ID のどちらか一方で、実在の顧客を指す。履歴を求めるときだけ口座の添字が口座数の範囲に入る
            var model=new TpcEModel(7,new TpcEScale{Customers=1000});
            var generator=new CeInputGenerator(new CeContext(model));
            var rng=new Core.Rng(13);
            var byTaxId=0;
            var withHistory=0;
            for(var index=0;index<2000;index++){
                var input=generator.CustomerPosition(ref rng);
                long customer;
                if(input.CustomerId==0){
                    byTaxId++;
                    var text=input.TaxId.ToString();
                    Assert.StartsWith("C",text,StringComparison.Ordinal);
                    customer=long.Parse(text[1..],System.Globalization.CultureInfo.InvariantCulture);
                    Assert.Equal(input.TaxId,model.TaxIdOfCustomer(customer));
                }else{
                    Assert.Equal(default,input.TaxId);
                    customer=input.CustomerId;
                }
                Assert.InRange(customer,1,model.CustomerCount);
                if(input.GetHistory){
                    withHistory++;
                    Assert.InRange(input.AccountIdIndex,0,model.AccountCountOf(customer)-1);
                }else{
                    Assert.Equal(-1,input.AccountIdIndex);
                }
            }
            //どちらも 50% 前後(6.4.1)
            Assert.InRange(byTaxId,900,1100);
            Assert.InRange(withHistory,900,1100);
        }
    }
    private static long TradeDts(int trade)=>Start+trade*10_000L;
    /// <summary>
    /// 顧客 1(口座 10・11・12)、顧客 2(口座 20)、顧客 3(口座なし)。口座 10 には取引 1〜12(12 だけ指値)。
    /// </summary>
    private static void Load(ILoadSink sink){
        sink.Load(Customer(1,"C000000000001","Tanaka","Taro"));
        sink.Load(Customer(2,"C000000000002","Suzuki","Hanako"));
        sink.Load(Customer(3,"C000000000003","Sato","Jiro"));
        sink.Load(new CustomerAccountRow(10,1,1,"main",1,1000.00m));
        sink.Load(new CustomerAccountRow(11,1,1,"sub",0,50.00m));
        sink.Load(new CustomerAccountRow(12,1,1,"spare",2,7.25m));
        sink.Load(new CustomerAccountRow(20,1,2,"other",1,999.00m));
        var aaaa=Code16.From("AAAA");
        var aaab=Code16.From("AAAB");
        sink.Load(new HoldingSummaryRow(10,aaaa,100));
        sink.Load(new HoldingSummaryRow(10,aaab,50));
        sink.Load(new HoldingSummaryRow(11,aaaa,10));
        sink.Load(new HoldingSummaryRow(20,aaaa,1000));
        sink.Load(new LastTradeRow(aaaa,Start,20.00m,19.00m,0));
        sink.Load(new LastTradeRow(aaab,Start,30.50m,30.00m,0));
        sink.Load(new StatusTypeRow(Core.Code8.From("CMPT"),"Completed"));
        sink.Load(new StatusTypeRow(Core.Code8.From("ACTV"),"Active"));
        sink.Load(new StatusTypeRow(Core.Code8.From("SBMT"),"Submitted"));
        sink.Load(new StatusTypeRow(Core.Code8.From("PNDG"),"Pending"));
        sink.Load(new StatusTypeRow(Core.Code8.From("CNCL"),"Canceled"));
        var completed=Core.Code8.From("CMPT");
        for(var trade=1;trade<=12;trade++){
            var dts=TradeDts(trade);
            var type="TMB";
            if(trade==12){
                type="TLB";
                sink.Load(new TradeHistoryRow(trade,dts-2000,Core.Code8.From("PNDG")));
            }
            sink.Load(new TradeRow(trade,dts,completed,Core.Code8.From(type),true,aaaa,100+trade,20.00m,10,"Taro Tanaka",20.00m,1.00m,2.00m,0m,false));
            sink.Load(new TradeHistoryRow(trade,dts-1000,Core.Code8.From("SBMT")));
            sink.Load(new TradeHistoryRow(trade,dts,completed));
        }
        // 他の顧客の取引(入らないことの確認用)。
        sink.Load(new TradeRow(13,TradeDts(13),completed,Core.Code8.From("TMB"),true,aaaa,500,20.00m,20,"Hanako Suzuki",20.00m,1.00m,2.00m,0m,false));
        sink.Load(new TradeHistoryRow(13,TradeDts(13),completed));
        // 顧客 4(口座 40): 取引 201〜210 に状態の記録を 4 件ずつ(PNDG・SBMT・ACTV・CMPT)。
        sink.Load(Customer(4,"C000000000004","Ito","Saburo"));
        sink.Load(new CustomerAccountRow(40,1,4,"busy",1,0m));
        for(var trade=201;trade<=210;trade++){
            var dts=TradeDts(trade);
            sink.Load(new TradeRow(trade,dts,completed,Core.Code8.From("TLB"),true,aaaa,trade,20.00m,40,"Saburo Ito",20.00m,1.00m,2.00m,0m,false));
            sink.Load(new TradeHistoryRow(trade,dts-3000,Core.Code8.From("PNDG")));
            sink.Load(new TradeHistoryRow(trade,dts-2000,Core.Code8.From("SBMT")));
            sink.Load(new TradeHistoryRow(trade,dts-1000,Core.Code8.From("ACTV")));
            sink.Load(new TradeHistoryRow(trade,dts,completed));
        }
        // 顧客 5: 口座 50〜60 の 11 口座。口座 50+k は AAAA を数量 [1,2,3,3,5,6,7,8,9,10,11][k] 持つ(資産は ×20.00)。
        sink.Load(Customer(5,"C000000000005","Kato","Shiro"));
        int[] quantities=[1,2,3,3,5,6,7,8,9,10,11];
        for(var k=0;k<quantities.Length;k++){
            sink.Load(new CustomerAccountRow(50+k,1,5,"many",1,0m));
            sink.Load(new HoldingSummaryRow(50+k,aaaa,quantities[k]));
        }
    }
    private static CustomerRow Customer(long id,string taxId,string lastName,string firstName){
        if(id==1){
            return new(id,Code24.From(taxId),Core.Code8.From("ACTV"),lastName,firstName,"Q","M",2,TpcETime.DayNumber(1970,5,6),101,
                "81","3","1234-5678",null,
                "81","90","2222-3333","12",
                null,null,null,null,
                "taro@example.com",null);
        }
        return new(id,Code24.From(taxId),Core.Code8.From("ACTV"),lastName,firstName,null,"F",1,TpcETime.DayNumber(1980,1,2),100+id,
            null,null,null,null,null,null,null,null,null,null,null,null,null,null);
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
