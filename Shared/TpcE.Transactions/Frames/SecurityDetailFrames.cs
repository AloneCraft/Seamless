namespace Shared.TpcE.Transactions;
/// <summary>
/// Security-Detail(TPC-E 3.3.5)の手順。読み取りだけ。LINQ は書かない(問い合わせは SecurityDetailQueries に書く)。
/// 主キーで引ける行(銘柄・会社・住所・取引所・直近取引)はここで引き、索引や並べ替えを伴う読み取りは問い合わせに任せる。
/// </summary>
internal sealed class SecurityDetailFrames{
    /// <summary>財務データの最初の年を探す範囲。EGen のデータは固定の 5 年(2000〜2004)で、実行時の時計とは関係が無い。</summary>
    private const long FinancialFirstYearFrom=1900;
    private const long FinancialFirstYearTo=2200;
    /// <summary>最初の年から前向きに当てる年数の上限(1 社 20 行 = 5 年なので十分に広い)。</summary>
    private const int FinancialSearchYears=30;
    /// <summary>DAILY_MARKET の候補日に足す余裕(行の無い平日があっても max_rows_to_return 行に届くように)。</summary>
    private const int CandidateDaySlack=TpcESpec.MaxDayLength;
    private readonly SecurityDetailQueries queries=new();
    /// <summary>財務データの最初の年(最初に見つけた会社のもの。全社が同じ年に並ぶ前提で、違う会社は個別に探し直す)。0 は未発見。</summary>
    private long firstFinancialYear;
    public SecurityDetailOutput Run(TpcEDatabase db,SecurityDetailInput input){
        // 銘柄・会社・会社の住所・取引所・取引所の住所(SECURITY × COMPANY × ADDRESS × ZIP_CODE × EXCHANGE)。
        var security=db.Security.Get(new SecurityKey(input.Symbol));
        var company=db.Company.Get(new CompanyKey(security.CompanyId));
        var companyAddress=SecurityDetailFrames.AddressOf(db,company.AddressId);
        var exchange=db.Exchange.Get(new ExchangeKey(security.ExchangeId));
        var exchangeAddress=SecurityDetailFrames.AddressOf(db,exchange.AddressId);
        var competitors=this.queries.Competitors(db,company.Id);
        var financials=this.queries.Financials(db,this.FindFinancials(db,company.Id));
        var maxRows=Math.Max(0,input.MaxRowsToReturn);
        var candidateDays=SecurityDetailFrames.Weekdays(input.StartDay,maxRows+SecurityDetailFrames.CandidateDaySlack);
        var days=this.queries.Days(db,input.Symbol,candidateDays,maxRows);
        var lastTrade=db.LastTrade.Get(new LastTradeKey(input.Symbol));
        var news=this.queries.News(db,company.Id,input.AccessLobFlag);
        return new SecurityDetailOutput{
            Status=SecurityDetailFrames.StatusOf(days.Length,financials.Length,news.Length),
            SecurityName=security.Name,
            NumOutstanding=security.NumOutstanding,
            StartDate=security.StartDate,
            ExchangeDate=security.ExchangeDate,
            Pe=security.Pe,
            High52Week=security.High52Week,
            High52WeekDate=security.High52WeekDate,
            Low52Week=security.Low52Week,
            Low52WeekDate=security.Low52WeekDate,
            Dividend=security.Dividend,
            Yield=security.Yield,
            CompanyName=company.Name,
            SpRate=company.SpRate,
            CeoName=company.Ceo,
            CompanyDescription=company.Description,
            OpenDate=company.OpenDate,
            CompanyStatusId=company.StatusId,
            CompanyAddress=companyAddress,
            ExchangeAddress=exchangeAddress,
            ExchangeName=exchange.Name,
            ExchangeDescription=exchange.Description,
            ExchangeNumSymbols=exchange.NumSymbols,
            ExchangeOpen=exchange.Open,
            ExchangeClose=exchange.Close,
            Competitors=competitors,
            Financials=financials,
            Days=days,
            LastPrice=lastTrade.Price,
            LastOpen=lastTrade.OpenPrice,
            LastVolume=lastTrade.Volume,
            News=news,
        };
    }
    /// <summary>ハーネスの検査(3.3.5.3)。上から順に判定する。</summary>
    private static int StatusOf(int dayLength,int financialLength,int newsLength){
        if(dayLength<TpcESpec.MinDayLength||dayLength>TpcESpec.MaxDayLength){
            return -511;
        }
        if(financialLength!=TpcESpec.MaxFinancialLength){
            return -512;
        }
        if(newsLength!=TpcESpec.MaxNewsLength){
            return -513;
        }
        return 0;
    }
    /// <summary>住所と郵便番号の町・地域を引く(ADDRESS × ZIP_CODE)。</summary>
    private static SecurityDetailAddress AddressOf(TpcEDatabase db,long addressId){
        var address=db.Address.Get(new AddressKey(addressId));
        var zip=db.ZipCode.Get(new ZipCodeKey(address.ZipCode));
        return new SecurityDetailAddress(address.Line1,address.Line2,zip.Town,zip.Division,address.ZipCode,address.Country);
    }
    /// <summary>
    /// 会社の財務データ(3.3.5.3 の FINANCIAL: 年・四半期の順の先頭 20 行)を主キー (会社, 年, 四半期) で当てて集める(FINANCIAL は主キーの先頭で範囲を引けないため)。
    /// 最初の年は、実行時の時計に頼らず、最初の 1 回だけ <see cref="FinancialFirstYearFrom"/>〜<see cref="FinancialFirstYearTo"/> を探して覚える。並べ替えは問い合わせが行う。
    /// </summary>
    private FinancialRow[] FindFinancials(TpcEDatabase db,long companyId){
        var rows=new List<FinancialRow>(TpcESpec.MaxFinancialLength);
        var first=this.firstFinancialYear;
        if(first!=0){
            SecurityDetailFrames.CollectFinancials(db,companyId,first,rows);
        }
        if(rows.Count==0){
            first=SecurityDetailFrames.LocateFirstFinancialYear(db,companyId);
            if(first==0){
                return rows.ToArray();
            }
            this.firstFinancialYear=first;
            SecurityDetailFrames.CollectFinancials(db,companyId,first,rows);
        }
        return rows.ToArray();
    }
    private static void CollectFinancials(TpcEDatabase db,long companyId,long firstYear,List<FinancialRow> rows){
        for(var year=firstYear;year<firstYear+SecurityDetailFrames.FinancialSearchYears&&rows.Count<TpcESpec.MaxFinancialLength;year++){
            for(var quarter=1L;quarter<=4;quarter++){
                if(db.Financial.TryGet(new FinancialKey(companyId,year,quarter),out var row)){
                    rows.Add(row);
                }
            }
        }
    }
    private static long LocateFirstFinancialYear(TpcEDatabase db,long companyId){
        for(var year=SecurityDetailFrames.FinancialFirstYearFrom;year<=SecurityDetailFrames.FinancialFirstYearTo;year++){
            for(var quarter=1L;quarter<=4;quarter++){
                if(db.Financial.Contains(new FinancialKey(companyId,year,quarter))){
                    return year;
                }
            }
        }
        return 0;
    }
    /// <summary>start(日番号)以降の平日を古い順に count 日ぶん返す(DAILY_MARKET は営業日=平日にだけ行がある)。</summary>
    private static long[] Weekdays(long start,int count){
        var result=new long[count];
        var day=start;
        var filled=0;
        while(filled<count){
            var weekday=TpcETime.ToDate(day).DayOfWeek;
            if(weekday!=DayOfWeek.Saturday&&weekday!=DayOfWeek.Sunday){
                result[filled]=day;
                filled++;
            }
            day++;
        }
        return result;
    }
}
