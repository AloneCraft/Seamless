using System.Text;
namespace Benchmark.TpcE.Core;
/// <summary>
/// 合成データの模型。顧客・口座・銘柄・会社などの属性を (種, ID) から決定的に決める。
/// 初期データの生成器と、取引の入力を作る運転側(CE/MEE/DM)が同じ値を DB を読まずに再計算できる(EGen と同じ考え方)。
/// 件数比と分布は TPC-E 1.14.0(1.4、2.6)に合わせ、仕様に無い細部は <see cref="TpcEAssumptions"/> に「推測」として並べる。
/// </summary>
public sealed class TpcEModel{
    private readonly int[] tierOfCustomer;
    private readonly long[][] customersByTier;
    private readonly long[] companyOfSecurity;
    private readonly int[] issueNumberOfSecurity;
    private readonly long[][] securitiesOfCompany;
    private readonly Code16[] symbols;
    private readonly System.Collections.Generic.Dictionary<Code16,long> securityBySymbol;
    public TpcEModel(ulong seed,TpcEScale scale){
        this.Seed=seed;
        this.Scale=scale;
        var customers=scale.Customers;
        this.tierOfCustomer=new int[customers+1];
        var byTier=new List<long>[]{[],[],[]};
        for(var id=1;id<=customers;id++){
            var pick=Rng.KeyedRange(seed,"tier",id,0,0,99);
            int tier;
            if(pick<TpcESpec.Tier1Percent){
                tier=1;
            }else if(pick<TpcESpec.Tier1Percent+TpcESpec.Tier2Percent){
                tier=2;
            }else{
                tier=3;
            }
            this.tierOfCustomer[id]=tier;
            byTier[tier-1].Add(id);
        }
        this.customersByTier=new long[3][];
        for(var tier=0;tier<3;tier++){
            this.customersByTier[tier]=[..byTier[tier]];
        }
        // 銘柄: 先頭の会社数ぶんは各社の普通株、残りは会社を無作為に選んだ優先株(1 社あたり最大 5 銘柄、1.4.1.2)。
        var companies=scale.Companies;
        var securities=scale.Securities;
        this.companyOfSecurity=new long[securities+1];
        this.issueNumberOfSecurity=new int[securities+1];
        var perCompany=new List<long>[companies+1];
        for(var company=1;company<=companies;company++){
            perCompany[company]=[];
        }
        for(var security=1;security<=securities;security++){
            long company;
            if(security<=companies){
                company=security;
            }else{
                var rng=new Rng(Rng.Keyed(seed,"security-company",security));
                company=rng.Range(1,companies);
                while(perCompany[company].Count>=5){
                    company=company%companies+1;
                }
            }
            this.companyOfSecurity[security]=company;
            this.issueNumberOfSecurity[security]=perCompany[company].Count;
            perCompany[company].Add(security);
        }
        this.securitiesOfCompany=new long[companies+1][];
        for(var company=1;company<=companies;company++){
            this.securitiesOfCompany[company]=[..perCompany[company]];
        }
        this.symbols=new Code16[securities+1];
        this.securityBySymbol=new(checked((int)securities));
        for(var security=1;security<=securities;security++){
            var symbol=Code16.From(SymbolText(this.companyOfSecurity[security],this.issueNumberOfSecurity[security]));
            this.symbols[security]=symbol;
            this.securityBySymbol.Add(symbol,security);
        }
    }
    public ulong Seed{get;}
    public TpcEScale Scale{get;}

    // ---- 顧客と口座(1.4.2) ----
    public long CustomerCount=>this.Scale.Customers;
    public int TierOf(long customerId)=>this.tierOfCustomer[customerId];
    /// <summary>層ごとの口座数(層 1: 1〜4、層 2: 2〜8、層 3: 5〜10、1.4.2.2)。</summary>
    public int AccountCountOf(long customerId){
        return this.TierOf(customerId) switch{
            1=>(int)Rng.KeyedRange(this.Seed,"accounts",customerId,0,1,4),
            2=>(int)Rng.KeyedRange(this.Seed,"accounts",customerId,0,2,8),
            _=>(int)Rng.KeyedRange(this.Seed,"accounts",customerId,0,5,10),
        };
    }
    /// <summary>口座 ID は 顧客 ID×10+番号(顧客あたり最大 10 口座)。</summary>
    public static long AccountId(long customerId,int index)=>customerId*10+index;
    public static long CustomerOfAccount(long accountId)=>accountId/10;
    public long BrokerOfAccount(long accountId)=>Rng.KeyedRange(this.Seed,"account-broker",accountId,0,1,this.Scale.Brokers);
    /// <summary>税区分 0/1/2(推測: 一様)。</summary>
    public int TaxStatusOf(long accountId)=>(int)Rng.KeyedRange(this.Seed,"account-tax-status",accountId,0,0,2);
    /// <summary>口座で取引できる人の数(本人だけ 60%、+1 人 38%、+2 人 2%、2.6.1.8)。</summary>
    public int PermissionCountOf(long accountId){
        var pick=Rng.KeyedRange(this.Seed,"permissions",accountId,0,0,99);
        if(pick<60){
            return 1;
        }
        if(pick<98){
            return 2;
        }
        return 3;
    }
    public Code24 TaxIdOfCustomer(long customerId)=>Code24.From("C"+customerId.ToString("D12",System.Globalization.CultureInfo.InvariantCulture));
    /// <summary>口座の権限者(0 番目は持ち主の顧客)。</summary>
    public (Code24 TaxId,string LastName,string FirstName) PermittedPerson(long accountId,int index){
        var customer=CustomerOfAccount(accountId);
        if(index==0){
            return (this.TaxIdOfCustomer(customer),this.LastNameOf(customer),this.FirstNameOf(customer));
        }
        var key=accountId*4+index;
        return (Code24.From("P"+accountId.ToString("D12",System.Globalization.CultureInfo.InvariantCulture)+"-"+index.ToString(System.Globalization.CultureInfo.InvariantCulture)),SyntheticText.LastName(Rng.Keyed(this.Seed,"permission-last",key)),SyntheticText.FirstName(Rng.Keyed(this.Seed,"permission-first",key)));
    }
    public string LastNameOf(long customerId)=>SyntheticText.LastName(Rng.Keyed(this.Seed,"customer-last",customerId));
    public string FirstNameOf(long customerId)=>SyntheticText.FirstName(Rng.Keyed(this.Seed,"customer-first",customerId));
    /// <summary>顧客を層の取引頻度(1:2:3、1.4.2.1)で重みづけて選ぶ。</summary>
    public long PickCustomerByTier(ref Rng rng){
        Span<int> weights=[TpcESpec.Tier1Percent*1,TpcESpec.Tier2Percent*2,TpcESpec.Tier3Percent*3];
        for(var attempt=0;attempt<8;attempt++){
            var tier=rng.Weighted(weights);
            var candidates=this.customersByTier[tier];
            if(candidates.Length>0){
                return candidates[rng.Range(0,candidates.Length-1)];
            }
        }
        return rng.Range(1,this.CustomerCount);
    }
    /// <summary>顧客の口座から一様に 1 つ。</summary>
    public long PickAccountOf(long customerId,ref Rng rng)=>AccountId(customerId,rng.Range(0,this.AccountCountOf(customerId)-1));
    /// <summary>口座が売買する銘柄の集合(平均 10、1.4.1.4。推測: 5〜15 の一様)。初期データと実行中で同じ(1.4.2.3)。</summary>
    public long[] SecuritiesOfAccount(long accountId){
        var count=(int)Math.Min(this.Scale.Securities,Rng.KeyedRange(this.Seed,"account-securities-count",accountId,0,5,15));
        return this.DistinctSecurities(Rng.Keyed(this.Seed,"account-securities",accountId),count);
    }
    /// <summary>顧客のウォッチリストの銘柄(平均 100、2.6.1.8。推測: 50〜150 の一様)。</summary>
    public long[] WatchListSecurities(long customerId){
        var count=(int)Math.Min(this.Scale.Securities,Rng.KeyedRange(this.Seed,"watch-count",customerId,0,50,150));
        return this.DistinctSecurities(Rng.Keyed(this.Seed,"watch-securities",customerId),count);
    }
    private long[] DistinctSecurities(ulong key,int count){
        var rng=new Rng(key);
        var chosen=new HashSet<long>();
        var result=new long[count];
        var filled=0;
        while(filled<count){
            var security=rng.Range(1,this.Scale.Securities);
            if(chosen.Add(security)){
                result[filled++]=security;
            }
        }
        Array.Sort(result);
        return result;
    }

    // ---- 会社と銘柄(1.4.1) ----
    public long CompanyOfSecurity(long security)=>this.companyOfSecurity[security];
    public IReadOnlyList<long> SecuritiesOfCompany(long company)=>this.securitiesOfCompany[company];
    public Code16 SymbolOf(long security)=>this.symbols[security];
    public long SecurityOf(Code16 symbol)=>this.securityBySymbol[symbol];
    /// <summary>発行区分(COMMON、PREF_A〜PREF_D)。</summary>
    public Code8 IssueOf(long security)=>Code8.From(IssueText(this.issueNumberOfSecurity[security]));
    public string CompanyName(long company)=>SyntheticText.CompanyName(Rng.Keyed(this.Seed,"company-name",company),company);
    public Code8 IndustryOfCompany(long company)=>IndustryId((int)Rng.KeyedRange(this.Seed,"company-industry",company,0,0,TpcEModel.IndustryCount-1));
    public Code8 ExchangeOfSecurity(long security){
        var pick=Rng.KeyedRange(this.Seed,"security-exchange",security,0,0,99);
        if(pick<40){
            return Exchanges[0];
        }
        if(pick<80){
            return Exchanges[1];
        }
        if(pick<92){
            return Exchanges[2];
        }
        return Exchanges[3];
    }
    private static string IssueText(int number){
        return number switch{
            0=>"COMMON",
            1=>"PREF_A",
            2=>"PREF_B",
            3=>"PREF_C",
            _=>"PREF_D",
        };
    }
    private static string SymbolText(long company,int issueNumber){
        var root=Base26(company-1,4);
        if(issueNumber==0){
            return root;
        }
        return root+"-P"+(char)('A'+issueNumber-1);
    }
    internal static string Base26(long value,int minLength){
        var builder=new StringBuilder();
        do{
            builder.Insert(0,(char)('A'+value%26));
            value/=26;
        }while(value>0);
        while(builder.Length<minLength){
            builder.Insert(0,'A');
        }
        return builder.ToString();
    }

    // ---- ブローカー・部門・業種・取引所・税率 ----
    public string BrokerName(long broker)=>SyntheticText.FirstName(Rng.Keyed(this.Seed,"broker-first",broker))+" "+SyntheticText.LastName(Rng.Keyed(this.Seed,"broker-last",broker))+" "+Base26(broker,3);
    public const int SectorCount=12;
    public const int IndustryCount=102;
    public static Code8 SectorId(int index)=>Code8.From("S"+(char)('A'+index));
    public static string SectorName(int index)=>SectorNames[index];
    public static Code8 IndustryId(int index)=>Code8.From(Base26(index,2));
    public static int SectorOfIndustry(int index)=>index%SectorCount;
    public static string IndustryName(int index)=>SectorNames[SectorOfIndustry(index)]+" "+IndustryWords[index/SectorCount%IndustryWords.Length]+" "+Base26(index,2);
    private static readonly string[] SectorNames=["Basic Materials","Capital Goods","Conglomerates","Consumer Cyclical","Consumer Staples","Energy","Financial","Healthcare","Services","Technology","Transportation","Utilities"];
    private static readonly string[] IndustryWords=["Products","Services","Equipment","Manufacturing","Retail","Systems","Supplies","Operations","Holdings"];
    public static readonly Code8[] Exchanges=[Code8.From("NYSE"),Code8.From("NASDAQ"),Code8.From("AMEX"),Code8.From("PCX")];
    public static readonly string[] ExchangeNames=["New York Stock Exchange","NASDAQ Stock Market","American Stock Exchange","Pacific Exchange"];
    /// <summary>国の税率 ID(Data-Maintenance が US1〜US5、CN1〜CN4 を巡回させる、3.3.11)。</summary>
    public static readonly string[] NationalTaxrates=["US1","US2","US3","US4","US5","CN1","CN2","CN3","CN4"];
    public const int TaxrateCount=320;
    /// <summary>地域の税率 ID(US・CN で始まらない 2 文字 + 数字)。</summary>
    public static Code8 DivisionTaxrateId(int index){
        // AA1 から順に、US・CN で始まるものを飛ばす。
        var letters=index/9;
        var digit=index%9+1;
        var text=Base26(letters,2);
        if(text.StartsWith("CN",StringComparison.Ordinal)||text.StartsWith("US",StringComparison.Ordinal)){
            text="X"+text[1..];
        }
        return Code8.From(text+digit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    public const int DivisionTaxrateCount=TaxrateCount-9;
    /// <summary>顧客の国の税率(推測: US 80%、CN 20%)。</summary>
    public Code8 NationalTaxrateOf(long customerId){
        if(Rng.KeyedRange(this.Seed,"customer-country",customerId,0,0,99)<80){
            return Code8.From(NationalTaxrates[Rng.KeyedRange(this.Seed,"customer-national-tax",customerId,0,0,4)]);
        }
        return Code8.From(NationalTaxrates[5+Rng.KeyedRange(this.Seed,"customer-national-tax",customerId,0,0,3)]);
    }
    public Code8 DivisionTaxrateOf(long customerId)=>DivisionTaxrateId((int)Rng.KeyedRange(this.Seed,"customer-division-tax",customerId,0,0,DivisionTaxrateCount-1));
    public const int ZipCodeCount=14741;
    public static Code16 ZipCode(int index)=>Code16.From((10000+index).ToString(System.Globalization.CultureInfo.InvariantCulture));

    // ---- 住所 ID(顧客、会社、取引所の順) ----
    public static long CustomerAddressId(long customerId)=>customerId;
    public long CompanyAddressId(long company)=>this.Scale.Customers+company;
    public long ExchangeAddressId(int exchangeIndex)=>this.Scale.Customers+this.Scale.Companies+exchangeIndex+1;

    // ---- 時刻 ----
    /// <summary>初期取引の開始(2005-01-03 月曜 9:00)。DAILY_MARKET は 2004-12-31 で終わる(2.2.6.3)。</summary>
    public static readonly long InitialTradeStart=TpcETime.FromDateTime(new DateTime(2005,1,3,9,0,0));
    public static readonly long LastMarketDay=TpcETime.DayNumber(2004,12,31);
    /// <summary>営業日 n(0 始まり)の開始時刻。土日を飛ばす。</summary>
    public static long BusinessDayStart(int businessDay){
        var day=TpcETime.DateOf(InitialTradeStart);
        var remaining=businessDay;
        while(remaining>0){
            day++;
            var weekday=TpcETime.ToDate(day).DayOfWeek;
            if(weekday!=DayOfWeek.Saturday&&weekday!=DayOfWeek.Sunday){
                remaining--;
            }
        }
        return TpcETime.StartOfDay(day)+9*3_600_000L;
    }
    /// <summary>実行の開始時刻(初期取引の翌営業日 9:00)。</summary>
    public long RunStart=>BusinessDayStart(this.Scale.InitialTradeDays);
    /// <summary>DAILY_MARKET の営業日(平日)を古い順に返す。2004-12-31 で終わる <see cref="TpcEScale.MarketDays"/> 日。</summary>
    public long[] MarketDays(){
        var result=new long[this.Scale.MarketDays];
        var day=LastMarketDay;
        for(var index=result.Length-1;index>=0;){
            var weekday=TpcETime.ToDate(day).DayOfWeek;
            if(weekday!=DayOfWeek.Saturday&&weekday!=DayOfWeek.Sunday){
                result[index--]=day;
            }
            day--;
        }
        return result;
    }

    // ---- 価格(MEE の価格表) ----
    /// <summary>価格の下限・上限(セント)。推測: 20.00〜30.00。</summary>
    public const long PriceLowCents=2000;
    public const long PriceHighCents=3000;
    /// <summary>価格の周期(15 分。指値は 15 分以内に必ず届く、1.4.4.4)。</summary>
    public const long PricePeriodMilliseconds=900_000;
    /// <summary>銘柄の時刻 t の価格(セント)。下限と上限の間を三角波で往復する。</summary>
    public long PriceCents(long security,long time){
        var phase=Rng.KeyedRange(this.Seed,"price-phase",security,0,0,PricePeriodMilliseconds-1);
        var position=(time+phase)%PricePeriodMilliseconds;
        var half=PricePeriodMilliseconds/2;
        long offset;
        if(position<half){
            offset=position;
        }else{
            offset=PricePeriodMilliseconds-position;
        }
        return PriceLowCents+(PriceHighCents-PriceLowCents)*offset/half;
    }
    public decimal Price(long security,long time)=>this.PriceCents(security,time)/100m;
}
/// <summary>仕様書で決まらず推測で決めた値(describe と要約に出す)。</summary>
public static class TpcEAssumptions{
    public static readonly IReadOnlyList<string> All=[
        "口座の税区分 0/1/2 は一様",
        "口座が売買する銘柄数は 5〜15 の一様(平均 10 は仕様どおり)",
        "ウォッチリストの銘柄数は 50〜150 の一様(平均 100 は仕様どおり)",
        "顧客の国は US 80%、CN 20%。国の税率は US1〜US5 / CN1〜CN4 の一様",
        "銘柄の価格は 20.00〜30.00 を周期 15 分の三角波で往復(位相は銘柄ごと)",
        "銘柄の上場取引所は NYSE 40%、NASDAQ 40%、AMEX 12%、PCX 8%",
        "成行注文の約定までの遅れは 1〜5 秒の一様(模擬時刻)",
        "1,000 顧客未満の規模は仕様外の縮小(件数比はそのまま)",
        "DAILY_MARKET の日数と NEWS_ITEM の本文の大きさは既定で縮小(引数で仕様値にできる)",
    ];
}
/// <summary>合成の名前と文章。種から決定的に作る。</summary>
public static class SyntheticText{
    private static readonly string[] Onsets=["b","c","d","f","g","h","j","k","l","m","n","p","r","s","t","v","w","z","br","ch","cl","dr","gr","sh","st","tr"];
    private static readonly string[] Vowels=["a","e","i","o","u","ai","ea","ou","ie"];
    private static readonly string[] Codas=["","n","r","s","l","th","nd","ck","m","x"];
    private static string Word(ref Rng rng,int syllables){
        var builder=new StringBuilder();
        for(var index=0;index<syllables;index++){
            builder.Append(Onsets[rng.Range(0,Onsets.Length-1)]);
            builder.Append(Vowels[rng.Range(0,Vowels.Length-1)]);
            if(index==syllables-1){
                builder.Append(Codas[rng.Range(0,Codas.Length-1)]);
            }
        }
        builder[0]=char.ToUpperInvariant(builder[0]);
        return builder.ToString();
    }
    public static string FirstName(ulong key){
        var rng=new Rng(key);
        return Word(ref rng,rng.Range(1,3));
    }
    public static string LastName(ulong key){
        var rng=new Rng(key);
        return Word(ref rng,rng.Range(2,3));
    }
    public static string CompanyName(ulong key,long company){
        var rng=new Rng(key);
        string[] suffixes=["Inc.","Corp.","Co.","Ltd.","Group","Holdings"];
        return Word(ref rng,rng.Range(2,3))+" "+TpcEModel.Base26(company,3)+" "+suffixes[rng.Range(0,suffixes.Length-1)];
    }
    public static string Sentence(ulong key,int maxLength){
        var rng=new Rng(key);
        var builder=new StringBuilder();
        while(builder.Length<maxLength-12){
            if(builder.Length>0){
                builder.Append(' ');
            }
            builder.Append(Word(ref rng,rng.Range(1,3)).ToLowerInvariant());
        }
        if(builder.Length>maxLength){
            builder.Length=maxLength;
        }
        return builder.ToString();
    }
    public static byte[] Blob(ulong key,int length){
        var rng=new Rng(key);
        var bytes=new byte[length];
        for(var index=0;index<length;index++){
            bytes[index]=(byte)(' '+rng.Range(0,94));
        }
        return bytes;
    }
}
