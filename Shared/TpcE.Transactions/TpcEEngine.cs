namespace Shared.TpcE.Transactions;
/// <summary>
/// 1 つの組み合わせの取引の実行器。LINQ は書かない(LINQ は *Queries にだけ書き、*Frames から呼ぶ)。
/// FLinq は「LINQ を含むメソッドを呼んでいる箇所」を置き換えるので、Frames→Queries の呼び出しが同じアセンブリにあることが要る。
/// </summary>
internal sealed partial class TpcEEngine:ITpcEEngine{
    private readonly TpcEDatabase db;
    private readonly TradeCleanupFrames tradeCleanup=new();
    private readonly DataMaintenanceFrames dataMaintenance=new();
    private readonly MarketFeedFrames marketFeed=new();
    private readonly TradeResultFrames tradeResult=new();
    private readonly BrokerVolumeFrames brokerVolume=new();
    private readonly CustomerPositionFrames customerPosition=new();
    private readonly MarketWatchFrames marketWatch=new();
    private readonly SecurityDetailFrames securityDetail=new();
    private readonly TradeLookupFrames tradeLookup=new();
    private readonly TradeOrderFrames tradeOrder=new();
    private readonly TradeStatusFrames tradeStatus=new();
    private readonly TradeUpdateFrames tradeUpdate=new();
    public TpcEEngine(TpcEDatabase db){
        this.db=db;
        this.Loader=new DatabaseLoadSink(db);
    }
    public TpcEDatabaseBase Database=>this.db;
    public ILoadSink Loader{get;}
    public TxnResult Execute(TxnRequest request){
        this.db.Log.Begin();
        try{
            TxnResult result=request switch{
            TradeCleanupInput input=>this.tradeCleanup.Run(this.db,input),
            DataMaintenanceInput input=>this.dataMaintenance.Run(this.db,input),
            MarketFeedInput input=>this.marketFeed.Run(this.db,input),
            TradeResultInput input=>this.tradeResult.Run(this.db,input),
            BrokerVolumeInput input=>this.brokerVolume.Run(this.db,input),
            CustomerPositionInput input=>this.customerPosition.Run(this.db,input),
            MarketWatchInput input=>this.marketWatch.Run(this.db,input),
            SecurityDetailInput input=>this.securityDetail.Run(this.db,input),
            TradeLookupInput input=>this.tradeLookup.Run(this.db,input),
            TradeOrderInput input=>this.tradeOrder.Run(this.db,input),
            TradeStatusInput input=>this.tradeStatus.Run(this.db,input),
            TradeUpdateInput input=>this.tradeUpdate.Run(this.db,input),
                _=>throw new NotSupportedException($"{request.GetType().Name} is not a TPC-E transaction input."),
            };
            // Trade-Order の巻き戻し(Frame 5)は Frames の中で Rollback 済み。それ以外は確定する。
            if(this.db.Log.IsRecording){
                this.db.Log.Commit();
            }
            return result;
        }catch{
            if(this.db.Log.IsRecording){
                this.db.Log.Rollback();
            }
            throw;
        }
    }
    public void Dispose()=>this.db.Dispose();
}
