namespace Shared.TpcE.Transactions;
/// <summary>
/// Trade-Status(3.3.9)のフレーム 1 を、EGen の構造体の入出力で実行する。
/// 手順と問い合わせは既存の <see cref="TradeStatusFrames"/> を使い、ここは構造体との変換だけを行う。
/// ハーネスは出力の num_found が 50 でないとき status を TSF1_ERROR1(-911)にするので、フレームは status を決めない。
/// </summary>
internal sealed class TradeStatusHarness{
    private readonly TradeStatusFrames frames=new();
    public void Frame1(TpcEDatabase db,TradeStatusTxnInput input,TradeStatusFrame1Output output){
        var result=this.frames.Run(db,new TradeStatusInput{AccountId=input.AcctId});
        var count=Math.Min(result.Trades.Length,output.TradeIdCount);
        output.NumFound=result.Trades.Length;
        for(var index=0;index<count;index++){
            var trade=result.Trades[index];
            output.SetTradeId(index,trade.TradeId);
            output.SetTradeDts(index,EGenTimestamp.FromMilliseconds(trade.TradeDts));
            output.SetStatusName(index,trade.StatusName);
            output.SetTypeName(index,trade.TypeName);
            output.SetSymbol(index,trade.Symbol.ToString());
            output.SetTradeQty(index,trade.Quantity);
            output.SetExecName(index,trade.ExecName);
            output.SetCharge(index,(double)trade.Charge);
            output.SetSName(index,trade.SecurityName);
            output.SetExName(index,trade.ExchangeName);
        }
        output.CustLName=result.CustomerLastName;
        output.CustFName=result.CustomerFirstName;
        output.BrokerName=result.BrokerName;
    }
}
