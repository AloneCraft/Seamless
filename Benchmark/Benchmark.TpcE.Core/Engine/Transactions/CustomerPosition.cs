namespace Benchmark.TpcE.Core;
// Customer-Position(TPC-E 3.3.2)の入出力。
/// <summary>Customer-Position の入力(3.3.2.1)。顧客は <see cref="CustomerId"/> か、それが 0 なら <see cref="TaxId"/> で指す。</summary>
public sealed record CustomerPositionInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.CustomerPosition;
    /// <summary>顧客 ID(cust_id)。0 なら納税者番号で引く。</summary>
    public long CustomerId{get;init;}
    /// <summary>納税者番号(tax_id)。顧客 ID で指すときは空(default)。</summary>
    public Code24 TaxId{get;init;}
    /// <summary>取引の履歴も返すか(get_history。true なら Frame 2、false なら Frame 3)。</summary>
    public bool GetHistory{get;init;}
    /// <summary>履歴を返す口座の、Frame 1 の口座一覧での添字(acct_id_idx)。履歴を返さないときは -1。</summary>
    public int AccountIdIndex{get;init;}=-1;
}
/// <summary>口座 1 つの資産(acct_id・cash_bal・asset_total)。資産は HS_QTY×LT_PRICE の合計で、保有が無ければ 0。</summary>
public readonly record struct CustomerAccountPosition(long AccountId,decimal CashBalance,decimal AssetTotal);
/// <summary>保有 1 件の時価の材料(口座、保有数量、銘柄の現在値)。</summary>
public readonly record struct HoldingValue(long AccountId,int Quantity,decimal Price);
/// <summary>履歴 1 件(trade_id・symbol・qty・trade_status・hist_dts)。</summary>
public readonly record struct CustomerPositionHistory(long TradeId,Code16 Symbol,int Quantity,string TradeStatus,long HistoryDts);
/// <summary>Customer-Position の Frame 2 の途中の 1 行(取引と、その状態の記録)。</summary>
public readonly record struct TradeHistoryEvent(TradeRow Trade,TradeHistoryRow History);
/// <summary>Customer-Position の出力(3.3.2.1)。</summary>
public sealed record CustomerPositionOutput:TxnResult{
    /// <summary>引いた顧客 ID(入力が納税者番号のときは Frame 1 で求めた値)。</summary>
    public long CustomerId{get;init;}
    public Code8 CustomerStatusId{get;init;}
    public string LastName{get;init;}="";
    public string FirstName{get;init;}="";
    public string? MiddleInitial{get;init;}
    public string? Gender{get;init;}
    public int Tier{get;init;}
    /// <summary>生年月日(日番号)。</summary>
    public long DateOfBirth{get;init;}
    public long AddressId{get;init;}
    public string? Country1{get;init;}
    public string? Area1{get;init;}
    public string? Local1{get;init;}
    public string? Extension1{get;init;}
    public string? Country2{get;init;}
    public string? Area2{get;init;}
    public string? Local2{get;init;}
    public string? Extension2{get;init;}
    public string? Country3{get;init;}
    public string? Area3{get;init;}
    public string? Local3{get;init;}
    public string? Extension3{get;init;}
    public string? Email1{get;init;}
    public string? Email2{get;init;}
    /// <summary>口座の数(acct_len)。</summary>
    public int AccountLength{get;init;}
    /// <summary>口座ごとの資産。資産の昇順(同額は口座 ID の昇順)、先頭 max_acct_len(10)件まで。</summary>
    public CustomerAccountPosition[] Accounts{get;init;}=[];
    /// <summary>履歴の件数(hist_len)。履歴を返さないときは 0。</summary>
    public int HistoryLength{get;init;}
    /// <summary>履歴。TH_DTS の降順(同時刻は取引 ID の降順、次に状態名の Ordinal 昇順)、先頭 max_hist_len(30)件まで。</summary>
    public CustomerPositionHistory[] History{get;init;}=[];
}
