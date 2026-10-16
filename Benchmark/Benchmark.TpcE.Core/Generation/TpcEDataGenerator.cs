namespace Benchmark.TpcE.Core;
/// <summary>初期データの生成の結果(運転側の文脈に渡す)。</summary>
public sealed record GenerationResult(long LastInitialTradeId,long InitialTradeStart,long InitialTradeEnd,long FirstUnfinishedTradeId,int UnfinishedTrades);
/// <summary>
/// 初期データの生成器。全 33 表を受け口へ 1 行ずつ押し出す(全件をメモリに持たない)。
/// 件数比・表の関係は TPC-E 2.6.1、値は <see cref="TpcEModel"/> の合成値。
/// 初期取引は、営業日 9:00〜17:00 に (顧客数/500) 件/秒で注文を出し、約定まで時刻順に模擬する(Trade-Result と同じ式で保有・税・手数料・決済を作る)。
/// メモリに持つのは件数の限られる状態だけ(建玉、残高、ブローカーの集計、進行中の注文)。
/// </summary>
public sealed class TpcEDataGenerator(TpcEModel model){
    private readonly TpcEModel model=model;
    private ulong Seed=>this.model.Seed;
    private TpcEScale Scale=>this.model.Scale;
    public GenerationResult Generate(ILoadSink sink){
        this.FixedTables(sink);
        this.Companies(sink);
        this.Securities(sink);
        this.CustomersFirstPart(sink);
        var simulation=new InitialTrades(this.model,sink);
        var result=simulation.Run();
        simulation.Finish();
        return result;
    }
    // ---- 固定表 ----
    private void FixedTables(ILoadSink sink){
        foreach(var row in FixedData.StatusTypes){
            sink.Load(row);
        }
        foreach(var row in FixedData.TradeTypes){
            sink.Load(row);
        }
        for(var index=0;index<TpcEModel.SectorCount;index++){
            sink.Load(new SectorRow(TpcEModel.SectorId(index),TpcEModel.SectorName(index)));
        }
        for(var index=0;index<TpcEModel.IndustryCount;index++){
            sink.Load(new IndustryRow(TpcEModel.IndustryId(index),TpcEModel.IndustryName(index),TpcEModel.SectorId(TpcEModel.SectorOfIndustry(index))));
        }
        var symbolsPerExchange=new int[TpcEModel.Exchanges.Length];
        for(var security=1L;security<=this.Scale.Securities;security++){
            symbolsPerExchange[FixedData.ExchangeIndex(this.model.ExchangeOfSecurity(security))]++;
        }
        int[] opens=[930,930,930,630];
        int[] closes=[1600,1600,1600,1300];
        for(var index=0;index<TpcEModel.Exchanges.Length;index++){
            var addressId=this.model.ExchangeAddressId(index);
            sink.Load(this.Address(addressId,"exchange",index));
            sink.Load(new ExchangeRow(TpcEModel.Exchanges[index],TpcEModel.ExchangeNames[index],symbolsPerExchange[index],opens[index],closes[index],SyntheticText.Sentence(Rng.Keyed(this.Seed,"exchange-desc",index),100),addressId));
        }
        for(var typeIndex=0;typeIndex<FixedData.TradeTypes.Length;typeIndex++){
            for(var tier=1;tier<=3;tier++){
                sink.Load(new ChargeRow(FixedData.TradeTypes[typeIndex].Id,tier,FixedData.Charge(typeIndex,tier)));
            }
        }
        for(var tier=1;tier<=3;tier++){
            for(var typeIndex=0;typeIndex<FixedData.TradeTypes.Length;typeIndex++){
                for(var exchange=0;exchange<TpcEModel.Exchanges.Length;exchange++){
                    for(var band=0;band<FixedData.CommissionBands.Length;band++){
                        var (from,to)=FixedData.CommissionBands[band];
                        sink.Load(new CommissionRateRow(tier,FixedData.TradeTypes[typeIndex].Id,TpcEModel.Exchanges[exchange],from,to,FixedData.CommissionRate(tier,typeIndex,exchange,band)));
                    }
                }
            }
        }
        for(var index=0;index<TpcEModel.TaxrateCount;index++){
            sink.Load(FixedData.Taxrate(index));
        }
        for(var index=0;index<TpcEModel.ZipCodeCount;index++){
            sink.Load(new ZipCodeRow(TpcEModel.ZipCode(index),SyntheticText.LastName(Rng.Keyed(this.Seed,"zip-town",index))+" Town","Division "+(index%60).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
    private AddressRow Address(long id,string domain,long key){
        var rng=new Rng(Rng.Keyed(this.Seed,"address-"+domain,key));
        var line1=rng.Range(1,9999).ToString(System.Globalization.CultureInfo.InvariantCulture)+" "+SyntheticText.LastName(rng.NextUInt64())+" Street";
        string? line2=null;
        if(rng.Percent(50)){
            line2="Apt. "+rng.Range(1,99).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        string country;
        if(rng.Percent(80)){
            country="USA";
        }else{
            country="CANADA";
        }
        return new(id,line1,line2,TpcEModel.ZipCode(rng.Range(0,TpcEModel.ZipCodeCount-1)),country);
    }
    // ---- 会社(住所・会社・競合・財務・ニュース) ----
    private void Companies(ILoadSink sink){
        string[] ratings=["AAA","AA+","AA","AA-","A+","A","A-","BBB+","BBB","BBB-","BB+","BB","BB-","B+","B","B-","CCC"];
        var companies=this.Scale.Companies;
        for(var company=1L;company<=companies;company++){
            var rng=new Rng(Rng.Keyed(this.Seed,"company",company));
            var addressId=this.model.CompanyAddressId(company);
            sink.Load(this.Address(addressId,"company",company));
            var openDate=TpcETime.DayNumber(1800,1,1)+rng.Range(0,73000);
            sink.Load(new CompanyRow(company,FixedData.Active,this.model.CompanyName(company),this.model.IndustryOfCompany(company),ratings[rng.Range(0,ratings.Length-1)],SyntheticText.FirstName(rng.NextUInt64())+" "+SyntheticText.LastName(rng.NextUInt64()),addressId,SyntheticText.Sentence(rng.NextUInt64(),140),openDate));
            // 競合 3 社(自分以外、重複なし)。
            var competitors=new HashSet<long>();
            while(competitors.Count<Math.Min(3,companies-1)){
                var other=rng.Range(1,companies);
                if(other!=company&&competitors.Add(other)){
                    sink.Load(new CompanyCompetitorRow(company,other,this.model.IndustryOfCompany(other)));
                }
            }
            // 財務 20 四半期(2000Q1〜2004Q4、2.2.6.5)。
            for(var year=2000;year<=2004;year++){
                for(var quarter=1;quarter<=4;quarter++){
                    var revenue=rng.Range(10_000_000,5_000_000_000)/100m;
                    var net=decimal.Round(revenue*rng.Range(-20,30)/100m,2,MidpointRounding.AwayFromZero);
                    var basic=rng.Range(10_000_000,2_000_000_000);
                    var diluted=basic+rng.Range(0,100_000_000);
                    var basicEps=decimal.Round(net/basic,2,MidpointRounding.AwayFromZero);
                    var dilutedEps=decimal.Round(net/diluted,2,MidpointRounding.AwayFromZero);
                    var margin=decimal.Round(net/revenue,2,MidpointRounding.AwayFromZero);
                    sink.Load(new FinancialRow(company,year,quarter,TpcETime.DayNumber(year,quarter*3-2,1),revenue,net,basicEps,dilutedEps,margin,rng.Range(0,1_000_000_000)/100m,rng.Range(100_000_000,90_000_000_000)/100m,rng.Range(10_000_000,50_000_000_000)/100m,basic,diluted));
                }
            }
            // ニュース 2 件(2.6.1.8)。
            for(var item=0;item<2;item++){
                var newsId=(company-1)*2+item+1;
                var dts=TpcETime.FromDateTime(new DateTime(2000,1,1))+rng.Range(0,5L*365*TpcETime.MillisecondsPerDay);
                string? author=null;
                if(rng.Percent(90)){
                    author=SyntheticText.FirstName(rng.NextUInt64())+" "+SyntheticText.LastName(rng.NextUInt64());
                }
                sink.Load(new NewsItemRow(newsId,SyntheticText.Sentence(rng.NextUInt64(),80),SyntheticText.Sentence(rng.NextUInt64(),255),SyntheticText.Blob(rng.NextUInt64(),this.Scale.NewsItemBytes),dts,SyntheticText.LastName(rng.NextUInt64())+" Wire",author));
                sink.Load(new NewsXrefRow(newsId,company));
            }
        }
    }
    // ---- 銘柄(日次相場・銘柄) ----
    private void Securities(ILoadSink sink){
        var days=this.model.MarketDays();
        for(var security=1L;security<=this.Scale.Securities;security++){
            var rng=new Rng(Rng.Keyed(this.Seed,"security",security));
            var symbol=this.model.SymbolOf(security);
            var close=rng.Range(1500,3500);
            decimal high52=0;
            decimal low52=decimal.MaxValue;
            long highDate=0;
            long lowDate=0;
            for(var index=0;index<days.Length;index++){
                close=Math.Clamp(close+rng.Range(-60,60),100,99_999);
                var closePrice=close/100m;
                var highPrice=(close+rng.Range(0,150))/100m;
                var lowPrice=Math.Max(1,close-rng.Range(0,150))/100m;
                sink.Load(new DailyMarketRow(days[index],symbol,closePrice,highPrice,lowPrice,rng.Range(1_000,1_000_000)));
                // 52 週 = 直近 260 営業日。
                if(index>=days.Length-260){
                    if(highPrice>high52){
                        high52=highPrice;
                        highDate=days[index];
                    }
                    if(lowPrice<low52){
                        low52=lowPrice;
                        lowDate=days[index];
                    }
                }
            }
            var startDate=TpcETime.DayNumber(1900,1,1)+rng.Range(0,36500);
            var exchangeDate=startDate+rng.Range(0,3650);
            var company=this.model.CompanyOfSecurity(security);
            var dividend=rng.Range(0,200)/100m;
            var yieldPercent=decimal.Round(dividend/(close/100m)*100m,2,MidpointRounding.AwayFromZero);
            sink.Load(new SecurityRow(symbol,this.model.IssueOf(security),FixedData.Active,this.model.SecurityName(security),this.model.ExchangeOfSecurity(security),company,rng.Range(4_000_000,9_500_000_000),startDate,exchangeDate,rng.Range(100,12_000)/100m,high52,highDate,low52,lowDate,dividend,yieldPercent));
        }
    }
    // ---- 顧客(住所・顧客・税率・ウォッチ・権限)。口座の行は残高が決まる最後に出す ----
    private void CustomersFirstPart(ILoadSink sink){
        string[] domains=["@mindspring.com","@earthlink.com","@msn.com","@yahoo.com","@hotmail.com"];
        for(var customer=1L;customer<=this.Scale.Customers;customer++){
            var rng=new Rng(Rng.Keyed(this.Seed,"customer",customer));
            var addressId=TpcEModel.CustomerAddressId(customer);
            sink.Load(this.Address(addressId,"customer",customer));
            var first=this.model.FirstNameOf(customer);
            var last=this.model.LastNameOf(customer);
            string? middle=null;
            if(rng.Percent(70)){
                middle=((char)('A'+rng.Range(0,25))).ToString();
            }
            string gender;
            if(rng.Percent(50)){
                gender="M";
            }else{
                gender="F";
            }
            var dob=TpcETime.DayNumber(1920,1,1)+rng.Range(0,30000);
            var phones=new string?[12];
            for(var phone=0;phone<3;phone++){
                if(phone==0||rng.Percent(50)){
                    phones[phone*4]="1";
                    phones[phone*4+1]=rng.Range(200,999).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    phones[phone*4+2]=rng.Range(1_000_000,9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if(rng.Percent(20)){
                        phones[phone*4+3]=rng.Range(1,9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            var email1=first.ToLowerInvariant()+"."+last.ToLowerInvariant()+domains[rng.Range(0,domains.Length-1)];
            var email2=last.ToLowerInvariant()+customer.ToString(System.Globalization.CultureInfo.InvariantCulture)+domains[rng.Range(0,domains.Length-1)];
            sink.Load(new CustomerRow(customer,this.model.TaxIdOfCustomer(customer),FixedData.Active,last,first,middle,gender,this.model.TierOf(customer),dob,addressId,
                phones[0],phones[1],phones[2],phones[3],phones[4],phones[5],phones[6],phones[7],phones[8],phones[9],phones[10],phones[11],email1,email2));
            sink.Load(new CustomerTaxrateRow(this.model.NationalTaxrateOf(customer),customer));
            sink.Load(new CustomerTaxrateRow(this.model.DivisionTaxrateOf(customer),customer));
            sink.Load(new WatchListRow(customer,customer));
            foreach(var security in this.model.WatchListSecurities(customer)){
                sink.Load(new WatchItemRow(customer,this.model.SymbolOf(security)));
            }
            string[] acls=["0000","0001","0011","0111","1111"];
            var accounts=this.model.AccountCountOf(customer);
            for(var index=0;index<accounts;index++){
                var account=TpcEModel.AccountId(customer,index);
                var permissions=this.model.PermissionCountOf(account);
                for(var person=0;person<permissions;person++){
                    var (taxId,lastName,firstName)=this.model.PermittedPerson(account,person);
                    sink.Load(new AccountPermissionRow(account,Code8.From(acls[(int)Rng.KeyedRange(this.Seed,"acl",account,person,0,acls.Length-1)]),taxId,lastName,firstName));
                }
            }
        }
    }
}
