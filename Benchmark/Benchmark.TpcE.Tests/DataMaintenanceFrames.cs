using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;
/// <summary>
/// Data-Maintenance(TPC-E 3.3.11)の検査。小さな手作りのデータを 4 通りのエンジンへ入れて 12 表の切り替えを流し、
/// 4 通りの出力と DB のダイジェストが一致すること、仕様メモ(notes-txn-e §3)から手で求めた値になることを確かめる。
/// 型名はほかのテストクラスと重ならないよう Core. で修飾する。
/// </summary>
public sealed class DataMaintenanceFrames{
    private static readonly Core.TpcEScale Scale=new(){Customers=10,MarketDays=30};
    private static readonly long Now1=Core.TpcETime.FromDateTime(new DateTime(2005,1,3,9,30,0));
    private static readonly long Now2=Core.TpcETime.FromDateTime(new DateTime(2005,1,3,9,31,0,250));
    private static readonly Code24 TaxA=Code24.From("C000000000001");
    private static readonly Code24 TaxB=Code24.From("P000000000011-1");
    private static readonly Code24 TaxC=Code24.From("C000000000002");
    private static readonly Code16 Aaaa=Code16.From("AAAA");
    private static readonly Code16 Aaab=Code16.From("AAAB");
    private static readonly Code16 Aaac=Code16.From("AAAC");
    private static readonly Code16 Aaad=Code16.From("AAAD");
    private static readonly Code16 Aaae=Code16.From("AAAE");
    private static readonly Code16 Aaaf=Code16.From("AAAF");
    private static readonly Code16 Aaag=Code16.From("AAAG");
    private const long NewsDts=632_000_000_000_000;
    [Fact,Trait("Category","Unit")]public void Run(){
        var stores=new List<Store>();
        var engines=new List<Core.ITpcEEngine>();
        try{
            foreach(var variant in VariantCatalog.All){
                var store=Store.CreateTemporary();
                stores.Add(store);
                engines.Add(variant.Open(store,DataMaintenanceFrames.Scale));
            }
            foreach(var engine in engines){
                DataMaintenanceFrames.Load(engine.Loader);
            }
            {
                //ACCOUNT_PERMISSION: 降順の先頭の AP_ACL が "1111" 以外なら、その値を持つ行を "1111" にする(他の行・他の口座は変えない)
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.AccountPermission) with{AccountId=11});
                Assert.Equal(1,output.NumUpdated);
                foreach(var engine in engines){
                    var db=engine.Database;
                    Assert.Equal("0001",db.AccountPermission.Get(new(11,DataMaintenanceFrames.TaxA)).Acl.ToString());
                    Assert.Equal("1111",db.AccountPermission.Get(new(11,DataMaintenanceFrames.TaxB)).Acl.ToString());
                    Assert.Equal("1111",db.AccountPermission.Get(new(21,DataMaintenanceFrames.TaxC)).Acl.ToString());
                }
            }
            {
                //ACCOUNT_PERMISSION: 先頭が "1111" なら、"1111" の行を "0011" に戻す
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.AccountPermission) with{AccountId=11});
                Assert.Equal(1,output.NumUpdated);
                foreach(var engine in engines){
                    Assert.Equal("0001",engine.Database.AccountPermission.Get(new(11,DataMaintenanceFrames.TaxA)).Acl.ToString());
                    Assert.Equal("0011",engine.Database.AccountPermission.Get(new(11,DataMaintenanceFrames.TaxB)).Acl.ToString());
                }
            }
            {
                //ADDRESS: 顧客の住所。NULL は "Apt. 10C" 以外なので "Apt. 10C" になり、もう一度で "Apt. 22" になる
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Address) with{CustomerId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal("Apt. 10C",db.Address.Get(new(101)).Line2));
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Address) with{CustomerId=1});
                Assert.Equal(1,output.NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal("Apt. 22",db.Address.Get(new(101)).Line2));
            }
            {
                //ADDRESS: 顧客の住所が "Apt. 10C" なら "Apt. 22"。c_id が 0 なら会社の住所を変える
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Address) with{CustomerId=2});
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Address) with{CompanyId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal("Apt. 22",db.Address.Get(new(102)).Line2);
                    Assert.Equal("Apt. 10C",db.Address.Get(new(201)).Line2);
                    Assert.Equal("Suite 9",db.Address.Get(new(202)).Line2);
                    Assert.Equal("Line one 201",db.Address.Get(new(201)).Line1);
                });
            }
            {
                //COMPANY: CO_SP_RATE は "ABA" 以外なら "ABA"、"ABA" なら "AAA"
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Company) with{CompanyId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal("ABA",db.Company.Get(new(1)).SpRate));
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Company) with{CompanyId=1});
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Company) with{CompanyId=2});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal("AAA",db.Company.Get(new(1)).SpRate);
                    Assert.Equal("ABA",db.Company.Get(new(2)).SpRate);
                    Assert.Equal("Company One",db.Company.Get(new(1)).Name);
                });
            }
            {
                //CUSTOMER: C_EMAIL_2 の ISP を mindspring → earthlink → mindspring と切り替える
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Customer) with{CustomerId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal("taro@mindspring.com",db.Customer.Get(new(1)).Email2));
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Customer) with{CustomerId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal("taro@earthlink.com",db.Customer.Get(new(1)).Email2));
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Customer) with{CustomerId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal("taro@mindspring.com",db.Customer.Get(new(1)).Email2);
                    Assert.Equal("taro@example.com",db.Customer.Get(new(1)).Email1);
                });
            }
            {
                //CUSTOMER: NULL は NULL のまま。"@mindspring.com" ちょうど(長さが 15 を超えない)は mindspring 側の式になる
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Customer) with{CustomerId=2});
                Assert.Equal(1,output.NumUpdated);
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Customer) with{CustomerId=3});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Null(db.Customer.Get(new(2)).Email2);
                    Assert.Equal("@mindspring.com",db.Customer.Get(new(3)).Email2);
                });
            }
            {
                //CUSTOMER_TAXRATE: US5→US1(主キーが変わるので消して足す)、CN2→CN3、CN4→CN1、US1→US2。地域の税率は変えない
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.CustomerTaxrate) with{CustomerId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.False(db.CustomerTaxrate.Contains(new(Code8.From("US5"),1)));
                    Assert.True(db.CustomerTaxrate.Contains(new(Code8.From("US1"),1)));
                    Assert.Equal(new[]{"AB1","US1"},DataMaintenanceFrames.Texts(db.CustomerTaxrates.All(1)));
                });
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.CustomerTaxrate) with{CustomerId=2});
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.CustomerTaxrate) with{CustomerId=3});
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.CustomerTaxrate) with{CustomerId=1});
                Assert.Equal(1,output.NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(new[]{"AB1","US2"},DataMaintenanceFrames.Texts(db.CustomerTaxrates.All(1)));
                    Assert.Equal(new[]{"CN3","XA3"},DataMaintenanceFrames.Texts(db.CustomerTaxrates.All(2)));
                    Assert.Equal(new[]{"AC2","CN1"},DataMaintenanceFrames.Texts(db.CustomerTaxrates.All(3)));
                    Assert.Equal(6,db.CustomerTaxrate.Count);
                });
            }
            {
                //DAILY_MARKET: 日が 22 の行(2004-11-22 月、2004-12-22 水)だけ DM_VOL に足す。他の銘柄・他の日は変えない
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.DailyMarket) with{Symbol=DataMaintenanceFrames.Aaaa,DayOfMonth=22,VolumeIncrement=-7});
                Assert.Equal(2,output.NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,11,22)-7,db.DailyMarket.Get(new(DataMaintenanceFrames.Aaaa,Core.TpcETime.DayNumber(2004,11,22))).Volume);
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,12,22)-7,db.DailyMarket.Get(new(DataMaintenanceFrames.Aaaa,Core.TpcETime.DayNumber(2004,12,22))).Volume);
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,12,23),db.DailyMarket.Get(new(DataMaintenanceFrames.Aaaa,Core.TpcETime.DayNumber(2004,12,23))).Volume);
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,11,22),db.DailyMarket.Get(new(DataMaintenanceFrames.Aaab,Core.TpcETime.DayNumber(2004,11,22))).Volume);
                });
            }
            {
                //DAILY_MARKET: 2004-11-27 は土曜で行が無いので 12-27 の 1 行。31 日は 12 月だけ。12-05 は日曜で 11-05 は範囲外なので 0 行
                Assert.Equal(1,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.DailyMarket) with{Symbol=DataMaintenanceFrames.Aaab,DayOfMonth=27,VolumeIncrement=5}).NumUpdated);
                Assert.Equal(1,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.DailyMarket) with{Symbol=DataMaintenanceFrames.Aaab,DayOfMonth=31,VolumeIncrement=3}).NumUpdated);
                Assert.Equal(0,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.DailyMarket) with{Symbol=DataMaintenanceFrames.Aaab,DayOfMonth=5,VolumeIncrement=3}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,12,27)+5,db.DailyMarket.Get(new(DataMaintenanceFrames.Aaab,Core.TpcETime.DayNumber(2004,12,27))).Volume);
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,12,31)+3,db.DailyMarket.Get(new(DataMaintenanceFrames.Aaab,Core.TpcETime.DayNumber(2004,12,31))).Volume);
                    Assert.Equal(DataMaintenanceFrames.Volume(2004,12,3),db.DailyMarket.Get(new(DataMaintenanceFrames.Aaab,Core.TpcETime.DayNumber(2004,12,3))).Volume);
                });
            }
            {
                //EXCHANGE: どれにも "LAST UPDATED" が無ければ全行の末尾に " LAST UPDATED " と現在日時を足す(NULL は NULL のまま)
                var output=DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Exchange));
                Assert.Equal(4,output.NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal("New York desc LAST UPDATED 2005-01-03 09:30:00.000",db.Exchange.Get(new(Code8.From("NYSE"))).Description);
                    Assert.Equal("NASDAQ desc LAST UPDATED 2005-01-03 09:30:00.000",db.Exchange.Get(new(Code8.From("NASDAQ"))).Description);
                    Assert.Null(db.Exchange.Get(new(Code8.From("AMEX"))).Description);
                    Assert.Equal("Pacific desc LAST UPDATED 2005-01-03 09:30:00.000",db.Exchange.Get(new(Code8.From("PCX"))).Description);
                });
            }
            {
                //EXCHANGE: 付いていれば末尾の日時だけを今の日時に差し替える
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Exchange) with{Now=DataMaintenanceFrames.Now2});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal("New York desc LAST UPDATED 2005-01-03 09:31:00.250",db.Exchange.Get(new(Code8.From("NYSE"))).Description);
                    Assert.Equal("Pacific desc LAST UPDATED 2005-01-03 09:31:00.250",db.Exchange.Get(new(Code8.From("PCX"))).Description);
                    Assert.Equal("New York Stock Exchange",db.Exchange.Get(new(Code8.From("NYSE"))).Name);
                });
            }
            {
                //FINANCIAL: 四半期開始日に 1 日のものがあれば全行 +1 日(2 日になる)、次は 1 日のものが無いので全行 -1 日
                Assert.Equal(20,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Financial) with{CompanyId=1}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(Core.TpcETime.DayNumber(2000,1,2),db.Financial.Get(new(1,2000,1)).QuarterStartDate);
                    Assert.Equal(Core.TpcETime.DayNumber(2004,10,2),db.Financial.Get(new(1,2004,4)).QuarterStartDate);
                    Assert.Equal(Core.TpcETime.DayNumber(2000,1,2),db.Financial.Get(new(2,2000,1)).QuarterStartDate);
                });
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Financial) with{CompanyId=1});
                Assert.Equal(2,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Financial) with{CompanyId=2}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(Core.TpcETime.DayNumber(2000,1,1),db.Financial.Get(new(1,2000,1)).QuarterStartDate);
                    Assert.Equal(Core.TpcETime.DayNumber(2002,7,1),db.Financial.Get(new(1,2002,3)).QuarterStartDate);
                    Assert.Equal(Core.TpcETime.DayNumber(2000,1,1),db.Financial.Get(new(2,2000,1)).QuarterStartDate);
                    Assert.Equal(Core.TpcETime.DayNumber(2000,4,1),db.Financial.Get(new(2,2000,2)).QuarterStartDate);
                    Assert.Equal(1000m,db.Financial.Get(new(1,2000,1)).Revenue);
                });
            }
            {
                //NEWS_ITEM: 会社に結び付いたニュース全部の NI_DTS を 1 日進める(他社のニュースは変えない)
                Assert.Equal(2,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.NewsItem) with{CompanyId=1}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(DataMaintenanceFrames.NewsDts+1+Core.TpcETime.MillisecondsPerDay,db.NewsItem.Get(new(1)).Dts);
                    Assert.Equal(DataMaintenanceFrames.NewsDts+2,db.NewsItem.Get(new(2)).Dts);
                    Assert.Equal(DataMaintenanceFrames.NewsDts+3+Core.TpcETime.MillisecondsPerDay,db.NewsItem.Get(new(3)).Dts);
                    Assert.Equal("Headline 3",db.NewsItem.Get(new(3)).Headline);
                });
            }
            {
                //SECURITY: S_EXCH_DATE を 1 日進める
                Assert.Equal(1,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Security) with{Symbol=DataMaintenanceFrames.Aaac}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(Core.TpcETime.DayNumber(1990,1,2),db.Security.Get(new(DataMaintenanceFrames.Aaac)).ExchangeDate);
                    Assert.Equal(Core.TpcETime.DayNumber(1990,1,1),db.Security.Get(new(DataMaintenanceFrames.Aaab)).ExchangeDate);
                });
            }
            {
                //TAXRATE: " Tax " があれば " tax " に、無ければ " tax " を " Tax " に(大文字小文字を区別する)
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Taxrate) with{TaxId=Code8.From("US1")});
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Taxrate) with{TaxId=Code8.From("AB1")});
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal("Federal tax Rate One",db.Taxrate.Get(new(Code8.From("US1"))).Name);
                    Assert.Equal("Region Tax AB1",db.Taxrate.Get(new(Code8.From("AB1"))).Name);
                    Assert.Equal(0.15m,db.Taxrate.Get(new(Code8.From("US1"))).Rate);
                });
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.Taxrate) with{TaxId=Code8.From("US1")});
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal("Federal Tax Rate One",db.Taxrate.Get(new(Code8.From("US1"))).Name));
            }
            {
                //WATCH_ITEM: [AAAA,AAAC,AAAE,AAAF] の中央(2 番目)AAAC を、次の未登録の AAAD に差し替える(主キーが変わるので消して足す)
                Assert.Equal(1,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.WatchItem) with{CustomerId=1}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(new[]{"AAAA","AAAD","AAAE","AAAF"},DataMaintenanceFrames.Texts(db.WatchListItems.All(1)));
                    Assert.False(db.WatchItem.Contains(new(1,DataMaintenanceFrames.Aaac)));
                });
            }
            {
                //WATCH_ITEM: 中央 AAAD の次は AAAE・AAAF が登録済みなので AAAG
                DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.WatchItem) with{CustomerId=1});
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal(new[]{"AAAA","AAAE","AAAF","AAAG"},DataMaintenanceFrames.Texts(db.WatchListItems.All(1))));
            }
            {
                //WATCH_ITEM: 全銘柄を登録済みなら次の銘柄が無いので何もしない
                Assert.Equal(0,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.WatchItem) with{CustomerId=2}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>Assert.Equal(7,db.WatchListItems.CountOf(2)));
            }
            {
                //WATCH_ITEM: リストが 2 つなら合わせて数えて並べる([AAAB,AAAC,AAAC,AAAD] の 2 番目 AAAC)。両方のリストの AAAC を AAAE にする
                Assert.Equal(2,DataMaintenanceFrames.Execute(engines,DataMaintenanceFrames.Input(Core.DataMaintenanceInput.WatchItem) with{CustomerId=3}).NumUpdated);
                DataMaintenanceFrames.AssertAll(engines,db=>{
                    Assert.Equal(new[]{"AAAB","AAAE"},DataMaintenanceFrames.Texts(db.WatchListItems.All(3)));
                    Assert.Equal(new[]{"AAAD","AAAE"},DataMaintenanceFrames.Texts(db.WatchListItems.All(4)));
                    Assert.Equal(4+7+2+2,db.WatchItem.Count);
                });
            }
        }finally{
            foreach(var engine in engines){
                engine.Dispose();
            }
            foreach(var store in stores){
                store.Dispose();
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void CeInputGeneratorDataMaintenance(){
        var model=new Core.TpcEModel(42,new Core.TpcEScale{Customers=1000});
        var generator=new Core.CeInputGenerator(new Core.CeContext(model){Now=DataMaintenanceFrames.Now1});
        {
            //表の番号は 0〜11 だけ
            var rng=new Rng(1);
            Assert.Throws<ArgumentOutOfRangeException>(()=>generator.DataMaintenance(ref rng,-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>generator.DataMaintenance(ref rng,12));
        }
        {
            //表ごとに使う引数だけが範囲内の値を持ち、使わない引数は 0 か空
            var taxIds=new HashSet<Code8>();
            foreach(var national in Core.TpcEModel.NationalTaxrates){
                taxIds.Add(Code8.From(national));
            }
            for(var index=0;index<Core.TpcEModel.DivisionTaxrateCount;index++){
                taxIds.Add(Core.TpcEModel.DivisionTaxrateId(index));
            }
            Assert.Equal(Core.TpcEModel.TaxrateCount,taxIds.Count);
            var rng=new Rng(7);
            var addressByCustomer=0;
            var negative=0;
            var positive=0;
            for(var round=0;round<1000;round++){
                for(var tableIndex=0;tableIndex<12;tableIndex++){
                    var input=generator.DataMaintenance(ref rng,tableIndex);
                    var expected=new Core.DataMaintenanceInput{TableName=Core.DataMaintenanceInput.TableNames[tableIndex],Now=DataMaintenanceFrames.Now1};
                    switch(input.TableName){
                    case Core.DataMaintenanceInput.AccountPermission:{
                        var customer=Core.TpcEModel.CustomerOfAccount(input.AccountId);
                        Assert.InRange(customer,1,1000);
                        Assert.InRange(input.AccountId%10,0,model.AccountCountOf(customer)-1);
                        expected=expected with{AccountId=input.AccountId};
                        break;
                    }
                    case Core.DataMaintenanceInput.Address:
                        if(input.CustomerId!=0){
                            Assert.InRange(input.CustomerId,1,1000);
                            addressByCustomer++;
                            expected=expected with{CustomerId=input.CustomerId};
                        }else{
                            Assert.InRange(input.CompanyId,1,model.Scale.Companies);
                            expected=expected with{CompanyId=input.CompanyId};
                        }
                        break;
                    case Core.DataMaintenanceInput.Company:
                    case Core.DataMaintenanceInput.Financial:
                    case Core.DataMaintenanceInput.NewsItem:
                        Assert.InRange(input.CompanyId,1,model.Scale.Companies);
                        expected=expected with{CompanyId=input.CompanyId};
                        break;
                    case Core.DataMaintenanceInput.Customer:
                    case Core.DataMaintenanceInput.CustomerTaxrate:
                    case Core.DataMaintenanceInput.WatchItem:
                        Assert.InRange(input.CustomerId,1,1000);
                        expected=expected with{CustomerId=input.CustomerId};
                        break;
                    case Core.DataMaintenanceInput.DailyMarket:
                        Assert.InRange(model.SecurityOf(input.Symbol),1,model.Scale.Securities);
                        Assert.InRange(input.DayOfMonth,1,31);
                        Assert.InRange(input.VolumeIncrement,-100,100);
                        Assert.NotEqual(0,input.VolumeIncrement);
                        if(input.VolumeIncrement<0){
                            negative++;
                        }else{
                            positive++;
                        }
                        expected=expected with{Symbol=input.Symbol,DayOfMonth=input.DayOfMonth,VolumeIncrement=input.VolumeIncrement};
                        break;
                    case Core.DataMaintenanceInput.Exchange:
                        break;
                    case Core.DataMaintenanceInput.Security:
                        Assert.InRange(model.SecurityOf(input.Symbol),1,model.Scale.Securities);
                        expected=expected with{Symbol=input.Symbol};
                        break;
                    case Core.DataMaintenanceInput.Taxrate:
                        Assert.Contains(input.TaxId,taxIds);
                        expected=expected with{TaxId=input.TaxId};
                        break;
                    default:
                        Assert.Fail($"unexpected table {input.TableName}");
                        break;
                    }
                    Assert.Equal(expected,input);
                }
            }
            //ADDRESS は 67% が顧客(1,000 回で 60〜74%)。vol_incr は正負の両方が出る
            Assert.InRange(addressByCustomer,600,740);
            Assert.True(negative>300&&positive>300,$"negative={negative} positive={positive}");
        }
        {
            //同じ乱数の列なら同じ入力
            var left=new Rng(99);
            var right=new Rng(99);
            for(var index=0;index<100;index++){
                Assert.Equal(generator.DataMaintenance(ref left,index%12),generator.DataMaintenance(ref right,index%12));
            }
        }
    }
    private static Core.DataMaintenanceInput Input(string tableName)=>new(){TableName=tableName,Now=DataMaintenanceFrames.Now1};
    /// <summary>4 通りで流し、出力の正規化バイト列と DB のダイジェストが一致することを確かめて、先頭の出力を返す。</summary>
    private static Core.DataMaintenanceOutput Execute(List<Core.ITpcEEngine> engines,Core.DataMaintenanceInput input){
        Core.DataMaintenanceOutput? first=null;
        byte[]? expected=null;
        Core.DatabaseDigest? digest=null;
        foreach(var engine in engines){
            var output=Assert.IsType<Core.DataMaintenanceOutput>(engine.Execute(input));
            var bytes=CanonicalWriter.ToBytes(output);
            var state=Core.StateDigest.Of(engine.Database);
            if(first is null){
                first=output;
                expected=bytes;
                digest=state;
            }else{
                Assert.Equal(expected,bytes);
                Assert.Empty(digest!.Differences(state));
            }
        }
        Assert.NotNull(first);
        Assert.Equal(0,first.Status);
        return first;
    }
    private static void AssertAll(List<Core.ITpcEEngine> engines,Action<Core.TpcEDatabaseBase> check){
        foreach(var engine in engines){
            check(engine.Database);
        }
    }
    private static string[] Texts<T>(T[] values) where T:struct{
        var texts=new string[values.Length];
        for(var index=0;index<values.Length;index++){
            texts[index]=values[index].ToString()!;
        }
        return texts;
    }
    private static long Volume(int year,int month,int day)=>1000+day*10+month;
    /// <summary>手作りのデータ。顧客 3 人・会社 2 社・銘柄 7 つ(AAAA〜AAAG)と、Data-Maintenance が触る表と索引の元になる表。</summary>
    private static void Load(Core.ILoadSink sink){
        // 顧客と住所
        sink.Load(DataMaintenanceFrames.Customer(1,101,"taro@example.com"));
        sink.Load(DataMaintenanceFrames.Customer(2,102,null));
        sink.Load(DataMaintenanceFrames.Customer(3,103,"@mindspring.com"));
        sink.Load(new Core.AddressRow(101,"Line one 101",null,Code16.From("10001"),"USA"));
        sink.Load(new Core.AddressRow(102,"Line one 102","Apt. 10C",Code16.From("10002"),"USA"));
        sink.Load(new Core.AddressRow(103,"Line one 103","Apt. 22",Code16.From("10003"),"USA"));
        sink.Load(new Core.AddressRow(201,"Line one 201","Suite 5",Code16.From("10004"),"USA"));
        sink.Load(new Core.AddressRow(202,"Line one 202","Suite 9",Code16.From("10005"),"USA"));
        // 会社
        sink.Load(new Core.CompanyRow(1,Code8.From("ACTV"),"Company One",Code8.From("AA"),"AAA","Ceo One",201,"First company",Core.TpcETime.DayNumber(1980,1,1)));
        sink.Load(new Core.CompanyRow(2,Code8.From("ACTV"),"Company Two",Code8.From("AB"),"BBB","Ceo Two",202,"Second company",Core.TpcETime.DayNumber(1981,1,1)));
        // 口座の権限(口座 11 は 2 人、口座 21 は 1 人)
        sink.Load(new Core.AccountPermissionRow(11,Code8.From("0001"),DataMaintenanceFrames.TaxA,"Last","First"));
        sink.Load(new Core.AccountPermissionRow(11,Code8.From("0011"),DataMaintenanceFrames.TaxB,"Other","Person"));
        sink.Load(new Core.AccountPermissionRow(21,Code8.From("1111"),DataMaintenanceFrames.TaxC,"Last","Second"));
        // 顧客の税率(国 1 つ + 地域 1 つ)と税率
        sink.Load(new Core.CustomerTaxrateRow(Code8.From("US5"),1));
        sink.Load(new Core.CustomerTaxrateRow(Code8.From("AB1"),1));
        sink.Load(new Core.CustomerTaxrateRow(Code8.From("CN2"),2));
        sink.Load(new Core.CustomerTaxrateRow(Code8.From("XA3"),2));
        sink.Load(new Core.CustomerTaxrateRow(Code8.From("CN4"),3));
        sink.Load(new Core.CustomerTaxrateRow(Code8.From("AC2"),3));
        sink.Load(new Core.TaxrateRow(Code8.From("US1"),"Federal Tax Rate One",0.15m));
        sink.Load(new Core.TaxrateRow(Code8.From("AB1"),"Region tax AB1",0.02m));
        // 銘柄と DAILY_MARKET(AAAA と AAAB の 2004-11-22〜2004-12-31 の平日 = 30 営業日)
        Code16[] symbols=[DataMaintenanceFrames.Aaaa,DataMaintenanceFrames.Aaab,DataMaintenanceFrames.Aaac,DataMaintenanceFrames.Aaad,DataMaintenanceFrames.Aaae,DataMaintenanceFrames.Aaaf,DataMaintenanceFrames.Aaag];
        Code16[] marketSymbols=[DataMaintenanceFrames.Aaaa,DataMaintenanceFrames.Aaab];
        Code16[] firstWatchList=[DataMaintenanceFrames.Aaaa,DataMaintenanceFrames.Aaac,DataMaintenanceFrames.Aaae,DataMaintenanceFrames.Aaaf];
        foreach(var symbol in symbols){
            sink.Load(new Core.SecurityRow(symbol,Code8.From("COMMON"),Code8.From("ACTV"),"Security "+symbol,Code8.From("NYSE"),1,1_000_000,Core.TpcETime.DayNumber(1980,1,1),Core.TpcETime.DayNumber(1990,1,1),10m,30m,Core.TpcETime.DayNumber(2004,6,1),20m,Core.TpcETime.DayNumber(2004,2,1),1m,2m));
        }
        foreach(var symbol in marketSymbols){
            for(var date=new DateOnly(2004,11,22);date<=new DateOnly(2004,12,31);date=date.AddDays(1)){
                if(date.DayOfWeek==DayOfWeek.Saturday||date.DayOfWeek==DayOfWeek.Sunday){
                    continue;
                }
                sink.Load(new Core.DailyMarketRow(date.DayNumber,symbol,25m,26m,24m,DataMaintenanceFrames.Volume(date.Year,date.Month,date.Day)));
            }
        }
        // 取引所(AMEX の説明は NULL)
        sink.Load(new Core.ExchangeRow(Code8.From("NYSE"),"New York Stock Exchange",7,930,1600,"New York desc",301));
        sink.Load(new Core.ExchangeRow(Code8.From("NASDAQ"),"NASDAQ Stock Market",0,930,1600,"NASDAQ desc",302));
        sink.Load(new Core.ExchangeRow(Code8.From("AMEX"),"American Stock Exchange",0,930,1600,null,303));
        sink.Load(new Core.ExchangeRow(Code8.From("PCX"),"Pacific Exchange",0,930,1600,"Pacific desc",304));
        // 決算(会社 1 は 2000〜2004 年の 20 四半期で開始日は 1 日、会社 2 は 2 四半期で開始日は 2 日)
        for(var year=2000;year<=2004;year++){
            for(var quarter=1;quarter<=4;quarter++){
                sink.Load(DataMaintenanceFrames.Financial(1,year,quarter,Core.TpcETime.DayNumber(year,quarter*3-2,1)));
            }
        }
        sink.Load(DataMaintenanceFrames.Financial(2,2000,1,Core.TpcETime.DayNumber(2000,1,2)));
        sink.Load(DataMaintenanceFrames.Financial(2,2000,2,Core.TpcETime.DayNumber(2000,4,2)));
        // ニュース(会社 1 は 1 と 3、会社 2 は 2)
        for(var id=1;id<=3;id++){
            sink.Load(new Core.NewsItemRow(id,"Headline "+id,"Summary "+id,[1,2,3],DataMaintenanceFrames.NewsDts+id,"Source",null));
        }
        sink.Load(new Core.NewsXrefRow(1,1));
        sink.Load(new Core.NewsXrefRow(2,2));
        sink.Load(new Core.NewsXrefRow(3,1));
        // ウォッチリスト(顧客 1 は 1 つ、顧客 2 は全銘柄、顧客 3 は 2 つ)
        sink.Load(new Core.WatchListRow(1,1));
        sink.Load(new Core.WatchListRow(2,2));
        sink.Load(new Core.WatchListRow(3,3));
        sink.Load(new Core.WatchListRow(4,3));
        foreach(var symbol in firstWatchList){
            sink.Load(new Core.WatchItemRow(1,symbol));
        }
        foreach(var symbol in symbols){
            sink.Load(new Core.WatchItemRow(2,symbol));
        }
        sink.Load(new Core.WatchItemRow(3,DataMaintenanceFrames.Aaab));
        sink.Load(new Core.WatchItemRow(3,DataMaintenanceFrames.Aaac));
        sink.Load(new Core.WatchItemRow(4,DataMaintenanceFrames.Aaac));
        sink.Load(new Core.WatchItemRow(4,DataMaintenanceFrames.Aaad));
    }
    private static Core.CustomerRow Customer(long id,long addressId,string? email2){
        return new(id,Code24.From("C"+id.ToString("D12",System.Globalization.CultureInfo.InvariantCulture)),Code8.From("ACTV"),"Last"+id,"First"+id,null,null,1,Core.TpcETime.DayNumber(1970,1,1),addressId,
            "1","212","5550000",null,null,null,null,null,null,null,null,null,
            "taro@example.com",email2);
    }
    private static Core.FinancialRow Financial(long companyId,long year,long quarter,long startDate){
        return new(companyId,year,quarter,startDate,1000m,100m,1.5m,1.4m,0.1m,500m,10_000m,4_000m,1_000_000,1_100_000);
    }
}
