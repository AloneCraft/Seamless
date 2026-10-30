namespace Shared.TpcE.Transactions;
/// <summary>
/// Security-Detail(TPC-E 3.3.5)の問い合わせ。LINQ はここにだけ書き、SecurityDetailFrames から呼ぶ。
/// 表と入力はすべて引数で受け取り、確定した配列を返す。
/// </summary>
internal sealed class SecurityDetailQueries{
    /// <summary>
    /// 競合会社を最大 max_comp_len(3)件読み、会社名と業種名を結合する(COMPANY_COMPETITOR × COMPANY × INDUSTRY)。
    /// 仕様に並びの指定が無いので、索引の順(競合の会社 ID、業種 ID の昇順)で先頭から取る。
    /// </summary>
    public SecurityDetailCompetitor[] Competitors(TpcEDatabase db,long companyId){
        return db.CompanyCompetitors.All(companyId).Take(TpcESpec.MaxCompetitorLength).Select(entry=>new SecurityDetailCompetitor(db.Company.Get(new CompanyKey(entry.CompetitorId)).Name,db.Industry.Get(new IndustryKey(entry.IndustryId)).Name)).ToArray();
    }
    /// <summary>会社の財務データを FI_YEAR・FI_QTR の昇順に並べ、先頭から最大 max_fin_len(20)件返す。</summary>
    public SecurityDetailFinancial[] Financials(TpcEDatabase db,FinancialRow[] rows){
        return rows.OrderBy(row=>row.Year).ThenBy(row=>row.Quarter).Take(TpcESpec.MaxFinancialLength).Select(row=>new SecurityDetailFinancial(row.Year,row.Quarter,row.QuarterStartDate,row.Revenue,row.NetEarnings,row.BasicEps,row.DilutedEps,row.Margin,row.Inventory,row.Assets,row.Liability,row.OutBasic,row.OutDiluted)).ToArray();
    }
    /// <summary>
    /// 日次相場を、候補の日(start_day 以降の平日を古い順)ごとに主キー (銘柄, 日) で当て、行のある日を先頭から最大 maxRows 件返す(DM_DATE の昇順)。
    /// </summary>
    public SecurityDetailDay[] Days(TpcEDatabase db,Code16 symbol,long[] candidateDays,int maxRows){
        return candidateDays.Select(day=>db.DailyMarket.Find(new DailyMarketKey(symbol,day))).Where(row=>row.HasValue).Select(row=>row.GetValueOrDefault()).Take(maxRows).Select(row=>new SecurityDetailDay(row.Date,row.Close,row.High,row.Low,row.Volume)).ToArray();
    }
    /// <summary>
    /// 会社のニュースを最大 max_news_len(2)件読む(NEWS_XREF × NEWS_ITEM)。仕様に並びの指定が無いので、ニュース ID の昇順で先頭から取る。
    /// access_lob_flag が真なら本文を、偽なら見出しと要約を返す(返さない側は空)。
    /// </summary>
    public SecurityDetailNews[] News(TpcEDatabase db,long companyId,bool accessLob){
        if(accessLob){
            return db.CompanyNews.All(companyId).Take(TpcESpec.MaxNewsLength).Select(newsId=>db.NewsItem.Get(new NewsItemKey(newsId))).Select(item=>new SecurityDetailNews(item.Item,item.Dts,item.Source,item.Author,"","")).ToArray();
        }
        return db.CompanyNews.All(companyId).Take(TpcESpec.MaxNewsLength).Select(newsId=>db.NewsItem.Get(new NewsItemKey(newsId))).Select(item=>new SecurityDetailNews(Array.Empty<byte>(),item.Dts,item.Source,item.Author,item.Headline,item.Summary)).ToArray();
    }
}
