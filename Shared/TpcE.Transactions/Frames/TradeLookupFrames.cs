namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Lookup(TPC-E 3.3.6)の手順。読み取りだけで、frame_to_execute のフレームを 1 つ実行する。
/// LINQ は書かない(問い合わせは TradeLookupQueries に書く)。
/// </summary>
internal sealed class TradeLookupFrames{
    /// <summary>Frame 4 で返す保有履歴の上限(3.3.6.6 の擬似コードの「first 20 rows」とハーネスの検査)。</summary>
    private const int MaxHoldingHistoryRows=20;
    private readonly TradeLookupQueries queries=new();
    public TradeLookupOutput Run(TpcEDatabase db,TradeLookupInput input){
        switch(input.FrameToExecute){
            case 1:
                return this.Frame1(db,input);
            case 2:
                return this.Frame2(db,input);
            case 3:
                return this.Frame3(db,input);
            case 4:
                return this.Frame4(db,input);
            default:
                throw new ArgumentOutOfRangeException(nameof(input),input.FrameToExecute,"frame_to_execute must be 1 to 4.");
        }
    }
    /// <summary>Frame 1(3.3.6.3): 指定の取引 ID ごとに、TRADE × TRADE_TYPE・SETTLEMENT・CASH_TRANSACTION・TRADE_HISTORY を読む。</summary>
    private TradeLookupOutput Frame1(TpcEDatabase db,TradeLookupInput input){
        var statusIds=this.queries.StatusIds(db);
        var count=Math.Min(Math.Max(0,input.MaxTrades),input.TradeIds.Length);
        var trades=new List<TradeLookupTrade>(count);
        for(var index=0;index<count;index++){
            if(!db.Trade.TryGet(new TradeKey(input.TradeIds[index]),out var trade)){
                // 行が無ければ num_found は増えない(仕様の row_count が 0)。
                continue;
            }
            var isMarket=db.TradeType.Get(new TradeTypeKey(trade.TypeId)).IsMarket;
            trades.Add(this.Detail(db,trade,isMarket,statusIds));
        }
        var status=0;
        if(trades.Count!=input.MaxTrades){
            status=-611;
        }
        return new TradeLookupOutput{Status=status,FrameExecuted=1,NumFound=trades.Count,Trades=trades.ToArray()};
    }
    /// <summary>Frame 2(3.3.6.4): 口座の期間内の取引を T_DTS の昇順に最大 max_trades 件読み、それぞれの決済・現金取引・履歴を読む。</summary>
    private TradeLookupOutput Frame2(TpcEDatabase db,TradeLookupInput input){
        var statusIds=this.queries.StatusIds(db);
        var rows=this.queries.AccountTrades(db,input.AccountId,input.StartTradeDts,input.EndTradeDts,Math.Max(0,input.MaxTrades));
        var trades=this.Details(db,rows,statusIds);
        var status=0;
        if(trades.Length<0||trades.Length>input.MaxTrades){
            status=-621;
        }else if(trades.Length==0){
            status=621;
        }
        return new TradeLookupOutput{Status=status,FrameExecuted=2,NumFound=trades.Length,Trades=trades};
    }
    /// <summary>Frame 3(3.3.6.5): 銘柄の期間内の取引を T_DTS の昇順に最大 max_trades 件読み、それぞれの決済・現金取引・履歴を読む。</summary>
    private TradeLookupOutput Frame3(TpcEDatabase db,TradeLookupInput input){
        var statusIds=this.queries.StatusIds(db);
        var rows=this.queries.SymbolTrades(db,input.Symbol,input.StartTradeDts,input.EndTradeDts,Math.Max(0,input.MaxTrades));
        var trades=this.Details(db,rows,statusIds);
        var status=0;
        if(trades.Length<0||trades.Length>input.MaxTrades){
            status=-631;
        }else if(trades.Length==0){
            status=631;
        }
        return new TradeLookupOutput{Status=status,FrameExecuted=3,NumFound=trades.Length,Trades=trades};
    }
    /// <summary>
    /// Frame 4(3.3.6.6): 口座の start_trade_dts 以降で最初の取引を 1 件探し、その取引が変えた保有(HH_H_T_ID)ごとに、
    /// その保有を変えた取引の履歴を最大 20 行読む。
    /// </summary>
    private TradeLookupOutput Frame4(TpcEDatabase db,TradeLookupInput input){
        var first=db.AccountTrades.From(input.AccountId,new TimedId(input.StartTradeDts,long.MinValue),1);
        long tradeId=0;
        var history=new List<TradeLookupHoldingHistory>(TradeLookupFrames.MaxHoldingHistoryRows);
        if(first.Length>0){
            tradeId=first[0].Id;
            // 副問い合わせ「HH_T_ID = trade_id の HH_H_T_ID」(保有の ID の昇順)。
            foreach(var holdingTradeId in db.HoldingHistoryByTrade.All(tradeId)){
                var remaining=TradeLookupFrames.MaxHoldingHistoryRows-history.Count;
                if(remaining<=0){
                    break;
                }
                history.AddRange(this.queries.HoldingChanges(db,holdingTradeId,remaining));
            }
        }
        var status=0;
        if(first.Length==0){
            status=641;
        }else if(first.Length!=1){
            status=-641;
        }else if(history.Count<1||history.Count>TradeLookupFrames.MaxHoldingHistoryRows){
            status=-642;
        }
        return new TradeLookupOutput{Status=status,FrameExecuted=4,NumFound=history.Count,NumTradesFound=first.Length,TradeId=tradeId,HoldingHistory=history.ToArray()};
    }
    private TradeLookupTrade[] Details(TpcEDatabase db,TradeRow[] rows,Code8[] statusIds){
        var trades=new TradeLookupTrade[rows.Length];
        for(var index=0;index<rows.Length;index++){
            trades[index]=this.Detail(db,rows[index],false,statusIds);
        }
        return trades;
    }
    /// <summary>取引 1 件の決済(SETTLEMENT)・現金取引(現金取引のときだけ CASH_TRANSACTION)・履歴(TRADE_HISTORY 最大 3 行)を読む。</summary>
    private TradeLookupTrade Detail(TpcEDatabase db,TradeRow trade,bool isMarket,Code8[] statusIds){
        TradeLookupSettlement? settlement=null;
        if(db.Settlement.TryGet(new SettlementKey(trade.Id),out var settlementRow)){
            settlement=new TradeLookupSettlement(settlementRow.Amount,settlementRow.CashDueDate,settlementRow.CashType);
        }
        TradeLookupCash? cash=null;
        if(trade.IsCash&&db.CashTransaction.TryGet(new CashTransactionKey(trade.Id),out var cashRow)){
            cash=new TradeLookupCash(cashRow.Amount,cashRow.Dts,cashRow.Name);
        }
        var history=this.queries.History(db,trade.Id,statusIds);
        return new TradeLookupTrade(trade.Id,trade.AccountId,trade.Dts,trade.TypeId,trade.Quantity,trade.BidPrice,trade.ExecName,trade.IsCash,isMarket,trade.TradePrice,settlement,cash,history);
    }
}
