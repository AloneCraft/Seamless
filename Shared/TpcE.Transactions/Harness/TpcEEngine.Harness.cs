namespace Shared.TpcE.Transactions;
/// <summary>EGenTxnHarness から呼ばれるフレームの入口。フレームごとの処理は *Harness クラスに置く。</summary>
internal sealed partial class TpcEEngine:IHarnessFrameEngine{
    private readonly TradeStatusHarness tradeStatusHarness=new();
    public void ExecuteFrame(HarnessFrame frame,Span<byte> input,Span<byte> output,IMarketSink market){
        switch(frame){
            case HarnessFrame.TradeStatus1:
                this.BeginTransaction();
                this.tradeStatusHarness.Frame1(this.db,new TradeStatusTxnInput(input),new TradeStatusFrame1Output(output));
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
}
