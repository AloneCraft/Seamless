namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Result(3.3.8)のフレーム 1〜6。手順と問い合わせは既存の <see cref="TradeResultFrames"/> の Frame1〜Frame6 を使い、ここは構造体との変換だけ。
/// 手数料の金額(comm_amount)と決済額(se_amount)はハーネスが計算して渡す(丸めもハーネス)。フレーム 6 が最後で、エンジンが確定する。
/// </summary>
internal sealed class TradeResultHarness{
    private readonly TradeResultFrames frames=new();
    public void Frame1(TpcEDatabase db,TradeResultFrame1Input input,TradeResultFrame1Output output){
        if(!this.frames.Frame1(db,input.TradeId,out var info)){
            output.NumFound=0;
            return;
        }
        output.NumFound=1;
        output.AcctId=info.Trade.AccountId;
        output.Charge=(double)info.Trade.Charge;
        output.HsQty=checked((int)info.SummaryQuantity);
        output.IsLifo=Convert.ToInt32(info.Trade.Lifo);
        output.Symbol=info.Trade.Symbol.ToString();
        output.TradeQty=info.Trade.Quantity;
        output.TradeIsCash=Convert.ToInt32(info.Trade.IsCash);
        output.TypeId=info.Trade.TypeId.ToString();
        output.TypeName=info.Type.Name;
        output.TypeIsSell=Convert.ToInt32(info.Type.IsSell);
        output.TypeIsMarket=Convert.ToInt32(info.Type.IsMarket);
    }
    public void Frame2(TpcEDatabase db,TradeResultFrame2Input input,TradeResultFrame2Output output,long now){
        var holdings=this.frames.Frame2(db,input.TradeId,input.AcctId,input.HsQty,input.IsLifo!=0,Code16.From(input.Symbol),input.TradeQty,input.TypeIsSell!=0,(decimal)input.TradePrice,now);
        var account=db.CustomerAccount.Get(new CustomerAccountKey(input.AcctId));
        output.BuyValue=(double)holdings.BuyValue;
        output.SellValue=(double)holdings.SellValue;
        output.BrokerId=account.BrokerId;
        output.CustId=account.CustomerId;
        output.TaxStatus=account.TaxStatus;
        output.TradeDts=EGenTimestamp.FromMilliseconds(now);
    }
    public void Frame3(TpcEDatabase db,TradeResultFrame3Input input,TradeResultFrame3Output output){
        output.TaxAmount=(double)this.frames.Frame3(db,input.CustId,(decimal)input.BuyValue,(decimal)input.SellValue,input.TradeId);
    }
    public void Frame4(TpcEDatabase db,TradeResultFrame4Input input,TradeResultFrame4Output output){
        output.CommRate=(double)this.frames.Frame4(db,input.CustId,input.TradeQty,Code16.From(input.Symbol),Code8.From(input.TypeId),out var securityName);
        output.SName=securityName;
    }
    public void Frame5(TpcEDatabase db,TradeResultFrame5Input input){
        this.frames.Frame5(db,input.BrokerId,(decimal)input.CommAmount,Code8.From(input.StCompletedId),input.TradeDts.ToMilliseconds(),input.TradeId,(decimal)input.TradePrice);
    }
    public void Frame6(TpcEDatabase db,TradeResultFrame6Input input,TradeResultFrame6Output output){
        var dueDay=TpcETime.DateOf(input.DueDate.ToMilliseconds());
        output.AcctBal=(double)this.frames.Frame6(db,input.AcctId,dueDay,input.SName,(decimal)input.SeAmount,input.TradeDts.ToMilliseconds(),input.TradeId,input.TradeIsCash!=0,input.TradeQty,input.TypeName);
    }
}
