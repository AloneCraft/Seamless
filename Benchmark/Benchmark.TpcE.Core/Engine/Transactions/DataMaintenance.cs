namespace Benchmark.TpcE.Core;
// Data-Maintenance(TPC-E 3.3.11)の入出力。
/// <summary>
/// Data-Maintenance の入力(3.3.11.1)。表ごとに使う引数だけが入り、使わない引数は 0 か空(<c>default</c>)。
/// <list type="bullet">
/// <item>ACCOUNT_PERMISSION: <see cref="AccountId"/></item>
/// <item>ADDRESS: <see cref="CustomerId"/>(0 でなければ顧客の住所)か <see cref="CompanyId"/>(会社の住所)</item>
/// <item>COMPANY・FINANCIAL・NEWS_ITEM: <see cref="CompanyId"/></item>
/// <item>CUSTOMER・CUSTOMER_TAXRATE・WATCH_ITEM: <see cref="CustomerId"/></item>
/// <item>DAILY_MARKET: <see cref="Symbol"/>・<see cref="DayOfMonth"/>・<see cref="VolumeIncrement"/></item>
/// <item>EXCHANGE: 追加の引数なし(現在時刻は <see cref="TxnRequest.Now"/>)</item>
/// <item>SECURITY: <see cref="Symbol"/></item>
/// <item>TAXRATE: <see cref="TaxId"/></item>
/// </list>
/// </summary>
public sealed record DataMaintenanceInput:TxnRequest{
    public const string AccountPermission="ACCOUNT_PERMISSION";
    public const string Address="ADDRESS";
    public const string Company="COMPANY";
    public const string Customer="CUSTOMER";
    public const string CustomerTaxrate="CUSTOMER_TAXRATE";
    public const string DailyMarket="DAILY_MARKET";
    public const string Exchange="EXCHANGE";
    public const string Financial="FINANCIAL";
    public const string NewsItem="NEWS_ITEM";
    public const string Security="SECURITY";
    public const string Taxrate="TAXRATE";
    public const string WatchItem="WATCH_ITEM";
    /// <summary>変える表の一覧(3.3.11 の順)。実行のたびに次の表へ進む。</summary>
    public static IReadOnlyList<string> TableNames{get;}=[
        DataMaintenanceInput.AccountPermission,
        DataMaintenanceInput.Address,
        DataMaintenanceInput.Company,
        DataMaintenanceInput.Customer,
        DataMaintenanceInput.CustomerTaxrate,
        DataMaintenanceInput.DailyMarket,
        DataMaintenanceInput.Exchange,
        DataMaintenanceInput.Financial,
        DataMaintenanceInput.NewsItem,
        DataMaintenanceInput.Security,
        DataMaintenanceInput.Taxrate,
        DataMaintenanceInput.WatchItem,
    ];
    public override TxnKind Kind=>TxnKind.DataMaintenance;
    /// <summary>table_name。<see cref="TableNames"/> のどれか。</summary>
    public string TableName{get;init;}="";
    /// <summary>acct_id。</summary>
    public long AccountId{get;init;}
    /// <summary>c_id。</summary>
    public long CustomerId{get;init;}
    /// <summary>co_id。</summary>
    public long CompanyId{get;init;}
    /// <summary>day_of_month(1〜31)。</summary>
    public int DayOfMonth{get;init;}
    /// <summary>symbol。</summary>
    public Code16 Symbol{get;init;}
    /// <summary>tx_id。</summary>
    public Code8 TaxId{get;init;}
    /// <summary>vol_incr(正または負)。</summary>
    public int VolumeIncrement{get;init;}
}
/// <summary>
/// Data-Maintenance の出力。仕様の出力は status だけ(検査なしなので常に 0)。
/// 4 通りの比較を強めるため、書き換えた行の数 <see cref="NumUpdated"/> を足す(仕様外)。
/// </summary>
public sealed record DataMaintenanceOutput:TxnResult{
    /// <summary>書き換えた行の数(主キーが変わる表は、消して足した 1 組を 1 行と数える)。</summary>
    public int NumUpdated{get;init;}
}
