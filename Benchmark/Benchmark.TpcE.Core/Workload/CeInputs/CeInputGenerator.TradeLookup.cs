namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>
    /// Trade-Lookup の入力を作る(3.3.6.1、6.4.1)。フレームは 1・2・3・4 を 30:30:30:10 で選ぶ。
    /// Frame 1: 初期取引から非一様に重ならない取引 ID を 20 個。Frame 2: 層で重みづけた顧客の口座、起点は初期取引の期間の非一様、終点は初期取引の終わり。
    /// Frame 3: 全銘柄から一様、起点は初期取引の期間の一様、終点は初期取引の終わり。Frame 4: Frame 2 と同じ口座、起点は初期取引の期間の一様。
    /// </summary>
    public TradeLookupInput TradeLookup(ref Rng rng){
        Span<int> weights=[TpcESpec.TradeLookupFrame1Percent,TpcESpec.TradeLookupFrame2Percent,TpcESpec.TradeLookupFrame3Percent,TpcESpec.TradeLookupFrame4Percent];
        var frame=rng.Weighted(weights)+1;
        switch(frame){
            case 1:{
                var tradeIds=this.TradeLookupTradeIds(ref rng,TpcESpec.TradeLookupMaxRows);
                return new TradeLookupInput{FrameToExecute=1,TradeIds=tradeIds,MaxTrades=tradeIds.Length};
            }
            case 2:{
                var customer=this.Model.PickCustomerByTier(ref rng);
                var account=this.Model.PickAccountOf(customer,ref rng);
                return new TradeLookupInput{FrameToExecute=2,AccountId=account,MaxTrades=TpcESpec.TradeLookupMaxRows,StartTradeDts=this.TradeLookupStartDts(ref rng,true),EndTradeDts=this.Context.InitialTradeEnd};
            }
            case 3:{
                var security=rng.Range(1L,this.Model.Scale.Securities);
                return new TradeLookupInput{FrameToExecute=3,Symbol=this.Model.SymbolOf(security),MaxAccountId=this.TradeLookupMaxAccountId(),MaxTrades=TpcESpec.TradeLookupMaxRows,StartTradeDts=this.TradeLookupStartDts(ref rng,false),EndTradeDts=this.Context.InitialTradeEnd};
            }
            default:{
                var customer=this.Model.PickCustomerByTier(ref rng);
                var account=this.Model.PickAccountOf(customer,ref rng);
                return new TradeLookupInput{FrameToExecute=4,AccountId=account,StartTradeDts=this.TradeLookupStartDts(ref rng,false)};
            }
        }
    }
    /// <summary>
    /// 初期取引 [1, LastInitialTradeId] から非一様(NURand)に、重ならない取引 ID を最大 count 個選ぶ(Trade-Lookup・Trade-Update の Frame 1)。
    /// 偏りで同じ ID が続いて埋まらないときは一様に切り替える。
    /// </summary>
    private long[] TradeLookupTradeIds(ref Rng rng,int count){
        var last=this.Context.LastInitialTradeId;
        if(last<=0||count<=0){
            return [];
        }
        var size=(int)Math.Min(count,last);
        var result=new long[size];
        var skew=CeInputGenerator.TradeLookupSkew(last);
        var filled=0;
        var attempts=0;
        while(filled<size){
            long tradeId;
            if(attempts<size*16){
                tradeId=rng.NURand(skew,1,last,0);
            }else{
                tradeId=rng.Range(1L,last);
            }
            attempts++;
            if(Array.IndexOf(result,tradeId,0,filled)<0){
                result[filled]=tradeId;
                filled++;
            }
        }
        return result;
    }
    /// <summary>初期取引の期間 [InitialTradeStart, InitialTradeEnd] から起点の時刻を選ぶ(nonUniform なら NURand、そうでなければ一様)。</summary>
    private long TradeLookupStartDts(ref Rng rng,bool nonUniform){
        var start=this.Context.InitialTradeStart;
        var end=this.Context.InitialTradeEnd;
        if(end<=start){
            return start;
        }
        var span=end-start;
        if(nonUniform){
            return start+rng.NURand(CeInputGenerator.TradeLookupSkew(span+1),0,span,0);
        }
        return start+rng.Range(0L,span);
    }
    /// <summary>max_acct_id(Frame 3。検索条件には使わない)。最後の顧客の最大の口座 ID。</summary>
    private long TradeLookupMaxAccountId()=>TpcEModel.AccountId(this.Model.CustomerCount,TpcESpec.MaxAccountLength-1);
    /// <summary>NURand の A(範囲の 1/32〜1/16 程度の 2 のべき乗 - 1)。範囲が小さければ 0(一様)。</summary>
    private static long TradeLookupSkew(long span){
        var power=1L;
        while(power*32<=span){
            power<<=1;
        }
        return power-1;
    }
}
