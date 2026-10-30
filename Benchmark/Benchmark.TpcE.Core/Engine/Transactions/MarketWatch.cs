namespace Benchmark.TpcE.Core;
// Market-Watch(TPC-E 3.3.4)の入出力。
/// <summary>
/// Market-Watch の入力(3.3.4.1)。銘柄群はウォッチリスト(<see cref="CustomerId"/>)、業種(<see cref="IndustryName"/>)、
/// 口座の保有(<see cref="AccountId"/>)のどれか 1 つで指し、使わない項目は 0 か空。判定はこの順(3.3.4.3)。
/// </summary>
public sealed record MarketWatchInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.MarketWatch;
    /// <summary>口座 ID(acct_id)。口座の保有銘柄を見るときだけ 0 以外。</summary>
    public long AccountId{get;init;}
    /// <summary>顧客 ID(cust_id)。ウォッチリストを見るときだけ 0 以外。</summary>
    public long CustomerId{get;init;}
    /// <summary>業種名(industry_name)。業種を見るときだけ空以外。</summary>
    public string IndustryName{get;init;}="";
    /// <summary>業種を見るときの会社 ID の範囲の先頭(starting_co_id)。</summary>
    public long StartingCompanyId{get;init;}
    /// <summary>業種を見るときの会社 ID の範囲の末尾(ending_co_id、両端を含む)。</summary>
    public long EndingCompanyId{get;init;}
    /// <summary>終値を使う日(start_date。DAILY_MARKET の日番号)。</summary>
    public long StartDate{get;init;}
}
/// <summary>銘柄 1 つの時価の材料(発行済株数、開始日の終値、現在値)。</summary>
public readonly record struct SymbolPrices(long NumOutstanding,decimal OldPrice,decimal NewPrice);
/// <summary>銘柄群の時価総額(開始日の終値ベースと現在値ベース)。</summary>
public readonly record struct MarketCapitalization(decimal Old,decimal New);
/// <summary>Market-Watch の出力(3.3.4.1)。</summary>
public sealed record MarketWatchOutput:TxnResult{
    /// <summary>時価総額の変化率(pct_change、%)。100×(現在/開始日−1) を小数 2 桁へ丸める。開始日の時価総額が 0 なら 0。</summary>
    public decimal PctChange{get;init;}
}
