namespace Benchmark.TpcE.Core;
// Trade-Lookup(TPC-E 3.3.6)の入出力。読み取りだけの取引で、互いに排他な 4 つのフレームのうち 1 つを実行する。
/// <summary>Trade-Lookup の入力(3.3.6.1)。使わない項目は 0 か空のまま。</summary>
public sealed record TradeLookupInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.TradeLookup;
    /// <summary>frame_to_execute(1〜4)。</summary>
    public int FrameToExecute{get;init;}
    /// <summary>trade_id[](Frame 1。初期取引から選んだ取引 ID)。</summary>
    public long[] TradeIds{get;init;}=[];
    /// <summary>acct_id(Frame 2・4)。</summary>
    public long AccountId{get;init;}
    /// <summary>max_acct_id(Frame 3。仕様でも検索条件には使わない)。</summary>
    public long MaxAccountId{get;init;}
    /// <summary>max_trades(Frame 1・2・3)。</summary>
    public int MaxTrades{get;init;}
    /// <summary>start_trade_dts(Frame 2・3・4)。</summary>
    public long StartTradeDts{get;init;}
    /// <summary>end_trade_dts(Frame 2・3)。</summary>
    public long EndTradeDts{get;init;}
    /// <summary>symbol(Frame 3)。</summary>
    public Code16 Symbol{get;init;}
}
/// <summary>
/// Trade-Lookup の出力。Frame 1・2・3 は <see cref="Trades"/>、Frame 4 は <see cref="TradeId"/>・<see cref="NumTradesFound"/>・<see cref="HoldingHistory"/> を使う。
/// status は Frame 1: -611、Frame 2: -621/+621、Frame 3: -631/+631、Frame 4: +641/-641/-642(3.3.6.3〜3.3.6.6)。
/// </summary>
public sealed record TradeLookupOutput:TxnResult{
    /// <summary>frame_executed。</summary>
    public int FrameExecuted{get;init;}
    /// <summary>num_found(Frame 1・2・3 は取引の行数、Frame 4 は保有履歴の行数)。</summary>
    public int NumFound{get;init;}
    /// <summary>Frame 1・2・3 で見つけた取引(Frame 1 は入力の順、Frame 2・3 は T_DTS の昇順)。</summary>
    public TradeLookupTrade[] Trades{get;init;}=[];
    /// <summary>num_trades_found(Frame 4)。</summary>
    public int NumTradesFound{get;init;}
    /// <summary>trade_id(Frame 4。見つからなければ 0)。</summary>
    public long TradeId{get;init;}
    /// <summary>Frame 4 の保有履歴(最大 20 行。仕様に並びの指定が無いので HH_H_T_ID・HH_T_ID の昇順)。</summary>
    public TradeLookupHoldingHistory[] HoldingHistory{get;init;}=[];
}
/// <summary>
/// 取引 1 件の情報(TRADE と、SETTLEMENT・CASH_TRANSACTION・TRADE_HISTORY)。どのフレームでも TRADE の行の値をすべて入れる。
/// IsMarket は Frame 1 だけ(TRADE_TYPE を結合する)、他のフレームでは false。Settlement と Cash は行が無ければ null(未約定の取引)。
/// Cash は現金取引のときだけ読む。History は TH_DTS・TH_ST_ID の昇順に最大 3 行。
/// </summary>
public readonly record struct TradeLookupTrade(
    long TradeId,long AccountId,long Dts,Code8 TradeTypeId,int Quantity,decimal BidPrice,string ExecName,bool IsCash,bool IsMarket,decimal? TradePrice,
    TradeLookupSettlement? Settlement,TradeLookupCash? Cash,TradeLookupHistory[] History);
/// <summary>SETTLEMENT の SE_AMT・SE_CASH_DUE_DATE・SE_CASH_TYPE。</summary>
public readonly record struct TradeLookupSettlement(decimal Amount,long CashDueDate,string CashType);
/// <summary>CASH_TRANSACTION の CT_AMT・CT_DTS・CT_NAME。</summary>
public readonly record struct TradeLookupCash(decimal Amount,long Dts,string? Name);
/// <summary>TRADE_HISTORY の TH_DTS・TH_ST_ID。</summary>
public readonly record struct TradeLookupHistory(long Dts,Code8 StatusId);
/// <summary>HOLDING_HISTORY の 1 行(Frame 4)。</summary>
public readonly record struct TradeLookupHoldingHistory(long HoldingTradeId,long TradeId,int BeforeQuantity,int AfterQuantity);
