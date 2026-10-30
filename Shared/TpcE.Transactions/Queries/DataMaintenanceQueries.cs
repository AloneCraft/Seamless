namespace Shared.TpcE.Transactions;
/// <summary>
/// Data-Maintenance(TPC-E 3.3.11)の問い合わせ。LINQ はここにだけ書き、DataMaintenanceFrames から呼ぶ。
/// 表と入力はすべて引数で受け取り、結果は確定した値(配列・スカラー)で返す。
/// </summary>
internal sealed class DataMaintenanceQueries{
    /// <summary>口座の権限の行を AP_ACL の降順に並べた先頭の AP_ACL(どの口座にも 1 行以上ある、3.3.11)。</summary>
    public Code8 TopAccountPermissionAcl(TpcEDatabase db,long accountId){
        return db.AccountPermissions.All(accountId).Select(taxId=>db.AccountPermission.Get(new AccountPermissionKey(accountId,taxId)).Acl).OrderByDescending(acl=>acl).First();
    }
    /// <summary>口座の権限の行のうち AP_ACL が acl のもの(主キー AP_TAX_ID の昇順)。</summary>
    public AccountPermissionRow[] AccountPermissionsWithAcl(TpcEDatabase db,long accountId,Code8 acl){
        return db.AccountPermissions.All(accountId).Select(taxId=>db.AccountPermission.Get(new AccountPermissionKey(accountId,taxId))).Where(row=>row.Acl==acl).ToArray();
    }
    /// <summary>顧客の国の税率(CX_TX_ID が "US" か "CN" で始まるもの。顧客ごとにちょうど 1 つ)。</summary>
    public Code8 CustomerCountryTaxrate(TpcEDatabase db,long customerId){
        return db.CustomerTaxrates.All(customerId).Where(taxId=>taxId.ToString().StartsWith("US",StringComparison.Ordinal)||taxId.ToString().StartsWith("CN",StringComparison.Ordinal)).Single();
    }
    /// <summary>候補の主キーのうち DAILY_MARKET にある行(候補の順のまま)。</summary>
    public DailyMarketRow[] DailyMarketRows(TpcEDatabase db,DailyMarketKey[] keys){
        return keys.Select(key=>db.DailyMarket.Find(key)).Where(found=>found.HasValue).Select(found=>found.GetValueOrDefault()).ToArray();
    }
    /// <summary>EX_DESC が "LAST UPDATED" を含む取引所の数(大文字小文字を区別する)。</summary>
    public int ExchangeUpdatedCount(TpcEDatabase db){
        return db.Exchange.Rows.Count(exchange=>(exchange.Description??"").Contains("LAST UPDATED",StringComparison.Ordinal));
    }
    /// <summary>取引所の全行(主キー EX_ID の昇順)。</summary>
    public ExchangeRow[] ExchangeRows(TpcEDatabase db){
        return db.Exchange.Rows.OrderBy(exchange=>exchange.Id).ToArray();
    }
    /// <summary>候補の主キーのうち FINANCIAL にある行(候補の順のまま)。</summary>
    public FinancialRow[] FinancialRows(TpcEDatabase db,FinancialKey[] keys){
        return keys.Select(key=>db.Financial.Find(key)).Where(found=>found.HasValue).Select(found=>found.GetValueOrDefault()).ToArray();
    }
    /// <summary>四半期の開始日が月の 1 日の行の数。</summary>
    public int FinancialFirstDayCount(TpcEDatabase db,FinancialRow[] rows){
        return rows.Count(row=>TpcETime.ToDate(row.QuarterStartDate).Day==1);
    }
    /// <summary>会社のニュース(NEWS_XREF で会社に結び付いた NEWS_ITEM。NI_ID の昇順)。</summary>
    public NewsItemRow[] CompanyNewsItems(TpcEDatabase db,long companyId){
        return db.CompanyNews.All(companyId).Select(newsId=>db.NewsItem.Get(new NewsItemKey(newsId))).ToArray();
    }
    /// <summary>ウォッチリスト群の銘柄を全部(WI_S_SYMB の昇順。複数のリストにある銘柄はその数だけ並ぶ)。</summary>
    public Code16[] WatchSymbols(TpcEDatabase db,long[] watchListIds){
        return watchListIds.SelectMany(watchListId=>db.WatchListItems.All(watchListId)).OrderBy(symbol=>symbol).ToArray();
    }
    /// <summary>
    /// SECURITY で oldSymbol より後ろにあり、watched(昇順)に無い銘柄のうち最小のもの。無ければ空の配列。
    /// SECURITY には記号順の索引が無いので全行を走査する。
    /// </summary>
    public Code16[] NextUnwatchedSymbol(TpcEDatabase db,Code16 oldSymbol,Code16[] watched){
        return db.Security.Rows.Where(security=>security.Symbol.CompareTo(oldSymbol)>0&&Array.BinarySearch(watched,security.Symbol)<0).Select(security=>security.Symbol).OrderBy(symbol=>symbol).Take(1).ToArray();
    }
}
