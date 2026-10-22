namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Update(3.3.10)のフレーム 1〜3。取引ごとに frame_to_execute で選ばれた 1 つだけが呼ばれる(フレームが 1 取引)。
/// 手順と問い合わせは既存の <see cref="TradeUpdateFrames"/> を使い、ここは構造体との変換だけ。NULL の項目は指示子を -1 にする。
/// </summary>
internal sealed class TradeUpdateHarness{
    /// <summary>EGen の TradeUpdateMaxTradeHistoryRowsReturned(= 3)。</summary>
    private const int HistoryRows=3;
    private readonly TradeUpdateFrames frames=new();
    public void Frame1(TpcEDatabase db,TradeUpdateFrame1Input input,TradeUpdateFrame1Output output,long now){
        var ids=new long[input.MaxTrades];
        for(var index=0;index<ids.Length&&index<input.TradeIdCount;index++){
            ids[index]=input.GetTradeId(index);
        }
        var result=this.frames.Run(db,new TradeUpdateInput{Now=now,FrameToExecute=1,TradeIds=ids,MaxTrades=input.MaxTrades,MaxUpdates=input.MaxUpdates});
        output.NumFound=result.NumFound;
        output.NumUpdated=result.NumUpdated;
        for(var index=0;index<result.Trades.Length&&index<output.TradeInfoCount;index++){
            var source=result.Trades[index];
            var info=output.GetTradeInfo(index);
            info.BidPrice=(double)source.BidPrice;
            info.BidPriceInd=0;
            info.ExecName=source.ExecName;
            info.ExecNameInd=0;
            info.IsCash=source.IsCash;
            info.IsCashInd=0;
            info.IsMarket=source.IsMarket;
            info.IsMarketInd=0;
            info.TradePriceInd=-1;
            if(source.TradePrice is not null){
                info.TradePrice=(double)source.TradePrice.Value;
                info.TradePriceInd=0;
            }
            Settlement(source.Settlement,out var settlement);
            info.SettlementAmount=settlement.Amount;
            info.SettlementCashDueDate=settlement.DueDate;
            info.SettlementCashType=settlement.CashType;
            info.SettlementAmountInd=settlement.Indicator;
            info.SettlementCashDueDateInd=settlement.Indicator;
            info.SettlementCashTypeInd=settlement.Indicator;
            Cash(source.Cash,out var cash);
            info.CashTransactionAmount=cash.Amount;
            info.CashTransactionDts=cash.Dts;
            info.CashTransactionName=cash.Name;
            info.CashTransactionAmountInd=cash.Indicator;
            info.CashTransactionDtsInd=cash.Indicator;
            info.CashTransactionNameInd=cash.Indicator;
            for(var row=0;row<HistoryRows;row++){
                info.SetTradeHistoryDtsInd(row,-1);
                info.SetTradeHistoryStatusIdInd(row,-1);
                if(row<source.History.Length){
                    info.SetTradeHistoryDts(row,EGenTimestamp.FromMilliseconds(source.History[row].Dts));
                    info.SetTradeHistoryStatusId(row,source.History[row].StatusId.ToString());
                    info.SetTradeHistoryDtsInd(row,0);
                    info.SetTradeHistoryStatusIdInd(row,0);
                }
            }
        }
    }
    public void Frame2(TpcEDatabase db,TradeUpdateFrame2Input input,TradeUpdateFrame2Output output,long now){
        var result=this.frames.Run(db,new TradeUpdateInput{
            Now=now,
            FrameToExecute=2,
            AccountId=input.AcctId,
            MaxTrades=input.MaxTrades,
            MaxUpdates=input.MaxUpdates,
            StartTradeDts=input.StartTradeDts.ToMilliseconds(),
            EndTradeDts=input.EndTradeDts.ToMilliseconds(),
        });
        output.NumFound=result.NumFound;
        output.NumUpdated=result.NumUpdated;
        for(var index=0;index<result.Trades.Length&&index<output.TradeInfoCount;index++){
            var source=result.Trades[index];
            var info=output.GetTradeInfo(index);
            info.TradeId=source.TradeId;
            info.TradeIdInd=0;
            info.BidPrice=(double)source.BidPrice;
            info.BidPriceInd=0;
            info.ExecName=source.ExecName;
            info.ExecNameInd=0;
            info.IsCash=source.IsCash;
            info.IsCashInd=0;
            info.TradePriceInd=-1;
            if(source.TradePrice is not null){
                info.TradePrice=(double)source.TradePrice.Value;
                info.TradePriceInd=0;
            }
            Settlement(source.Settlement,out var settlement);
            info.SettlementAmount=settlement.Amount;
            info.SettlementCashDueDate=settlement.DueDate;
            info.SettlementCashType=settlement.CashType;
            info.SettlementAmountInd=settlement.Indicator;
            info.SettlementCashDueDateInd=settlement.Indicator;
            info.SettlementCashTypeInd=settlement.Indicator;
            Cash(source.Cash,out var cash);
            info.CashTransactionAmount=cash.Amount;
            info.CashTransactionDts=cash.Dts;
            info.CashTransactionName=cash.Name;
            info.CashTransactionAmountInd=cash.Indicator;
            info.CashTransactionDtsInd=cash.Indicator;
            info.CashTransactionNameInd=cash.Indicator;
            for(var row=0;row<HistoryRows;row++){
                info.SetTradeHistoryDtsInd(row,-1);
                info.SetTradeHistoryStatusIdInd(row,-1);
                if(row<source.History.Length){
                    info.SetTradeHistoryDts(row,EGenTimestamp.FromMilliseconds(source.History[row].Dts));
                    info.SetTradeHistoryStatusId(row,source.History[row].StatusId.ToString());
                    info.SetTradeHistoryDtsInd(row,0);
                    info.SetTradeHistoryStatusIdInd(row,0);
                }
            }
        }
    }
    public void Frame3(TpcEDatabase db,TradeUpdateFrame3Input input,TradeUpdateFrame3Output output,long now){
        var result=this.frames.Run(db,new TradeUpdateInput{
            Now=now,
            FrameToExecute=3,
            Symbol=Code16.From(input.Symbol),
            MaxAccountId=input.MaxAcctId,
            MaxTrades=input.MaxTrades,
            MaxUpdates=input.MaxUpdates,
            StartTradeDts=input.StartTradeDts.ToMilliseconds(),
            EndTradeDts=input.EndTradeDts.ToMilliseconds(),
        });
        output.NumFound=result.NumFound;
        output.NumUpdated=result.NumUpdated;
        for(var index=0;index<result.Trades.Length&&index<output.TradeInfoCount;index++){
            var source=result.Trades[index];
            var info=output.GetTradeInfo(index);
            info.AcctId=source.AccountId;
            info.AcctIdInd=0;
            info.TradeId=source.TradeId;
            info.TradeIdInd=0;
            info.TradeDts=EGenTimestamp.FromMilliseconds(source.Dts);
            info.TradeDtsInd=0;
            info.TradeType=source.TradeTypeId.ToString();
            info.TradeTypeInd=0;
            info.TypeName=source.TypeName ?? "";
            info.TypeNameInd=0;
            info.SName=source.SecurityName ?? "";
            info.SNameInd=0;
            info.Quantity=source.Quantity;
            info.QuantityInd=0;
            info.Price=(double)source.BidPrice;
            info.PriceInd=0;
            if(source.TradePrice is not null){
                info.Price=(double)source.TradePrice.Value;
            }
            info.ExecName=source.ExecName;
            info.ExecNameInd=0;
            info.IsCash=source.IsCash;
            info.IsCashInd=0;
            Settlement(source.Settlement,out var settlement);
            info.SettlementAmount=settlement.Amount;
            info.SettlementCashDueDate=settlement.DueDate;
            info.SettlementCashType=settlement.CashType;
            info.SettlementAmountInd=settlement.Indicator;
            info.SettlementCashDueDateInd=settlement.Indicator;
            info.SettlementCashTypeInd=settlement.Indicator;
            Cash(source.Cash,out var cash);
            info.CashTransactionAmount=cash.Amount;
            info.CashTransactionDts=cash.Dts;
            info.CashTransactionName=cash.Name;
            info.CashTransactionAmountInd=cash.Indicator;
            info.CashTransactionDtsInd=cash.Indicator;
            info.CashTransactionNameInd=cash.Indicator;
            for(var row=0;row<HistoryRows;row++){
                info.SetTradeHistoryDtsInd(row,-1);
                info.SetTradeHistoryStatusIdInd(row,-1);
                if(row<source.History.Length){
                    info.SetTradeHistoryDts(row,EGenTimestamp.FromMilliseconds(source.History[row].Dts));
                    info.SetTradeHistoryStatusId(row,source.History[row].StatusId.ToString());
                    info.SetTradeHistoryDtsInd(row,0);
                    info.SetTradeHistoryStatusIdInd(row,0);
                }
            }
        }
    }
    private readonly record struct SettlementValues(double Amount,EGenTimestamp DueDate,string CashType,int Indicator);
    private readonly record struct CashValues(double Amount,EGenTimestamp Dts,string Name,int Indicator);
    /// <summary>SETTLEMENT の値と、NULL 指示子(値があれば 0、無ければ -1)。</summary>
    private static void Settlement(TradeUpdateSettlement? source,out SettlementValues values){
        if(source is null){
            values=new SettlementValues(0,default,"",-1);
            return;
        }
        values=new SettlementValues((double)source.Value.Amount,EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(source.Value.CashDueDate)),source.Value.CashType,0);
    }
    /// <summary>CASH_TRANSACTION の値と、NULL 指示子。</summary>
    private static void Cash(TradeUpdateCash? source,out CashValues values){
        if(source is null){
            values=new CashValues(0,default,"",-1);
            return;
        }
        values=new CashValues((double)source.Value.Amount,EGenTimestamp.FromMilliseconds(source.Value.Dts),source.Value.Name ?? "",0);
    }
}
