namespace Benchmark.TpcE.Core;
// Security-Detail(TPC-E 3.3.5)の入出力。読み取りだけの取引(フレームは 1 つ)。
/// <summary>Security-Detail の入力(3.3.5.1)。</summary>
public sealed record SecurityDetailInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.SecurityDetail;
    /// <summary>access_lob_flag。true ならニュースの本文(NI_ITEM)を、false なら見出しと要約を読む。</summary>
    public bool AccessLobFlag{get;init;}
    /// <summary>max_rows_to_return。DAILY_MARKET から返す行数(5〜20)。</summary>
    public int MaxRowsToReturn{get;init;}
    /// <summary>start_day。DAILY_MARKET をこの日(日番号)以降から読む。</summary>
    public long StartDay{get;init;}
    /// <summary>symbol。</summary>
    public Code16 Symbol{get;init;}
}
/// <summary>
/// Security-Detail の出力(3.3.5.3 の Frame 1 の出力)。配列の長さが day_len・fin_len・news_len にあたる。
/// status は -511(日次相場の行数が 5〜20 の外)、-512(財務が 20 行でない)、-513(ニュースが 2 行でない)の順に判定する。
/// </summary>
public sealed record SecurityDetailOutput:TxnResult{
    /// <summary>s_name。</summary>
    public string SecurityName{get;init;}="";
    /// <summary>num_out。</summary>
    public long NumOutstanding{get;init;}
    /// <summary>start_date(日番号)。</summary>
    public long StartDate{get;init;}
    /// <summary>ex_date(S_EXCH_DATE、日番号)。</summary>
    public long ExchangeDate{get;init;}
    /// <summary>pe_ratio。</summary>
    public decimal Pe{get;init;}
    /// <summary>52_wk_high。</summary>
    public decimal High52Week{get;init;}
    /// <summary>52_wk_high_date(日番号)。</summary>
    public long High52WeekDate{get;init;}
    /// <summary>52_wk_low。</summary>
    public decimal Low52Week{get;init;}
    /// <summary>52_wk_low_date(日番号)。</summary>
    public long Low52WeekDate{get;init;}
    /// <summary>divid。</summary>
    public decimal Dividend{get;init;}
    /// <summary>yield。</summary>
    public decimal Yield{get;init;}
    /// <summary>co_name。</summary>
    public string CompanyName{get;init;}="";
    /// <summary>sp_rate。</summary>
    public string SpRate{get;init;}="";
    /// <summary>ceo_name。</summary>
    public string CeoName{get;init;}="";
    /// <summary>co_desc。</summary>
    public string CompanyDescription{get;init;}="";
    /// <summary>open_date(日番号)。</summary>
    public long OpenDate{get;init;}
    /// <summary>co_st_id。</summary>
    public Code8 CompanyStatusId{get;init;}
    /// <summary>co_ad_line1・co_ad_line2・co_ad_town・co_ad_div・co_ad_zip・co_ad_ctry。</summary>
    public SecurityDetailAddress CompanyAddress{get;init;}
    /// <summary>ex_ad_line1・ex_ad_line2・ex_ad_town・ex_ad_div・ex_ad_zip・ex_ad_ctry。</summary>
    public SecurityDetailAddress ExchangeAddress{get;init;}
    /// <summary>ex_name。</summary>
    public string ExchangeName{get;init;}="";
    /// <summary>ex_desc。</summary>
    public string? ExchangeDescription{get;init;}
    /// <summary>ex_num_symb。</summary>
    public int ExchangeNumSymbols{get;init;}
    /// <summary>ex_open(HHMM)。</summary>
    public int ExchangeOpen{get;init;}
    /// <summary>ex_close(HHMM)。</summary>
    public int ExchangeClose{get;init;}
    /// <summary>cp_co_name[]・cp_in_name[](最大 3 件。仕様に並びの指定が無いので競合の会社 ID・業種 ID の昇順)。</summary>
    public SecurityDetailCompetitor[] Competitors{get;init;}=[];
    /// <summary>fin[](FI_YEAR・FI_QTR の昇順に最大 20 件)。</summary>
    public SecurityDetailFinancial[] Financials{get;init;}=[];
    /// <summary>day[](DM_DATE の昇順に最大 max_rows_to_return 件)。</summary>
    public SecurityDetailDay[] Days{get;init;}=[];
    /// <summary>last_price。</summary>
    public decimal LastPrice{get;init;}
    /// <summary>last_open。</summary>
    public decimal LastOpen{get;init;}
    /// <summary>last_vol。</summary>
    public long LastVolume{get;init;}
    /// <summary>news[](最大 2 件。仕様に並びの指定が無いのでニュース ID の昇順)。</summary>
    public SecurityDetailNews[] News{get;init;}=[];
}
/// <summary>会社または取引所の住所(ADDRESS と ZIP_CODE)。</summary>
public readonly record struct SecurityDetailAddress(string? Line1,string? Line2,string Town,string Division,Code16 Zip,string? Country);
/// <summary>競合会社の名前と業種名。</summary>
public readonly record struct SecurityDetailCompetitor(string CompanyName,string IndustryName);
/// <summary>財務データ 1 行(FINANCIAL)。</summary>
public readonly record struct SecurityDetailFinancial(
    long Year,long Quarter,long QuarterStartDate,decimal Revenue,decimal NetEarnings,decimal BasicEps,decimal DilutedEps,decimal Margin,
    decimal Inventory,decimal Assets,decimal Liability,long OutBasic,long OutDiluted);
/// <summary>日次相場 1 行(DAILY_MARKET)。</summary>
public readonly record struct SecurityDetailDay(long Date,decimal Close,decimal High,decimal Low,long Volume);
/// <summary>ニュース 1 件。access_lob_flag が真なら Item に本文が入り見出しと要約は空、偽なら Item は空で見出しと要約が入る。</summary>
public readonly record struct SecurityDetailNews(byte[] Item,long Dts,string Source,string? Author,string Headline,string Summary);
