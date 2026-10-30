namespace Benchmark.TpcE.Core;
// Market-Feed(TPC-E 3.3.3)の入出力。
/// <summary>ティッカーの 1 項目(symbol・price_quote・trade_qty)。</summary>
public readonly record struct TickerEntry(Code16 Symbol,decimal PriceQuote,int TradeQuantity);
/// <summary>発動した指値注文(MEE へ送る。PriceQuote は TR_BID_PRICE)。</summary>
public readonly record struct TriggeredTrade(Code16 Symbol,long TradeId,decimal PriceQuote,int TradeQuantity,Code8 TradeTypeId);
/// <summary>Market-Feed の入力(3.3.3.1)。</summary>
public sealed record MarketFeedInput:TxnRequest{
    public override TxnKind Kind=>TxnKind.MarketFeed;
    public TickerEntry[] Entries{get;init;}=[];
    public Code8 StatusSubmitted{get;init;}
    public Code8 TypeLimitBuy{get;init;}
    public Code8 TypeLimitSell{get;init;}
    public Code8 TypeStopLoss{get;init;}
    public int UniqueSymbols{get;init;}
}
/// <summary>Market-Feed の出力。num_updated が unique_symbols を下回ると -311。</summary>
public sealed record MarketFeedOutput:TxnResult{
    public int NumUpdated{get;init;}
    public int SendLength{get;init;}
    public TriggeredTrade[] Triggered{get;init;}=[];
}
