using System.Text.Json;
using System.Security.Cryptography;
using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
using Seamless.Transactions;
namespace Benchmark.TpcE.Compliance;
public sealed record TpcEHarnessResult(int Status,long AllocatedTradeId,bool RolledBack,byte[] Output,byte[][] MarketRequests);
/// <summary>旧SutServer/v1を保全する追加経路。engine/Storeの寿命は呼出側が所有する。</summary>
public sealed class TpcETransactionalSut:IAsyncDisposable{
    private static readonly object HarnessGate=new();
    private readonly ITpcEEngine engine;
    private readonly TransactionJournal journal;
    private readonly TpcWalHost host;
    private readonly HarnessHost? harness;
    private readonly string harnessIdentity="managed";
    private int stopping;
    public TpcETransactionalSut(ITpcEEngine engine,TpcEScale scale,string walDirectory,string? harnessDll=null,bool auditMode=false){
        this.engine=engine;
        this.journal=new(walDirectory,new WalOptions{CatalogIdentity=$"tpce-v2|{scale.Customers}|{scale.InitialTradeDays}|{scale.MarketDays}|{scale.NewsItemBytes}|audit:{auditMode}"});
        try{
            this.host=new([engine.Database],this.journal.Writer,this.journal){AuditMode=auditMode};
            if(harnessDll is not null){using var file=File.OpenRead(harnessDll);this.harnessIdentity=Convert.ToHexString(SHA256.HashData(file));this.harness=new(harnessDll);}
            if(engine is IHarnessFrameEngine frames){frames.NextTradeId=0;_=frames.NextTradeId;}
        }catch{
            try{this.host?.DisposeAsync().AsTask().GetAwaiter().GetResult();}finally{this.harness?.Dispose();this.journal.DisposeAsync().AsTask().GetAwaiter().GetResult();}
            throw;
        }
    }
    public Task<TpcDurableResult<TxnResult>> ExecuteAsync(TxnRequest request,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.stopping)!=0,this);
        var input=JsonSerializer.SerializeToUtf8Bytes(request,request.GetType());
        var owned=(TxnRequest)(JsonSerializer.Deserialize(input,request.GetType())??throw new InvalidDataException("Missing TPC-E input."));
        return this.host.ExecuteAsync([0],()=>this.engine.Execute(owned),static result=>result is TradeOrderOutput{RolledBack:true},
            (result,hash)=>JsonSerializer.SerializeToUtf8Bytes(new{Version=1,Kind=owned.Kind,Input=input,Result=CanonicalWriter.ToBytes(result),WriteTraceHash=hash}),cancellationToken);
    }
    public Task<TpcDurableResult<TpcEHarnessResult>> ExecuteHarnessAsync(HarnessTransaction transaction,byte[] input,CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.stopping)!=0,this);
        if(this.harness is null){throw new InvalidOperationException("Configure EGenHarness.dll for native transactions.");}
        var owned=input.ToArray();
        if(owned.Length!=HarnessHost.SizeOf(transaction+"TxnInput")){throw new InvalidDataException("Native transaction input size mismatch.");}
        return this.host.ExecuteAsync([0],()=>{
            lock(HarnessGate){
                var frames=(IHarnessFrameEngine)this.engine;var before=frames.NextTradeId;
                var collector=new Collector();var output=new byte[HarnessHost.SizeOf(transaction+"TxnOutput")];
                var status=this.harness.Run(transaction,frames,collector,owned,output);var after=frames.NextTradeId;
                long allocated=0;if(transaction==HarnessTransaction.TradeOrder&&after>before){allocated=before;}
                return new TpcEHarnessResult(status,allocated,allocated>0&&collector.Requests.Count==0,output,collector.Requests.ToArray());
            }
        },static result=>result.RolledBack,(result,hash)=>JsonSerializer.SerializeToUtf8Bytes(new{Version=1,Codec="egen-harness-v1",NativeModule=this.harnessIdentity,Kind=transaction,Input=owned,Result=result,WriteTraceHash=hash}),cancellationToken);
    }
    public Task CheckpointAsync(CancellationToken cancellationToken=default)=>this.host.CheckpointAsync(0,cancellationToken);
    public TransactionOutcome GetTransactionOutcome(Guid id)=>this.journal.GetOutcome(id);
    public async ValueTask DisposeAsync(){
        if(Interlocked.Exchange(ref this.stopping,1)!=0){return;}
        try{await this.host.DisposeAsync().ConfigureAwait(false);}
        finally{this.harness?.Dispose();await this.journal.DisposeAsync().ConfigureAwait(false);}
    }
    private sealed class Collector:IMarketSink{
        public List<byte[]> Requests{get;}=[];
        public void SendToMarket(ReadOnlySpan<byte> request)=>this.Requests.Add(request.ToArray());
    }
}
