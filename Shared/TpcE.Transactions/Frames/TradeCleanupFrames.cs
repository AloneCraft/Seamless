namespace Shared.TpcE.Transactions;
/// <summary>Trade-Cleanup(TPC-E 3.3.12)の手順。LINQ は書かない(問い合わせは TradeCleanupQueries に書く)。</summary>
internal sealed class TradeCleanupFrames{
    private readonly TradeCleanupQueries queries=new();
    public TradeCleanupOutput Run(TpcEDatabase db,TradeCleanupInput input){
        var now=input.Now;
        // 発動待ちの注文を全部取り消す(3.3.12.3 手順 2・3)。
        var requests=this.queries.RequestIds(db);
        foreach(var id in requests){
            this.AddHistory(db,id,now,input.StatusSubmitted);
            this.SetStatus(db,id,now,input.StatusCanceled);
            this.AddHistory(db,id,now,input.StatusCanceled);
            db.TradeRequest.Delete(new TradeRequestKey(id));
        }
        // 提出済みのままの取引を取り消す(手順 4)。
        var submitted=this.queries.SubmittedIds(db,input.TradeId,input.StatusSubmitted);
        foreach(var id in submitted){
            this.SetStatus(db,id,now,input.StatusCanceled);
            this.AddHistory(db,id,now,input.StatusCanceled);
        }
        return new TradeCleanupOutput{CanceledRequests=requests.Length,CanceledSubmitted=submitted.Length};
    }
    private void SetStatus(TpcEDatabase db,long id,long now,Code8 status){
        var trade=db.Trade.Get(new TradeKey(id));
        db.Trade.Update(trade with{Dts=now,StatusId=status});
    }
    private void AddHistory(TpcEDatabase db,long id,long now,Code8 status){
        var key=new TradeHistoryKey(id,status);
        if(db.TradeHistory.Contains(key)){
            return;
        }
        db.TradeHistory.Insert(new TradeHistoryRow(id,now,status));
    }
}
