namespace Shared.TpcE.Transactions;
/// <summary>EGenTxnHarness から呼ばれるフレームの入口。フレームごとの処理は *Harness クラスに置く。</summary>
internal sealed partial class TpcEEngine:IHarnessFrameEngine{
    private readonly TradeStatusHarness tradeStatusHarness=new();
    private readonly BrokerVolumeHarness brokerVolumeHarness=new();
    private readonly MarketWatchHarness marketWatchHarness=new();
    private readonly SecurityDetailHarness securityDetailHarness=new();
    private readonly TradeCleanupHarness tradeCleanupHarness=new();
    private readonly DataMaintenanceHarness dataMaintenanceHarness=new();
    private readonly MarketFeedHarness marketFeedHarness=new();
    private readonly TradeLookupHarness tradeLookupHarness=new();
    private readonly TradeUpdateHarness tradeUpdateHarness=new();
    private readonly CustomerPositionHarness customerPositionHarness=new();
    private readonly TradeOrderHarness tradeOrderHarness=new();
    private readonly TradeResultHarness tradeResultHarness=new();
    private long nextTradeId;
    private long lastNow;
    public Func<long>? Clock{get;set;}
    public void ExecuteFrame(HarnessFrame frame,Span<byte> input,Span<byte> output,IMarketSink market){
        switch(frame){
            case HarnessFrame.TradeStatus1:
                this.BeginTransaction();
                this.tradeStatusHarness.Frame1(this.db,new TradeStatusTxnInput(input),new TradeStatusFrame1Output(output));
                this.CommitTransaction();
                break;
            case HarnessFrame.BrokerVolume1:
                this.BeginTransaction();
                this.brokerVolumeHarness.Frame1(this.db,new BrokerVolumeTxnInput(input),new BrokerVolumeFrame1Output(output));
                this.CommitTransaction();
                break;
            case HarnessFrame.MarketWatch1:
                this.BeginTransaction();
                this.marketWatchHarness.Frame1(this.db,new MarketWatchTxnInput(input),new MarketWatchFrame1Output(output));
                this.CommitTransaction();
                break;
            case HarnessFrame.SecurityDetail1:
                this.BeginTransaction();
                this.securityDetailHarness.Frame1(this.db,new SecurityDetailTxnInput(input),new SecurityDetailFrame1Output(output));
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeCleanup1:
                this.BeginTransaction();
                this.tradeCleanupHarness.Frame1(this.db,new TradeCleanupTxnInput(input),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.DataMaintenance1:
                this.BeginTransaction();
                this.dataMaintenanceHarness.Frame1(this.db,new DataMaintenanceTxnInput(input),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.MarketFeed1:
                this.BeginTransaction();
                this.marketFeedHarness.Frame1(this.db,new MarketFeedFrame1Input(input),new MarketFeedFrame1Output(output),market,this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeLookup1:
                this.BeginTransaction();
                this.tradeLookupHarness.Frame1(this.db,new TradeLookupFrame1Input(input),new TradeLookupFrame1Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeLookup2:
                this.BeginTransaction();
                this.tradeLookupHarness.Frame2(this.db,new TradeLookupFrame2Input(input),new TradeLookupFrame2Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeLookup3:
                this.BeginTransaction();
                this.tradeLookupHarness.Frame3(this.db,new TradeLookupFrame3Input(input),new TradeLookupFrame3Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeLookup4:
                this.BeginTransaction();
                this.tradeLookupHarness.Frame4(this.db,new TradeLookupFrame4Input(input),new TradeLookupFrame4Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeUpdate1:
                this.BeginTransaction();
                this.tradeUpdateHarness.Frame1(this.db,new TradeUpdateFrame1Input(input),new TradeUpdateFrame1Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeUpdate2:
                this.BeginTransaction();
                this.tradeUpdateHarness.Frame2(this.db,new TradeUpdateFrame2Input(input),new TradeUpdateFrame2Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeUpdate3:
                this.BeginTransaction();
                this.tradeUpdateHarness.Frame3(this.db,new TradeUpdateFrame3Input(input),new TradeUpdateFrame3Output(output),this.NextNow());
                this.CommitTransaction();
                break;
            case HarnessFrame.CustomerPosition1:
                // 取引を開始する。確定はフレーム 2(履歴あり)かフレーム 3(履歴なし)。
                this.BeginTransaction();
                this.customerPositionHarness.Frame1(this.db,new CustomerPositionFrame1Input(input),new CustomerPositionFrame1Output(output));
                break;
            case HarnessFrame.CustomerPosition2:
                this.customerPositionHarness.Frame2(this.db,new CustomerPositionFrame2Input(input),new CustomerPositionFrame2Output(output));
                this.CommitTransaction();
                break;
            case HarnessFrame.CustomerPosition3:
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeOrder1:
                this.BeginTransaction();
                this.tradeOrderHarness.Frame1(this.db,new TradeOrderFrame1Input(input),new TradeOrderFrame1Output(output));
                break;
            case HarnessFrame.TradeOrder2:
                this.tradeOrderHarness.Frame2(this.db,new TradeOrderFrame2Input(input),new TradeOrderFrame2Output(output));
                break;
            case HarnessFrame.TradeOrder3:
                this.tradeOrderHarness.Frame3(this.db,new TradeOrderFrame3Input(input),new TradeOrderFrame3Output(output));
                break;
            case HarnessFrame.TradeOrder4:
                this.tradeOrderHarness.Frame4(this.db,new TradeOrderFrame4Input(input),new TradeOrderFrame4Output(output),this.AllocateTradeId(),this.NextNow());
                break;
            case HarnessFrame.TradeOrder5:
                // 意図的な巻き戻し、または権限が無いとき。採番は戻さない。
                if(this.db.Log.IsRecording){
                    this.db.Log.Rollback();
                }
                break;
            case HarnessFrame.TradeOrder6:
                this.CommitTransaction();
                break;
            case HarnessFrame.TradeResult1:
                this.BeginTransaction();
                this.tradeResultHarness.Frame1(this.db,new TradeResultFrame1Input(input),new TradeResultFrame1Output(output));
                break;
            case HarnessFrame.TradeResult2:
                this.tradeResultHarness.Frame2(this.db,new TradeResultFrame2Input(input),new TradeResultFrame2Output(output),this.NextNow());
                break;
            case HarnessFrame.TradeResult3:
                this.tradeResultHarness.Frame3(this.db,new TradeResultFrame3Input(input),new TradeResultFrame3Output(output));
                break;
            case HarnessFrame.TradeResult4:
                this.tradeResultHarness.Frame4(this.db,new TradeResultFrame4Input(input),new TradeResultFrame4Output(output));
                break;
            case HarnessFrame.TradeResult5:
                this.tradeResultHarness.Frame5(this.db,new TradeResultFrame5Input(input));
                break;
            case HarnessFrame.TradeResult6:
                this.tradeResultHarness.Frame6(this.db,new TradeResultFrame6Input(input),new TradeResultFrame6Output(output));
                this.CommitTransaction();
                break;
            default:
                throw new NotImplementedException($"Harness frame {frame} is not implemented yet.");
        }
    }
    public void EndTransaction(){
        // ハーネスが最後のフレームまで進まなかった(誤りで打ち切った)ときのために、開いたままの取引を巻き戻す。
        if(this.db.Log.IsRecording){
            this.db.Log.Rollback();
        }
    }
    private void BeginTransaction(){
        if(!this.db.Log.IsRecording){
            this.db.Log.Begin();
        }
    }
    private void CommitTransaction(){
        if(this.db.Log.IsRecording){
            this.db.Log.Commit();
        }
    }
    public long NextTradeId{
        get{
            if(this.nextTradeId==0){
                // まだ採番していなければ、DB にある最大の取引 ID の次から始める。
                long maximum=0;
                foreach(var row in this.db.Trade.EnumerateRows()){
                    if(row.Id>maximum){
                        maximum=row.Id;
                    }
                }
                this.nextTradeId=maximum+1;
            }
            return this.nextTradeId;
        }
        set=>this.nextTradeId=value;
    }
    private long AllocateTradeId(){
        var id=this.NextTradeId;
        this.nextTradeId=id+1;
        return id;
    }
    /// <summary>取引の時刻。実時計を使うが、単調に増える(同じ時刻の取引を作らない)。</summary>
    private long NextNow(){
        if(this.Clock is not null){
            return this.Clock();
        }
        var now=TpcETime.FromDateTime(DateTime.Now);
        if(now<=this.lastNow){
            now=this.lastNow+1;
        }
        this.lastNow=now;
        return now;
    }
}
