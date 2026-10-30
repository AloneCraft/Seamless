namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Order(3.3.7)のフレーム 1〜4。手順と問い合わせは既存の <see cref="TradeOrderFrames"/> の Frame1〜Frame4 を使い、ここは構造体との変換だけ。
/// 手数料の金額(comm_amount)はハーネスが comm_rate から計算して Frame 4 に渡す(丸めもハーネス)。取引 ID は Frame 4 が採番する(エンジンが受け持つ)。
/// フレーム 5(巻き戻し)とフレーム 6(確定)はエンジンが取引の開始・確定・巻き戻しとして処理する。
/// </summary>
internal sealed class TradeOrderHarness{
    private readonly TradeOrderFrames frames=new();
    public void Frame1(TpcEDatabase db,TradeOrderFrame1Input input,TradeOrderFrame1Output output){
        if(!this.frames.Frame1(db,input.AcctId,out var account,out var customer)){
            output.NumFound=0;
            return;
        }
        var broker=db.Broker.Get(new BrokerKey(account.BrokerId));
        output.NumFound=1;
        output.AcctName=account.Name ?? "";
        output.BrokerId=account.BrokerId;
        output.BrokerName=broker.Name;
        output.CustId=customer.Id;
        output.CustFName=customer.FirstName;
        output.CustLName=customer.LastName;
        output.CustTier=customer.Tier;
        output.TaxId=customer.TaxId.ToString();
        output.TaxStatus=account.TaxStatus;
    }
    /// <summary>権限があれば ap_acl に ACL(ACCOUNT_PERMISSION.AP_ACL)を入れる。無ければ空(ハーネスが誤りにして巻き戻す)。</summary>
    public void Frame2(TpcEDatabase db,TradeOrderFrame2Input input,TradeOrderFrame2Output output){
        output.ApAcl="";
        var taxId=Code24.From(input.ExecTaxId);
        if(this.frames.Frame2(db,input.AcctId,input.ExecFName,input.ExecLName,taxId)){
            output.ApAcl=db.AccountPermission.Get(new AccountPermissionKey(input.AcctId,taxId)).Acl.ToString();
        }
    }
    public void Frame3(TpcEDatabase db,TradeOrderFrame3Input input,TradeOrderFrame3Output output){
        var symbol=default(Code16);
        if(input.Symbol.Length>0){
            symbol=Code16.From(input.Symbol);
        }
        var estimate=this.frames.Frame3(db,input.AcctId,input.CustId,input.CustTier,input.TaxStatus,input.IsLifo!=0,Code8.From(input.Issue),input.TradeQty,Code8.From(input.TradeTypeId),input.TypeIsMargin!=0,input.CoName,(decimal)input.RequestedPrice,symbol,Code8.From(input.StPendingId),Code8.From(input.StSubmittedId));
        var security=db.Security.Get(new SecurityKey(estimate.Symbol));
        output.AcctAssets=(double)estimate.Assets;
        output.BuyValue=(double)estimate.BuyValue;
        output.SellValue=(double)estimate.SellValue;
        output.TaxAmount=(double)estimate.TaxAmount;
        output.ChargeAmount=(double)estimate.Charge;
        output.CommRate=(double)estimate.CommissionRate;
        output.MarketPrice=(double)estimate.MarketPrice;
        output.RequestedPrice=(double)estimate.RequestedPrice;
        output.TypeIsMarket=Convert.ToInt32(estimate.IsMarket);
        output.TypeIsSell=Convert.ToInt32(estimate.IsSell);
        output.CoName=db.Company.Get(new CompanyKey(security.CompanyId)).Name;
        output.SName=estimate.SecurityName;
        output.StatusId=estimate.StatusId.ToString();
        output.Symbol=estimate.Symbol.ToString();
    }
    public void Frame4(TpcEDatabase db,TradeOrderFrame4Input input,TradeOrderFrame4Output output,long tradeId,long now){
        this.frames.Frame4(db,tradeId,input.AcctId,input.BrokerId,(decimal)input.ChargeAmount,(decimal)input.CommAmount,input.ExecName,input.IsCash!=0,input.IsLifo!=0,(decimal)input.RequestedPrice,Code8.From(input.StatusId),Code16.From(input.Symbol),input.TradeQty,Code8.From(input.TradeTypeId),input.TypeIsMarket!=0,now);
        output.TradeId=tradeId;
    }
}
