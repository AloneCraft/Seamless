namespace Shared.TpcE.Transactions;
/// <summary>
/// Security-Detail(TPC-E 3.3.5)の手順。読み取りだけ。LINQ は書かない(問い合わせは SecurityDetailQueries に書く)。
/// 主キーで引ける行(銘柄・会社・住所・取引所・直近取引)はここで引き、索引や並べ替えを伴う読み取りは問い合わせに任せる。
/// </summary>
internal sealed class SecurityDetailFrames{
    /// <summary>財務データを探す年数の上限(1 行も見つからないまま、この年数をさかのぼったら止める)。</summary>
    private const int FinancialSearchYears=10;
    /// <summary>DAILY_MARKET の候補日に足す余裕(行の無い平日があっても max_rows_to_return 行に届くように)。</summary>
    private const int CandidateDaySlack=TpcESpec.MaxDayLength;
    private readonly SecurityDetailQueries queries=new();
    public SecurityDetailOutput Run(TpcEDatabase db,SecurityDetailInput input){
        // 銘柄・会社・会社の住所・取引所・取引所の住所(SECURITY × COMPANY × ADDRESS × ZIP_CODE × EXCHANGE)。
        var security=db.Security.Get(new SecurityKey(input.Symbol));
        var company=db.Company.Get(new CompanyKey(security.CompanyId));
        var companyAddress=SecurityDetailFrames.AddressOf(db,company.AddressId);
        var exchange=db.Exchange.Get(new ExchangeKey(security.ExchangeId));
        var exchangeAddress=SecurityDetailFrames.AddressOf(db,exchange.AddressId);
        var competitors=this.queries.Competitors(db,company.Id);
        var financials=this.queries.Financials(db,SecurityDetailFrames.FindFinancials(db,company.Id,input.Now));
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
    /// 会社の財務データを主キー (会社, 年, 四半期) で当てて集める(FINANCIAL は主キーの先頭で範囲を引けないため)。
    /// 年は現在時刻の翌年から過去へ 1 年ずつ当て、行が見つかった後で 1 行も無い年に来たら止める(四半期決算は連続した年に並ぶ前提)。
    /// 1 行も見つからないまま <see cref="FinancialSearchYears"/> 年さかのぼったら止める。並べ替えは問い合わせが行う。
    /// </summary>
    private static FinancialRow[] FindFinancials(TpcEDatabase db,long companyId,long now){
        var rows=new List<FinancialRow>(TpcESpec.MaxFinancialLength);
        long year=TpcETime.ToDateTime(now).Year+1;
        var emptyYears=0;
        while(emptyYears<SecurityDetailFrames.FinancialSearchYears){
            var found=false;
            for(var quarter=1L;quarter<=4;quarter++){
                if(db.Financial.TryGet(new FinancialKey(companyId,year,quarter),out var row)){
                    rows.Add(row);
                    found=true;
                }
            }
            if(found){
                emptyYears=0;
            }else if(rows.Count>0){
                break;
            }else{
                emptyYears++;
            }
            year--;
        }
        return rows.ToArray();
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
