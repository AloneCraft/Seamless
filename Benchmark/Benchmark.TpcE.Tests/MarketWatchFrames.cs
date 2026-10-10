using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>Market-Watch(TPC-E 3.3.4)の手順の検査。小さな手作りのデータを 4 通りのエンジンに入れて流す。</summary>
public sealed class MarketWatchFrames{
    private static readonly long Day1=TpcETime.DayNumber(2004,12,30);
    private static readonly long Day2=TpcETime.DayNumber(2004,12,31);
    [Fact,Trait("Category","Unit")]public void Run(){
        var outputs=RunEverywhere(Load,[
            new MarketWatchInput{CustomerId=1,StartDate=Day1},
            new MarketWatchInput{IndustryName="Oil Products",StartingCompanyId=1,EndingCompanyId=2,StartDate=Day1},
            new MarketWatchInput{IndustryName="Oil Products",StartingCompanyId=1,EndingCompanyId=5000,StartDate=Day1},
            new MarketWatchInput{AccountId=10,StartDate=Day1},
            new MarketWatchInput{CustomerId=1,AccountId=10,IndustryName="Software",StartingCompanyId=1,EndingCompanyId=5000,StartDate=Day2},
            new MarketWatchInput{StartDate=Day1},
            new MarketWatchInput{AccountId=11,StartDate=Day1},
            new MarketWatchInput{IndustryName="Nothing",StartingCompanyId=1,EndingCompanyId=5000,StartDate=Day1},
        ]);
        {
            //ウォッチリスト: 顧客 1 のリスト 2 つの銘柄 AAAA・AAAB・AAAA(リストをまたぐ重複も数える)
            //開始日 1000×20.00×2 + 500×40.00 = 60000、現在 1000×22.00×2 + 500×38.00 = 63000 → 5.00%
            var output=(MarketWatchOutput)outputs[0];
            Assert.Equal(0,output.Status);
            Assert.Equal(5.00m,output.PctChange);
        }
        {
            //業種: Oil Products の会社のうち ID が 1〜2 の銘柄 AAAA・AAAB(会社 4 の AAAD と業種違いの AAAC は入らない)
            //開始日 20000 + 20000 = 40000、現在 22000 + 19000 = 41000 → 2.50%
            var output=(MarketWatchOutput)outputs[1];
            Assert.Equal(0,output.Status);
            Assert.Equal(2.50m,output.PctChange);
        }
        {
            //業種で会社の範囲を広げると AAAD も入る。開始日 45000、現在 46500 → 3.333…% を小数 2 桁へ丸めて 3.33%
            var output=(MarketWatchOutput)outputs[2];
            Assert.Equal(3.33m,output.PctChange);
        }
        {
            //口座の保有: 口座 10 の AAAC・AAAD。開始日 2000×10.00 + 100×50.00 = 25000、現在 2000×10.50 + 100×55.00 = 26500 → 6.00%
            var output=(MarketWatchOutput)outputs[3];
            Assert.Equal(0,output.Status);
            Assert.Equal(6.00m,output.PctChange);
        }
        {
            //全部を指したときは cust_id が優先。開始日を 12/31 にすると 1000×25.00×2 + 500×40.00 = 70000 → 63000 で -10.00%
            var output=(MarketWatchOutput)outputs[4];
            Assert.Equal(0,output.Status);
            Assert.Equal(-10.00m,output.PctChange);
        }
        {
            //何も指さなければ Frame を呼ばずに -411
            var output=(MarketWatchOutput)outputs[5];
            Assert.Equal(-411,output.Status);
            Assert.Equal(0m,output.PctChange);
        }
        {
            //銘柄が無い(保有の無い口座・無い業種名)なら開始日の時価総額が 0 なので 0%(誤りではない)
            Assert.Equal(new MarketWatchOutput{Status=0,PctChange=0m},outputs[6]);
            Assert.Equal(new MarketWatchOutput{Status=0,PctChange=0m},outputs[7]);
        }
    }
    [Fact,Trait("Category","Unit")]public void MarketWatch(){
        {
            //CE の入力: 指し方はちょうど 1 つ(ウォッチリスト 60%・口座 35%・業種 5% 前後)、開始日は DAILY_MARKET の営業日
            var model=new TpcEModel(7,new TpcEScale{Customers=1000});
            var context=new CeContext(model){MarketDays=model.MarketDays()};
            var generator=new CeInputGenerator(context);
            var rng=new Core.Rng(19);
            var days=new HashSet<long>(context.MarketDays);
            var industries=new HashSet<string>(StringComparer.Ordinal);
            for(var industry=0;industry<TpcEModel.IndustryCount;industry++){
                industries.Add(TpcEModel.IndustryName(industry));
            }
            var byCustomer=0;
            var byAccount=0;
            var byIndustry=0;
            for(var index=0;index<4000;index++){
                var input=generator.MarketWatch(ref rng);
                Assert.Contains(input.StartDate,days);
                if(input.CustomerId!=0){
                    byCustomer++;
                    Assert.Equal(0,input.AccountId);
                    Assert.Equal("",input.IndustryName);
                    Assert.InRange(input.CustomerId,1,model.CustomerCount);
                }else if(input.AccountId!=0){
                    byAccount++;
                    Assert.Equal("",input.IndustryName);
                    var customer=TpcEModel.CustomerOfAccount(input.AccountId);
                    Assert.InRange(input.AccountId-customer*10,0,model.AccountCountOf(customer)-1);
                }else{
                    byIndustry++;
                    Assert.Contains(input.IndustryName,industries);
                    //会社が 5,000 社以下なら全社を覆う幅 5,000 の範囲
                    Assert.Equal(1,input.StartingCompanyId);
                    Assert.Equal(5000,input.EndingCompanyId);
                }
            }
            Assert.InRange(byCustomer,2200,2600);
            Assert.InRange(byAccount,1200,1600);
            Assert.InRange(byIndustry,120,280);
        }
    }
    /// <summary>
    /// 業種 Oil Products(会社 1・2・4)と Software(会社 3)。銘柄 AAAA(会社 1、1,000 株)・AAAB(会社 2、500 株)・AAAC(会社 3、2,000 株)・AAAD(会社 4、100 株)。
    /// 顧客 1 のウォッチリスト 1(AAAA・AAAB)と 2(AAAA)、顧客 2 のリスト 3(AAAD)。口座 10 の保有は AAAC・AAAD。
    /// </summary>
    private static void Load(ILoadSink sink){
        sink.Load(new SectorRow(Core.Code8.From("SA"),"Energy"));
        sink.Load(new SectorRow(Core.Code8.From("SB"),"Technology"));
        sink.Load(new IndustryRow(Core.Code8.From("IA"),"Oil Products",Core.Code8.From("SA")));
        sink.Load(new IndustryRow(Core.Code8.From("IB"),"Software",Core.Code8.From("SB")));
        sink.Load(Company(1,"IA"));
        sink.Load(Company(2,"IA"));
        sink.Load(Company(3,"IB"));
        sink.Load(Company(4,"IA"));
        Symbol(sink,"AAAA",1,1000,20.00m,25.00m,22.00m);
        Symbol(sink,"AAAB",2,500,40.00m,40.00m,38.00m);
        Symbol(sink,"AAAC",3,2000,10.00m,10.00m,10.50m);
        Symbol(sink,"AAAD",4,100,50.00m,50.00m,55.00m);
        sink.Load(new WatchListRow(1,1));
        sink.Load(new WatchListRow(2,1));
        sink.Load(new WatchListRow(3,2));
        sink.Load(new WatchItemRow(1,Code16.From("AAAA")));
        sink.Load(new WatchItemRow(1,Code16.From("AAAB")));
        sink.Load(new WatchItemRow(2,Code16.From("AAAA")));
        sink.Load(new WatchItemRow(3,Code16.From("AAAD")));
        sink.Load(new CustomerAccountRow(10,1,1,"main",1,0m));
        sink.Load(new CustomerAccountRow(11,1,1,"empty",1,0m));
        sink.Load(new HoldingSummaryRow(10,Code16.From("AAAC"),5));
        sink.Load(new HoldingSummaryRow(10,Code16.From("AAAD"),7));
    }
    /// <summary>銘柄 1 つぶん(SECURITY、2 日分の DAILY_MARKET、LAST_TRADE)。</summary>
    private static void Symbol(ILoadSink sink,string text,long companyId,long numOutstanding,decimal closeDay1,decimal closeDay2,decimal price){
        var symbol=Code16.From(text);
        sink.Load(new SecurityRow(symbol,Core.Code8.From("COMMON"),Core.Code8.From("ACTV"),text+" stock",Core.Code8.From("NYSE"),companyId,numOutstanding,0,0,10m,60m,0,5m,0,1m,1m));
        sink.Load(new DailyMarketRow(MarketWatchFrames.Day1,symbol,closeDay1,closeDay1+1m,closeDay1-1m,1000));
        sink.Load(new DailyMarketRow(MarketWatchFrames.Day2,symbol,closeDay2,closeDay2+1m,closeDay2-1m,1000));
        sink.Load(new LastTradeRow(symbol,0,price,price,0));
    }
    private static CompanyRow Company(long id,string industry){
        return new(id,Core.Code8.From("ACTV"),"Company "+id.ToString(System.Globalization.CultureInfo.InvariantCulture),Core.Code8.From(industry),"AAA","Ceo",id,"",0);
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
