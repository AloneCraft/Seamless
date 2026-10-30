namespace Benchmark.TpcE.Core;
// Trade-Update(TPC-E 3.3.10)の入出力。互いに排他な 3 つのフレームのうち 1 つを実行し、過去の取引に小さな訂正を入れる。
/// <summary>Trade-Update の入力(3.3.10.1)。使わない項目は 0 か空のまま。</summary>
public sealed record TradeUpdateInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeUpdate;
    /// <summary>frame_to_execute(1〜3)。</summary>
    public int FrameToExecute{get;init;}
    /// <summary>trade_id[](Frame 1。初期取引から選んだ取引 ID)。</summary>
    public long[] TradeIds{get;init;}=[];
    /// <summary>acct_id(Frame 2)。</summary>
    public long AccountId{get;init;}
    /// <summary>max_acct_id(Frame 3。仕様でも検索条件には使わない)。</summary>
    public long MaxAccountId{get;init;}
    /// <summary>max_trades。</summary>
    public int MaxTrades{get;init;}
    /// <summary>max_updates(max_trades 以下)。</summary>
    public int MaxUpdates{get;init;}
    /// <summary>start_trade_dts(Frame 2・3)。</summary>
    public long StartTradeDts{get;init;}
    /// <summary>end_trade_dts(Frame 2・3)。</summary>
    public long EndTradeDts{get;init;}
    /// <summary>symbol(Frame 3)。</summary>
    public Code16 Symbol{get;init;}
}
/// <summary>
/// Trade-Update の出力。
/// status は Frame 1: -1011/-1012、Frame 2: -1021/+1021/-1022、Frame 3: -1031/+1032/-1032(3.3.10.3〜3.3.10.5 のハーネスの擬似コードどおり。
/// Frame 3 の警告は 3.2.1.8 の表では +1031 だが、擬似コードの +1032 に合わせた)。
/// </summary>
public sealed record TradeUpdateOutput:TxnResult{
    /// <summary>frame_executed。</summary>
    public int FrameExecuted{get;init;}
    /// <summary>num_found。Frame 1 は仕様どおり「変更する場合」に読んだ行だけを数える。</summary>
    public int NumFound{get;init;}
    /// <summary>num_updated。</summary>
    public int NumUpdated{get;init;}
    /// <summary>見つけた取引(Frame 1 は入力の順、Frame 2・3 は T_DTS の昇順)。値は変更後のもの。</summary>
    public TradeUpdateTrade[] Trades{get;init;}=[];
}
/// <summary>
/// 取引 1 件の情報。どのフレームでも TRADE の行の値をすべて入れる。IsMarket は Frame 1 だけ、TypeName と SecurityName は Frame 3 だけ(他は false と null)。
/// Settlement と Cash は行が無ければ null(未約定の取引)。Cash は現金取引のときだけ読む。History は TH_DTS・TH_ST_ID の昇順に最大 3 行。
/// </summary>
public readonly record struct TradeUpdateTrade(
    long TradeId,long AccountId,long Dts,Code8 TradeTypeId,string? TypeName,string? SecurityName,int Quantity,decimal BidPrice,string ExecName,bool IsCash,bool IsMarket,decimal? TradePrice,
    TradeUpdateSettlement? Settlement,TradeUpdateCash? Cash,TradeUpdateHistory[] History);
/// <summary>SETTLEMENT の SE_AMT・SE_CASH_DUE_DATE・SE_CASH_TYPE。</summary>
public readonly record struct TradeUpdateSettlement(decimal Amount,long CashDueDate,string CashType);
/// <summary>CASH_TRANSACTION の CT_AMT・CT_DTS・CT_NAME。</summary>
public readonly record struct TradeUpdateCash(decimal Amount,long Dts,string? Name);
/// <summary>TRADE_HISTORY の TH_DTS・TH_ST_ID。</summary>
public readonly record struct TradeUpdateHistory(long Dts,Code8 StatusId);
/// <summary>書き戻す値(取引 ID と新しい文字列)。1 つの取引の中で主キーの昇順に並べてから書く。</summary>
public readonly record struct TradeUpdateWrite(long TradeId,string Value);
