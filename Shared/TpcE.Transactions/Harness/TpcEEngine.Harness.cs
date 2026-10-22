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
