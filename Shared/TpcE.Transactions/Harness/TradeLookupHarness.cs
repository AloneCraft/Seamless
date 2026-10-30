namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Lookup(3.3.6)のフレーム 1〜4。取引ごとに frame_to_execute で選ばれた 1 つだけが呼ばれる(フレームが 1 取引)。
/// 手順と問い合わせは既存の <see cref="TradeLookupFrames"/> を使い、ここは構造体との変換だけ。NULL の項目は指示子を -1 にする。
/// </summary>
internal sealed class TradeLookupHarness{
    /// <summary>EGen の TradeLookupMaxTradeHistoryRowsReturned。</summary>
    private const int HistoryRows=3;
    private readonly TradeLookupFrames frames=new();
    public void Frame1(TpcEDatabase db,TradeLookupFrame1Input input,TradeLookupFrame1Output output,long now){
        var ids=new long[input.MaxTrades];
        for(var index=0;index<ids.Length&&index<input.TradeIdCount;index++){
            ids[index]=input.GetTradeId(index);
        }
        var result=this.frames.Run(db,new TradeLookupInput{Now=now,FrameToExecute=1,TradeIds=ids,MaxTrades=input.MaxTrades});
        output.NumFound=result.NumFound;
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
            FillSettlement1(info,source);
            FillCash1(info,source);
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
    public void Frame2(TpcEDatabase db,TradeLookupFrame2Input input,TradeLookupFrame2Output output,long now){
        var result=this.frames.Run(db,new TradeLookupInput{
            Now=now,
            FrameToExecute=2,
            AccountId=input.AcctId,
            MaxTrades=input.MaxTrades,
            StartTradeDts=input.StartTradeDts.ToMilliseconds(),
            EndTradeDts=input.EndTradeDts.ToMilliseconds(),
        });
        output.NumFound=result.NumFound;
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
            if(source.Settlement is not null){
                info.SettlementAmount=(double)source.Settlement.Value.Amount;
                info.SettlementCashDueDate=EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(source.Settlement.Value.CashDueDate));
                info.SettlementCashType=source.Settlement.Value.CashType;
                info.SettlementAmountInd=0;
                info.SettlementCashDueDateInd=0;
                info.SettlementCashTypeInd=0;
            }else{
                info.SettlementAmountInd=-1;
                info.SettlementCashDueDateInd=-1;
                info.SettlementCashTypeInd=-1;
            }
            info.CashTransactionAmountInd=-1;
            info.CashTransactionDtsInd=-1;
            info.CashTransactionNameInd=-1;
            if(source.Cash is not null){
                info.CashTransactionAmount=(double)source.Cash.Value.Amount;
                info.CashTransactionDts=EGenTimestamp.FromMilliseconds(source.Cash.Value.Dts);
                info.CashTransactionName=source.Cash.Value.Name ?? "";
                info.CashTransactionAmountInd=0;
                info.CashTransactionDtsInd=0;
                info.CashTransactionNameInd=0;
            }
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
    public void Frame3(TpcEDatabase db,TradeLookupFrame3Input input,TradeLookupFrame3Output output,long now){
        var result=this.frames.Run(db,new TradeLookupInput{
            Now=now,
            FrameToExecute=3,
            Symbol=Code16.From(input.Symbol),
            MaxAccountId=input.MaxAcctId,
            MaxTrades=input.MaxTrades,
            StartTradeDts=input.StartTradeDts.ToMilliseconds(),
            EndTradeDts=input.EndTradeDts.ToMilliseconds(),
        });
        output.NumFound=result.NumFound;
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
            info.CashTransactionAmountInd=-1;
            info.CashTransactionDtsInd=-1;
            info.CashTransactionNameInd=-1;
            if(source.Cash is not null){
                info.CashTransactionAmount=(double)source.Cash.Value.Amount;
                info.CashTransactionDts=EGenTimestamp.FromMilliseconds(source.Cash.Value.Dts);
                info.CashTransactionName=source.Cash.Value.Name ?? "";
                info.CashTransactionAmountInd=0;
                info.CashTransactionDtsInd=0;
                info.CashTransactionNameInd=0;
            }
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
    public void Frame4(TpcEDatabase db,TradeLookupFrame4Input input,TradeLookupFrame4Output output,long now){
        var result=this.frames.Run(db,new TradeLookupInput{
            Now=now,
            FrameToExecute=4,
            AccountId=input.AcctId,
            StartTradeDts=input.TradeDts.ToMilliseconds(),
        });
        output.NumTradesFound=result.NumTradesFound;
        output.TradeId=result.TradeId;
        output.NumFound=result.HoldingHistory.Length;
        for(var index=0;index<result.HoldingHistory.Length&&index<output.TradeInfoCount;index++){
            var source=result.HoldingHistory[index];
            var info=output.GetTradeInfo(index);
            info.HoldingHistoryId=source.HoldingTradeId;
            info.HoldingHistoryTradeId=source.TradeId;
            info.QuantityBefore=source.BeforeQuantity;
            info.QuantityAfter=source.AfterQuantity;
            info.HoldingHistoryIdInd=0;
            info.HoldingHistoryTradeIdInd=0;
            info.QuantityBeforeInd=0;
            info.QuantityAfterInd=0;
        }
    }
    private static void FillSettlement1(TradeLookupFrame1TradeInfo info,TradeLookupTrade source){
        if(source.Settlement is not null){
            info.SettlementAmount=(double)source.Settlement.Value.Amount;
            info.SettlementCashDueDate=EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(source.Settlement.Value.CashDueDate));
            info.SettlementCashType=source.Settlement.Value.CashType;
            info.SettlementAmountInd=0;
            info.SettlementCashDueDateInd=0;
            info.SettlementCashTypeInd=0;
        }else{
            info.SettlementAmountInd=-1;
            info.SettlementCashDueDateInd=-1;
            info.SettlementCashTypeInd=-1;
        }
    }
    private static void FillCash1(TradeLookupFrame1TradeInfo info,TradeLookupTrade source){
        info.CashTransactionAmountInd=-1;
        info.CashTransactionDtsInd=-1;
        info.CashTransactionNameInd=-1;
        if(source.Cash is not null){
            info.CashTransactionAmount=(double)source.Cash.Value.Amount;
            info.CashTransactionDts=EGenTimestamp.FromMilliseconds(source.Cash.Value.Dts);
            info.CashTransactionName=source.Cash.Value.Name ?? "";
            info.CashTransactionAmountInd=0;
            info.CashTransactionDtsInd=0;
            info.CashTransactionNameInd=0;
        }
    }
}
