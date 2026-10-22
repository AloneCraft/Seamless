namespace Shared.TpcE.Transactions;
/// <summary>
/// Market-Feed(3.3.3)のフレーム 1。手順は既存の <see cref="MarketFeedFrames"/> を使い、ここは構造体との変換だけ。
/// 発動した指値注文は、フレームの中で市場(MEE)へ処理させる注文(eMEEProcessOrder)として送る。
/// </summary>
internal sealed class MarketFeedHarness{
    /// <summary>EGen の eMEEProcessOrder。</summary>
    private const int ProcessOrder=0;
    private readonly MarketFeedFrames frames=new();
    public void Frame1(TpcEDatabase db,MarketFeedFrame1Input input,MarketFeedFrame1Output output,IMarketSink market,long now){
        var entries=new List<Benchmark.TpcE.Core.TickerEntry>();
        var symbols=new HashSet<string>(StringComparer.Ordinal);
        for(var index=0;index<20;index++){
            var entry=input.GetEntries(index);
            var symbol=entry.Symbol;
            if(symbol.Length==0){
                continue;
            }
            symbols.Add(symbol);
            entries.Add(new Benchmark.TpcE.Core.TickerEntry(Code16.From(symbol),(decimal)entry.PriceQuote,entry.TradeQty));
        }
        var status=input.StatusAndTradeType;
        var result=this.frames.Run(db,new MarketFeedInput{
            Now=now,
            Entries=entries.ToArray(),
            StatusSubmitted=Code8.From(status.StatusSubmitted),
            TypeLimitBuy=Code8.From(status.TypeLimitBuy),
            TypeLimitSell=Code8.From(status.TypeLimitSell),
            TypeStopLoss=Code8.From(status.TypeStopLoss),
            UniqueSymbols=symbols.Count,
        });
        output.NumUpdated=result.NumUpdated;
        output.SendLen=result.SendLength;
        foreach(var trigger in result.Triggered){
            var bytes=new byte[TradeRequest.Size];
            var request=new TradeRequest(bytes);
            request.PriceQuote=(double)trigger.PriceQuote;
            request.TradeId=trigger.TradeId;
            request.TradeQty=trigger.TradeQuantity;
            request.EAction=ProcessOrder;
            request.Symbol=trigger.Symbol.ToString();
            request.TradeTypeId=trigger.TradeTypeId.ToString();
            market.SendToMarket(bytes);
        }
    }
}
