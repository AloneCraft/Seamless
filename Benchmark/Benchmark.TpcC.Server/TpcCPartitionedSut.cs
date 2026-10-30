using System.Text.Json;
using Benchmark.Tpc.Core;
using Benchmark.Tpc.Server;
using Benchmark.TpcC.Core;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Server;
/// <summary>独立Storeを所有するTPC-C SUT。C10の単一Store runnerとは別の入口。</summary>
public sealed class TpcCPartitionedSut:IAsyncDisposable{
    private readonly Store[] stores;
    private readonly TransactionJournal journal;
    private readonly TpcWalHost host;
    private int stopping;
    public ITpcCEngine Engine{get;}
    public TpcCPartitionedSut(ITpcCVariant variant,IReadOnlyList<Store> stores,TpcCScale scale,TpcCMode mode,string walDirectory,Action<ITpcCEngine>? initialize=null,ulong seed=1,bool auditMode=false,Action<WalEntry>? replayApplied=null){
        if(stores.Count!=scale.PartitionCount){throw new ArgumentException("One Store per partition is required.",nameof(stores));}
        this.stores=stores.ToArray();
        var identity=$"tpcc-v2|{scale.Warehouses}|{scale.PartitionCount}|{scale.Items}|{scale.CustomersPerDistrict}|{mode}|{seed}|audit:{auditMode}";
        try{
            this.journal=new(walDirectory,new WalOptions{CatalogIdentity=identity});this.Engine=variant.Open(stores,scale,mode);
            if(stores.All(static store=>store.CurrentCheckpoint is null)&&initialize is not null){initialize(this.Engine);}
            this.host=new(this.Engine.Database.PartitionBases.Cast<TpcDatabaseBase>().ToArray(),this.journal.Writer,this.journal,this.Engine.Database.PartitionBases.Select(static part=>(ITableView)part.Item).ToArray(),replayApplied){AuditMode=auditMode};
        }catch{
            try{this.Engine?.Dispose();}finally{foreach(var store in this.stores){store.Dispose();}this.journal?.DisposeAsync().AsTask().GetAwaiter().GetResult();}
            throw;
        }
    }
    public Task<TpcDurableResult<TpcCResult>> ExecuteAsync(TpcCRequest request,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.stopping)!=0,this);
        request=new TpcCReplayEnvelope(1,this.Engine.Mode,request.Kind,JsonSerializer.SerializeToUtf8Bytes(request,request.GetType()),[],[]).DecodeRequest();
        var parts=request.TouchedWarehouses().Select(this.Engine.Database.Scale.PartitionOf).Distinct().Order().ToArray();
        return this.host.ExecuteAsync(parts,()=>this.Engine.Execute(request),static result=>result.RolledBack,
            (result,hash)=>JsonSerializer.SerializeToUtf8Bytes(new TpcCReplayEnvelope(1,this.Engine.Mode,request.Kind,JsonSerializer.SerializeToUtf8Bytes(request,request.GetType()),CanonicalWriter.ToBytes(result),hash)),cancellationToken);
    }
    public Task CheckpointAsync(int partition,CancellationToken cancellationToken=default)=>this.host.CheckpointAsync(partition,cancellationToken);
    public TransactionOutcome GetTransactionOutcome(Guid id)=>this.journal.GetOutcome(id);
    public async ValueTask DisposeAsync(){
        if(Interlocked.Exchange(ref this.stopping,1)!=0){return;}
        var errors=new List<Exception>();
        try{await this.host.DisposeAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{this.Engine.Dispose();}catch(Exception error){errors.Add(error);}
        foreach(var store in this.stores){try{store.Dispose();}catch(Exception error){errors.Add(error);}}
        try{await this.journal.DisposeAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(errors.Count>0){throw new AggregateException(errors);}
    }
}
public sealed record TpcCReplayEnvelope(int Version,TpcCMode Mode,TpcCTxnKind Kind,byte[] Input,byte[] ResultCanonical,byte[] WriteTraceHash){
    public TpcCRequest DecodeRequest(){
        if(this.Version!=1){throw new InvalidDataException("Unsupported TPC-C request codec.");}
        TpcCRequest? request=this.Kind switch{
            TpcCTxnKind.NewOrder=>JsonSerializer.Deserialize<NewOrderRequest>(this.Input)!,
            TpcCTxnKind.Payment=>JsonSerializer.Deserialize<PaymentRequest>(this.Input)!,
            TpcCTxnKind.OrderStatus=>JsonSerializer.Deserialize<OrderStatusRequest>(this.Input)!,
            TpcCTxnKind.Delivery=>JsonSerializer.Deserialize<DeliveryRequest>(this.Input)!,
            TpcCTxnKind.StockLevel=>JsonSerializer.Deserialize<StockLevelRequest>(this.Input)!,
            _=>throw new InvalidDataException("Unknown TPC-C request kind."),
        };
        return request??throw new InvalidDataException("Missing TPC-C request payload.");
    }
}
