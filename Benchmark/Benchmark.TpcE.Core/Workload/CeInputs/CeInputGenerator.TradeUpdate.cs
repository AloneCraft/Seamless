namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>
    /// Trade-Update の入力を作る(3.3.10.1、6.4.1)。フレームは 1・2・3 を 33:33:34 で選び、max_trades と max_updates は 20。
    /// 取引 ID・口座・銘柄・期間の選び方は Trade-Lookup の同じ番号のフレームと同じ。
    /// </summary>
    public TradeUpdateInput TradeUpdate(ref Rng rng){
        Span<int> weights=[TpcESpec.TradeUpdateFrame1Percent,TpcESpec.TradeUpdateFrame2Percent,TpcESpec.TradeUpdateFrame3Percent];
        var frame=rng.Weighted(weights)+1;
        switch(frame){
            case 1:{
                var tradeIds=this.TradeLookupTradeIds(ref rng,TpcESpec.TradeUpdateMaxRows);
                return new TradeUpdateInput{FrameToExecute=1,TradeIds=tradeIds,MaxTrades=tradeIds.Length,MaxUpdates=tradeIds.Length};
            }
            case 2:{
                var customer=this.Model.PickCustomerByTier(ref rng);
                var account=this.Model.PickAccountOf(customer,ref rng);
                return new TradeUpdateInput{FrameToExecute=2,AccountId=account,MaxTrades=TpcESpec.TradeUpdateMaxRows,MaxUpdates=TpcESpec.TradeUpdateMaxRows,StartTradeDts=this.TradeLookupStartDts(ref rng,true),EndTradeDts=this.Context.InitialTradeEnd};
            }
            default:{
                var security=rng.Range(1L,this.Model.Scale.Securities);
                return new TradeUpdateInput{FrameToExecute=3,Symbol=this.Model.SymbolOf(security),MaxAccountId=this.TradeLookupMaxAccountId(),MaxTrades=TpcESpec.TradeUpdateMaxRows,MaxUpdates=TpcESpec.TradeUpdateMaxRows,StartTradeDts=this.TradeLookupStartDts(ref rng,false),EndTradeDts=this.Context.InitialTradeEnd};
            }
        }
    }
}
