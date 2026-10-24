namespace Shared.TpcE.Transactions;
/// <summary>Trade-Cleanup(3.3.12)のフレーム 1。手順は既存の <see cref="TradeCleanupFrames"/> を使う。出力は無い。</summary>
internal sealed class TradeCleanupHarness{
    private readonly TradeCleanupFrames frames=new();
    public void Frame1(TpcEDatabase db,TradeCleanupTxnInput input,long now){
        this.frames.Run(db,new TradeCleanupInput{
            Now=now,
            StatusCanceled=Code8.From(input.StCanceledId),
            StatusPending=Code8.From(input.StPendingId),
            StatusSubmitted=Code8.From(input.StSubmittedId),
            TradeId=input.StartTradeId,
        });
    }
}
