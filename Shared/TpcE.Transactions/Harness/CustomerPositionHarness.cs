namespace Shared.TpcE.Transactions;
/// <summary>
/// Customer-Position(3.3.2)のフレーム 1〜3。フレーム 1 が取引を開始し、フレーム 2(履歴あり)かフレーム 3(履歴なし)が確定する。
/// 手順と問い合わせは既存の <see cref="CustomerPositionFrames"/> の Frame1 / Frame2 を使い、ここは構造体との変換だけ。
/// </summary>
internal sealed class CustomerPositionHarness{
    private readonly CustomerPositionFrames frames=new();
    public void Frame1(TpcEDatabase db,CustomerPositionFrame1Input input,CustomerPositionFrame1Output output){
        var result=this.frames.Frame1(db,input.CustId,Code24.From(input.TaxId));
        output.CustId=result.CustomerId;
        output.AcctLen=result.AccountLength;
        output.CAdId=result.AddressId;
        output.CStId=result.CustomerStatusId.ToString();
        output.CLName=result.LastName;
        output.CFName=result.FirstName;
        output.CMName=result.MiddleInitial ?? "";
        output.CGndr=result.Gender ?? "";
        output.CTier=(char)('0'+result.Tier);
        output.CDob=EGenTimestamp.FromMilliseconds(TpcETime.StartOfDay(result.DateOfBirth));
        output.CCtry1=result.Country1 ?? "";
        output.CArea1=result.Area1 ?? "";
        output.CLocal1=result.Local1 ?? "";
        output.CExt1=result.Extension1 ?? "";
        output.CCtry2=result.Country2 ?? "";
        output.CArea2=result.Area2 ?? "";
        output.CLocal2=result.Local2 ?? "";
        output.CExt2=result.Extension2 ?? "";
        output.CCtry3=result.Country3 ?? "";
        output.CArea3=result.Area3 ?? "";
        output.CLocal3=result.Local3 ?? "";
        output.CExt3=result.Extension3 ?? "";
        output.CEmail1=result.Email1 ?? "";
        output.CEmail2=result.Email2 ?? "";
        for(var index=0;index<result.Accounts.Length&&index<output.AcctIdCount;index++){
            output.SetAcctId(index,result.Accounts[index].AccountId);
            output.SetAssetTotal(index,(double)result.Accounts[index].AssetTotal);
            output.SetCashBal(index,(double)result.Accounts[index].CashBalance);
        }
    }
    public void Frame2(TpcEDatabase db,CustomerPositionFrame2Input input,CustomerPositionFrame2Output output){
        var result=this.frames.Frame2(db,input.AcctId);
        output.HistLen=result.History.Length;
        for(var index=0;index<result.History.Length&&index<output.TradeIdCount;index++){
            var entry=result.History[index];
            output.SetTradeId(index,entry.TradeId);
            output.SetQty(index,entry.Quantity);
            output.SetSymbol(index,entry.Symbol.ToString());
            output.SetTradeStatus(index,entry.TradeStatus);
            output.SetHistDts(index,EGenTimestamp.FromMilliseconds(entry.HistoryDts));
        }
    }
}
