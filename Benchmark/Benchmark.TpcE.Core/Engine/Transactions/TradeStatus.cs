namespace Benchmark.TpcE.Core;
// Trade-Status(TPC-E 3.3.9)の入出力。
/// <summary>Trade-Status の入力(3.3.9.1)。</summary>
public sealed record TradeStatusInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeStatus;
    /// <summary>口座 ID(acct_id)。</summary>
    public long AccountId{get;init;}
}
/// <summary>取引 1 件の状況(trade_id・trade_dts・status_name・type_name・symbol・trade_qty・exec_name・charge・s_name・ex_name)。</summary>
public readonly record struct TradeStatusEntry(long TradeId,long TradeDts,string StatusName,string TypeName,Code16 Symbol,int Quantity,string ExecName,decimal Charge,string SecurityName,string ExchangeName);
/// <summary>Trade-Status の問い合わせの途中の 1 行(取引と、その銘柄)。</summary>
public readonly record struct TradeWithSecurity(TradeRow Trade,SecurityRow Security);
/// <summary>Trade-Status の出力(3.3.9.1・3.3.9.3)。</summary>
public sealed record TradeStatusOutput:TxnResult{
    /// <summary>見つかった取引の数(num_found)。50 でなければ status は -911。</summary>
    public int NumFound{get;init;}
    /// <summary>口座の最新の取引。T_DTS の降順(同時刻は取引 ID の降順)、先頭 max_trade_status_len(50)件まで。</summary>
    public TradeStatusEntry[] Trades{get;init;}=[];
    /// <summary>口座の持ち主の姓(cust_l_name)。</summary>
    public string CustomerLastName{get;init;}="";
    /// <summary>口座の持ち主の名(cust_f_name)。</summary>
    public string CustomerFirstName{get;init;}="";
    /// <summary>口座のブローカーの名前(broker_name)。</summary>
    public string BrokerName{get;init;}="";
}
