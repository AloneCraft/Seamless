namespace Shared.TpcC.Transactions;
/// <summary>
/// 1 つの組み合わせの取引の実行器。LINQ は書かない(LINQ は *Queries にだけ書き、*Frames から呼ぶ)。
/// FLinq は「LINQ を含むメソッドを呼んでいる箇所」を置き換えるので、Frames→Queries の呼び出しが同じアセンブリにあることが要る。
/// 取引が触る倉庫の区画を昇順に押さえる想定で、取り消し記録(TxnLog)も昇順に始め、確定も巻き戻しも全区画でそろえる。
/// </summary>
internal sealed class TpcCEngine:ITpcCEngine{
    private readonly TpcCDatabase db;
    private readonly NewOrderFrames newOrder;
    private readonly PaymentFrames payment;
    private readonly OrderStatusFrames orderStatus;
    private readonly DeliveryFrames delivery;
    private readonly StockLevelFrames stockLevel;
    private readonly AdminQueries admin=new();
    public TpcCEngine(TpcCDatabase db,TpcCMode mode){
        this.db=db;
        this.Mode=mode;
        var customers=new CustomerQueries();
        var lookup=new CustomerLookup(customers);
        this.newOrder=new NewOrderFrames(mode,new NewOrderQueries());
        this.payment=new PaymentFrames(mode,lookup);
        this.orderStatus=new OrderStatusFrames(lookup,new OrderStatusQueries());
        this.delivery=new DeliveryFrames(mode,new DeliveryQueries());
        this.stockLevel=new StockLevelFrames(new StockLevelQueries());
        this.Loader=new TpcCDatabaseLoadSink<TpcCPartition>(db);
    }
    public TpcCMode Mode{get;}
    public ITpcCDatabase Database=>this.db;
    public ITpcCLoadSink Loader{get;}
    public long SumNextOrderIds(){
        long total=0;
        foreach(var partition in this.db.Partitions){
            total+=this.admin.SumNextOrderIds(partition);
        }
        return total;
    }
    public TpcCResult Execute(TpcCRequest request){
        var involved=this.Involved(request);
        foreach(var partition in involved){
            partition.Log.Begin();
        }
        try{
            TpcCResult result=request switch{
                NewOrderRequest input=>this.newOrder.Run(this.db,input),
                PaymentRequest input=>this.payment.Run(this.db,input),
                OrderStatusRequest input=>this.orderStatus.Run(this.db,input),
                DeliveryRequest input=>this.delivery.Run(this.db,input),
                StockLevelRequest input=>this.stockLevel.Run(this.db,input),
                _=>throw new NotSupportedException($"{request.GetType().Name} is not a TPC-C transaction input."),
            };
            if(result.RolledBack){
                this.RollbackAll(involved);
            }else{
                foreach(var partition in involved){
                    partition.Log.Commit();
                }
            }
            return result;
        }catch{
            this.RollbackAll(involved);
            throw;
        }
    }
    private void RollbackAll(List<TpcCPartition> involved){
        for(var index=involved.Count-1;index>=0;index--){
            if(involved[index].Log.IsRecording){
                involved[index].Log.Rollback();
            }
        }
    }
    /// <summary>取引が触る倉庫の区画(昇順・重複なし)。</summary>
    private List<TpcCPartition> Involved(TpcCRequest request){
        var result=new List<TpcCPartition>(2);
        var last=-1;
        foreach(var warehouse in request.TouchedWarehouses()){
            var partition=this.db.Scale.PartitionOf(warehouse);
            if(partition!=last){
                result.Add(this.db.Partition(partition));
                last=partition;
            }
        }
        return result;
    }
    public void Dispose()=>this.db.Dispose();
}
