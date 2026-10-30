using Benchmark.TpcE.Core;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>Broker-Volume(TPC-E 3.3.1)の手順の検査。小さな手作りのデータを 4 通りのエンジンに入れて流す。</summary>
public sealed class BrokerVolumeFrames{
    [Fact,Trait("Category","Unit")]public void Run(){
        var outputs=RunEverywhere(Load,[
            new BrokerVolumeInput{BrokerList=["Zed Brown","Amy Lee","Nobody"],SectorName="Energy"},
            new BrokerVolumeInput{BrokerList=["Zed Brown","Amy Lee","Carl Ng"],SectorName="Technology"},
            new BrokerVolumeInput{BrokerList=["Zed Brown","Amy Lee","Carl Ng"],SectorName="Healthcare"},
            new BrokerVolumeInput{BrokerList=["Carl Ng"],SectorName="Technology"},
            new BrokerVolumeInput{BrokerList=["Carl Ng"],SectorName="Energy"},
        ]);
        {
            //部門の銘柄(優先株も含む)と一覧のブローカーに当たる注文だけを、ブローカーごとに TR_QTY×TR_BID_PRICE で合計し、合計の降順に並べる
            //Amy Lee: 200×25.00 + 300×10.35 = 8105.00、Zed Brown: 100×20.50 + 50×21.10 = 3105.00(別部門の注文と一覧外のブローカーの注文は入らない)
            var output=(BrokerVolumeOutput)outputs[0];
            Assert.Equal(0,output.Status);
            Assert.Equal(2,output.ListLength);
            Assert.Equal([new BrokerVolumeEntry("Amy Lee",8105.00m),new BrokerVolumeEntry("Zed Brown",3105.00m)],output.Entries);
        }
        {
            //同額はブローカー名の Ordinal 昇順(ID の順ではない)
            var output=(BrokerVolumeOutput)outputs[1];
            Assert.Equal(0,output.Status);
            Assert.Equal([new BrokerVolumeEntry("Amy Lee",12000.00m),new BrokerVolumeEntry("Zed Brown",12000.00m)],output.Entries);
        }
        {
            //業種の無い部門は 0 件(誤りではない)
            var output=(BrokerVolumeOutput)outputs[2];
            Assert.Equal(0,output.Status);
            Assert.Equal(0,output.ListLength);
            Assert.Empty(output.Entries);
        }
        {
            //一覧のブローカーの注文が部門に無ければ 0 件
            var output=(BrokerVolumeOutput)outputs[3];
            Assert.Equal(0,output.Status);
            Assert.Empty(output.Entries);
        }
        {
            //一覧に 1 人だけでも集計する(Carl Ng の Energy の注文 100×10.00)
            var output=(BrokerVolumeOutput)outputs[4];
            Assert.Equal(0,output.Status);
            Assert.Equal([new BrokerVolumeEntry("Carl Ng",1000.00m)],output.Entries);
        }
    }
    [Fact,Trait("Category","Unit")]public void BrokerVolume(){
        {
            //CE の入力: 互いに異なる実在のブローカー名を 20〜40 件(ブローカーが少ない規模では全員)と、実在の部門名
            var model=new TpcEModel(7,new TpcEScale{Customers=5000});
            var generator=new CeInputGenerator(new CeContext(model));
            var rng=new Core.Rng(11);
            var brokerNames=new HashSet<string>(StringComparer.Ordinal);
            for(var broker=1L;broker<=model.Scale.Brokers;broker++){
                brokerNames.Add(model.BrokerName(broker));
            }
            var sectorNames=new HashSet<string>(StringComparer.Ordinal);
            for(var sector=0;sector<TpcEModel.SectorCount;sector++){
                sectorNames.Add(TpcEModel.SectorName(sector));
            }
            for(var index=0;index<200;index++){
                var input=generator.BrokerVolume(ref rng);
                Assert.InRange(input.BrokerList.Length,TpcESpec.MinBrokerListLength,TpcESpec.MaxBrokerListLength);
                Assert.Equal(input.BrokerList.Length,input.BrokerList.Distinct(StringComparer.Ordinal).Count());
                Assert.All(input.BrokerList,name=>Assert.Contains(name,brokerNames));
                Assert.Contains(input.SectorName,sectorNames);
            }
            var small=new CeInputGenerator(new CeContext(new TpcEModel(7,new TpcEScale{Customers=1000})));
            Assert.Equal(10,small.BrokerVolume(ref rng).BrokerList.Length);
        }
    }
    /// <summary>
    /// 部門 Energy(業種 Oil・Gas)、Technology(業種 Software)、Healthcare(業種なし)。会社 1(Oil)・2(Gas)・3(Software)。
    /// 銘柄 AAAA・AAAA-PA(会社 1)、AAAB(会社 2)、AAAC(会社 3)。ブローカー 1 Zed Brown、2 Amy Lee、3 Carl Ng。
    /// </summary>
    private static void Load(ILoadSink sink){
        sink.Load(new SectorRow(Core.Code8.From("SA"),"Energy"));
        sink.Load(new SectorRow(Core.Code8.From("SB"),"Technology"));
        sink.Load(new SectorRow(Core.Code8.From("SC"),"Healthcare"));
        sink.Load(new IndustryRow(Core.Code8.From("IA"),"Oil",Core.Code8.From("SA")));
        sink.Load(new IndustryRow(Core.Code8.From("IB"),"Gas",Core.Code8.From("SA")));
        sink.Load(new IndustryRow(Core.Code8.From("IC"),"Software",Core.Code8.From("SB")));
        sink.Load(Company(1,"IA"));
        sink.Load(Company(2,"IB"));
        sink.Load(Company(3,"IC"));
        sink.Load(Security("AAAA",1));
        sink.Load(Security("AAAA-PA",1));
        sink.Load(Security("AAAB",2));
        sink.Load(Security("AAAC",3));
        sink.Load(new BrokerRow(1,Core.Code8.From("ACTV"),"Zed Brown",0,0m));
        sink.Load(new BrokerRow(2,Core.Code8.From("ACTV"),"Amy Lee",0,0m));
        sink.Load(new BrokerRow(3,Core.Code8.From("ACTV"),"Carl Ng",0,0m));
        sink.Load(Request(101,"TLB","AAAA",100,20.50m,1));
        sink.Load(Request(102,"TLS","AAAB",200,25.00m,2));
        sink.Load(Request(103,"TSL","AAAA-PA",50,21.10m,1));
        sink.Load(Request(104,"TLB","AAAC",400,30.00m,1));
        sink.Load(Request(105,"TLB","AAAA",100,10.00m,3));
        sink.Load(Request(106,"TLB","AAAB",300,10.35m,2));
        sink.Load(Request(107,"TLS","AAAC",100,120.00m,2));
    }
    private static CompanyRow Company(long id,string industry){
        return new(id,Core.Code8.From("ACTV"),"Company "+id.ToString(System.Globalization.CultureInfo.InvariantCulture),Core.Code8.From(industry),"AAA","Ceo",id,"",0);
    }
    private static SecurityRow Security(string symbol,long companyId){
        return new(Code16.From(symbol),Core.Code8.From("COMMON"),Core.Code8.From("ACTV"),symbol+" stock",Core.Code8.From("NYSE"),companyId,1000,0,0,10m,30m,0,20m,0,1m,1m);
    }
    private static TradeRequestRow Request(long tradeId,string type,string symbol,int quantity,decimal bidPrice,long brokerId){
        return new(tradeId,Core.Code8.From(type),Code16.From(symbol),quantity,bidPrice,brokerId);
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
