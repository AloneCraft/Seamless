using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Benchmark.TpcE.Core;
using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance;
internal sealed class SutOptions{
    /// <summary>0 は空いているポートを選ぶ。</summary>
    public int Port{get;init;}
    /// <summary>1 回の fsync にまとめる取引の最大数(待たずに取れた分だけをまとめる)。</summary>
    public int MaxBatch{get;init;}=64;
    /// <summary>チェックポイントの間隔。仕様 6.6.5.3(データがコミット済みの状態より 15 分以上古くならない)から、その半分にする。</summary>
    public TimeSpan CheckpointInterval{get;init;}=TimeSpan.FromMinutes(7.5);
}
/// <summary>
/// 被測定系(SUT)。運転側からの要求を受け、1 本の作業スレッドで直列に実行する(直列なので隔離の条件を満たす。これは意図した設計判断)。
/// 要求は 2 種類: 取引は「0x01 + 取引 ID(4 バイト) + EGen の TxnInput の構造体のバイト列」、管理用は JSON(`{"admin":...}`)。
/// 取引の手順は EGenTxnHarness(C++)が持ち、フレームごとに <see cref="IHarnessFrameEngine"/> を呼ぶ(<see cref="HarnessHost"/>)。
/// 実行 → 書き込みを再実行ログへ追記 → 取れた分をまとめて fsync → 応答、の順で、応答を返した取引は電源断でも失わない。
/// </summary>
internal sealed class SutServer:IDisposable{
    private sealed class Pending(Stream stream,byte[] body){
        public Stream Stream{get;}=stream;
        public byte[] Body{get;}=body;
        public byte[]? Response{get;set;}
        public long ArrivedAt{get;}=Stopwatch.GetTimestamp();
    }
    /// <summary>取引の要求の先頭バイト。</summary>
    public const byte TransactionMarker=1;
    private static readonly HarnessTransaction[] Transactions=Enum.GetValues<HarnessTransaction>();
    private readonly ITpcEEngine engine;
    private readonly IHarnessFrameEngine frameEngine;
    private readonly HarnessHost harness;
    private readonly (int Input,int Output)[] sizes;
    private readonly List<byte[]> marketRequests=[];
    private readonly MarketCollector market;
    private readonly Dictionary<int,long> loadUnitTradeResults=[];
    private readonly RedoLogWriter redo;
    private readonly RedoObserver observer;
    private readonly RedoCheckpoint checkpoint;
    private readonly Action<long> saveCheckpoint;
    private readonly SutOptions options;
    private readonly TcpListener listener;
    private readonly BlockingCollection<Pending> queue=new();
    private readonly Thread acceptThread;
    private readonly Thread workerThread;
    private readonly List<Thread> readers=[];
    private readonly object readersLock=new();
    private readonly Stopwatch sinceCheckpoint=Stopwatch.StartNew();
    private readonly long[] executed=new long[Enum.GetValues<HarnessTransaction>().Length];
    private readonly long[] failed=new long[Enum.GetValues<HarnessTransaction>().Length];
    private long sequence;
    private long lastWriteSequence;
    // 診断用: 取引の実行・fsync・要求が待ち行列で待った時間(Stopwatch の刻み)と件数。作業スレッドだけが書き、別スレッドが読む(目安なので同期しない)。
    private long executeTicks;
    private long syncTicks;
    private long queueWaitTicks;
    private long batchCount;
    private long batchedTransactions;
    private volatile bool stopping;
    private sealed class MarketCollector(List<byte[]> requests):IMarketSink{
        public void SendToMarket(ReadOnlySpan<byte> tradeRequest)=>requests.Add(tradeRequest.ToArray());
    }
    public SutServer(ITpcEEngine engine,HarnessHost harness,RedoLogWriter redo,RedoCheckpoint checkpoint,long startSequence,Action<long> saveCheckpoint,SutOptions options){
        this.engine=engine;
        this.frameEngine=(IHarnessFrameEngine)engine;
        this.harness=harness;
        this.market=new MarketCollector(this.marketRequests);
        this.sizes=new (int Input,int Output)[Transactions.Length];
        foreach(var transaction in Transactions){
            this.sizes[(int)transaction]=(HarnessHost.SizeOf(transaction+"TxnInput"),HarnessHost.SizeOf(transaction+"TxnOutput"));
        }
        this.redo=redo;
        this.checkpoint=checkpoint;
        this.sequence=startSequence;
        this.saveCheckpoint=saveCheckpoint;
        this.options=options;
        this.observer=new RedoObserver(engine.Database);
        engine.Database.Log.Observer=this.observer;
        // 取引 ID の採番を、復旧(再実行ログの適用)が済んだ今の最大の取引 ID から始める。
        this.frameEngine.NextTradeId=0;
        _=this.frameEngine.NextTradeId;
        this.listener=new TcpListener(IPAddress.Loopback,options.Port);
        this.acceptThread=new Thread(this.AcceptLoop){IsBackground=true,Name="sut-accept"};
        this.workerThread=new Thread(this.WorkLoop){IsBackground=true,Name="sut-worker"};
    }
    public int Port=>((IPEndPoint)this.listener.LocalEndpoint).Port;
    public long Sequence=>Interlocked.Read(ref this.sequence);
    /// <summary>診断用の累計(取引ごとの平均の実行時間、バッチごとの平均の fsync 時間、取引が待ち行列で待った平均時間。ミリ秒)。</summary>
    public string Diagnostics(){
        var perMillisecond=Stopwatch.Frequency/1000.0;
        var transactions=Math.Max(1,this.batchedTransactions);
        var batches=Math.Max(1,this.batchCount);
        return $"exec {this.executeTicks/perMillisecond/transactions:F2} ms/txn, fsync {this.syncTicks/perMillisecond/batches:F2} ms/batch, queue wait {this.queueWaitTicks/perMillisecond/transactions:F2} ms/txn, batch {(double)this.batchedTransactions/batches:F2} txns";
    }
    public long Executed(HarnessTransaction transaction)=>Interlocked.Read(ref this.executed[(int)transaction]);
    public long Failed(HarnessTransaction transaction)=>Interlocked.Read(ref this.failed[(int)transaction]);
    public void Start(){
        this.listener.Start();
        this.workerThread.Start();
        this.acceptThread.Start();
    }
    private void AcceptLoop(){
        try{
            while(!this.stopping){
                var client=this.listener.AcceptTcpClient();
                client.NoDelay=true;
                var reader=new Thread(()=>this.ReadLoop(client)){IsBackground=true,Name="sut-reader"};
                lock(this.readersLock){
                    this.readers.Add(reader);
                }
                reader.Start();
            }
        }catch(SocketException){
        }catch(ObjectDisposedException){
        }
    }
    private void ReadLoop(TcpClient client){
        try{
            using(client){
                var stream=client.GetStream();
                while(!this.stopping){
                    var body=Framing.Read(stream);
                    if(body is null){
                        return;
                    }
                    this.queue.Add(new Pending(stream,body));
                }
            }
        }catch(IOException){
        }catch(ObjectDisposedException){
        }catch(InvalidOperationException){
        }
    }
    private void WorkLoop(){
        var batch=new List<Pending>();
        while(!this.stopping){
            if(!this.queue.TryTake(out var first,100)){
                this.CheckpointIfDue();
                continue;
            }
            batch.Clear();
            batch.Add(first);
            while(batch.Count<this.options.MaxBatch&&this.queue.TryTake(out var next)){
                batch.Add(next);
            }
            var executeStart=Stopwatch.GetTimestamp();
            foreach(var pending in batch){
                this.queueWaitTicks+=Stopwatch.GetTimestamp()-pending.ArrivedAt;
                pending.Response=this.Execute(pending.Body);
            }
            var syncStart=Stopwatch.GetTimestamp();
            this.executeTicks+=syncStart-executeStart;
            // 応答は fsync の後。ここを越えた取引は電源断でも残る。
            this.redo.Sync();
            this.syncTicks+=Stopwatch.GetTimestamp()-syncStart;
            this.batchCount++;
            this.batchedTransactions+=batch.Count;
            foreach(var pending in batch){
                try{
                    Framing.Write(pending.Stream,pending.Response!);
                }catch(IOException){
                }catch(ObjectDisposedException){
                }
            }
            this.CheckpointIfDue();
        }
    }
    private void CheckpointIfDue(){
        if(this.sinceCheckpoint.Elapsed<this.options.CheckpointInterval){
            return;
        }
        this.Checkpoint();
    }
    /// <summary>保存済みの状態にログの位置を記録して Store.Save する。作業スレッドからだけ呼ぶ(取引の合間)。</summary>
    public void Checkpoint(){
        this.redo.Sync();
        this.checkpoint.LastSequence=this.sequence;
        this.saveCheckpoint(this.sequence);
        this.sinceCheckpoint.Restart();
    }
    private byte[] Execute(byte[] body){
        HarnessTransaction? known=null;
        try{
            if(body.Length>0&&body[0]!=TransactionMarker){
                using var document=JsonDocument.Parse(body);
                if(document.RootElement.TryGetProperty("admin",out var admin)){
                    return this.Admin(admin.GetString() ?? "",document.RootElement);
                }
                throw new InvalidDataException("The request is neither a transaction nor an admin request.");
            }
            if(body.Length<5){
                throw new InvalidDataException("The transaction request is too short.");
            }
            var id=BitConverter.ToInt32(body,1);
            if(id<0||id>=this.sizes.Length){
                throw new InvalidDataException($"Unknown transaction id {id}.");
            }
            var transaction=(HarnessTransaction)id;
            known=transaction;
            var size=this.sizes[id];
            if(body.Length-5!=size.Input){
                throw new InvalidDataException($"{transaction} input is {body.Length-5} bytes, expected {size.Input}.");
            }
            var input=new byte[size.Input];
            Buffer.BlockCopy(body,5,input,0,size.Input);
            var output=new byte[size.Output];
            this.marketRequests.Clear();
            var tradeIdBefore=this.frameEngine.NextTradeId;
            var status=this.harness.Run(transaction,this.frameEngine,this.market,input,output);
            var tradeIdAfter=this.frameEngine.NextTradeId;
            var sequence=Interlocked.Increment(ref this.sequence);
            var record=this.observer.Seal(sequence);
            if(record is not null){
                this.redo.Append(record);
                this.lastWriteSequence=sequence;
            }
            Interlocked.Increment(ref this.executed[(int)transaction]);
            if(transaction==HarnessTransaction.TradeResult&&status>=0){
                var unit=new TradeResultTxnOutput(output).LoadUnit;
                this.loadUnitTradeResults[unit]=this.loadUnitTradeResults.GetValueOrDefault(unit)+1;
            }
            long allocated=0;
            if(transaction==HarnessTransaction.TradeOrder&&tradeIdAfter>tradeIdBefore){
                allocated=tradeIdBefore;
            }
            return this.Respond(status,allocated,sequence);
        }catch(Exception error){
            // エンジンは取引を巻き戻してある。通知で積んだ分は捨てる。
            this.observer.Discard();
            if(known is not null){
                Interlocked.Increment(ref this.failed[(int)known.Value]);
            }
            return Respond(error.GetType().Name+": "+error.Message,this.Sequence,this.lastWriteSequence);
        }
    }
    /// <summary>管理用の要求。作業スレッドで処理するので、取引は止まっていて DB は静止している。</summary>
    private byte[] Admin(string command,JsonElement request){
        var buffer=new ArrayBufferWriter<byte>(256);
        using(var writer=new Utf8JsonWriter(buffer)){
            writer.WriteStartObject();
            writer.WriteBoolean("ok",true);
            writer.WriteString("admin",command);
            writer.WriteNumber("seq",this.Sequence);
            switch(command){
                case "consistency":{
                    var results=ConsistencyChecker.Check(this.engine.Database);
                    writer.WriteStartArray("results");
                    foreach(var result in results){
                        writer.WriteStartObject();
                        writer.WriteString("clause",result.Clause);
                        writer.WriteString("name",result.Name);
                        writer.WriteNumber("checked",result.Checked);
                        writer.WriteNumber("violations",result.Violations);
                        writer.WriteBoolean("passed",result.Passed);
                        writer.WriteString("detail",result.Detail);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    break;
                }
                case "checkpoint":
                    this.Checkpoint();
                    break;
                case "load_units":{
                    // 負荷単位(顧客 1,000 人ごと)ごとの Trade-Result の累計。添字 0 が負荷単位 1。EGenValidate 用(仕様 6.7.4)。
                    var maximum=0;
                    foreach(var unit in this.loadUnitTradeResults.Keys){
                        maximum=Math.Max(maximum,unit);
                    }
                    writer.WriteStartArray("load_units");
                    for(var unit=1;unit<=maximum;unit++){
                        writer.WriteNumberValue(this.loadUnitTradeResults.GetValueOrDefault(unit));
                    }
                    writer.WriteEndArray();
                    break;
                }
                case "sample":{
                    // 試験用に、DB にある有効な組み合わせ(口座・その持ち主・銘柄・会社)を 1 つ返す。
                    var database=this.engine.Database;
                    var account=database.CustomerAccount.EnumerateRows().OrderBy(static row=>row.Id).First();
                    var customer=database.Customer.Get(new CustomerKey(account.CustomerId));
                    var security=database.Security.EnumerateRows().OrderBy(static row=>row.Symbol).First();
                    var company=database.Company.Get(new CompanyKey(security.CompanyId));
                    writer.WriteNumber("acct_id",account.Id);
                    writer.WriteString("exec_f_name",customer.FirstName);
                    writer.WriteString("exec_l_name",customer.LastName);
                    writer.WriteString("exec_tax_id",customer.TaxId.ToString());
                    writer.WriteString("symbol",security.Symbol.ToString());
                    writer.WriteString("issue",security.Issue.ToString());
                    writer.WriteString("co_name",company.Name);
                    break;
                }
                case "trade":{
                    // 原子性の試験用: 取引と取引履歴の行があるか(小さい DB で使う。履歴は全件走査)。
                    var tradeId=request.GetProperty("trade_id").GetInt64();
                    var database=this.engine.Database;
                    writer.WriteBoolean("trade_exists",database.Trade.Contains(new TradeKey(tradeId)));
                    long history=0;
                    foreach(var row in database.TradeHistory.EnumerateRows()){
                        if(row.TradeId==tradeId){
                            history++;
                        }
                    }
                    writer.WriteNumber("history_rows",history);
                    writer.WriteNumber("trade_count",database.Trade.Count);
                    break;
                }
                default:
                    writer.WriteBoolean("unknown",true);
                    break;
            }
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
    private byte[] Respond(int status,long allocatedTradeId,long sequence){
        var buffer=new ArrayBufferWriter<byte>(256);
        using(var writer=new Utf8JsonWriter(buffer)){
            writer.WriteStartObject();
            writer.WriteBoolean("ok",true);
            writer.WriteNumber("seq",sequence);
            // この応答を受け取った時点で、ここまでの書き込みは fsync 済み(応答は fsync の後に返す)。耐久性の試験で、応答済みの取引が残っているかの基準にする。
            writer.WriteNumber("wseq",this.lastWriteSequence);
            writer.WriteNumber("status",status);
            if(allocatedTradeId>0){
                // 巻き戻した注文も取引 ID は使い切る(戻さない)。原子性の試験が、その ID に行が無いことを確かめる。
                writer.WriteNumber("trade_id",allocatedTradeId);
                writer.WriteBoolean("rolled_back",this.marketRequests.Count==0);
            }
            if(this.marketRequests.Count>0){
                // 市場(MEE)へ送る注文。Trade-Order の成行は処理(action 0)、指値は発動条件の登録(action 1)。
                // Market-Feed が発動させた指値注文は処理(0)。これを運転側が MEE へ送り返さないと指値注文が約定しない。
                writer.WriteStartArray("trade_requests");
                foreach(var bytes in this.marketRequests){
                    var request=new TradeRequest(bytes);
                    writer.WriteStartObject();
                    writer.WriteNumber("trade_id",request.TradeId);
                    writer.WriteString("symbol",request.Symbol);
                    writer.WriteNumber("trade_qty",request.TradeQty);
                    writer.WriteString("trade_type_id",request.TradeTypeId);
                    writer.WriteNumber("price_quote",request.PriceQuote);
                    writer.WriteNumber("action",request.EAction);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
    private static byte[] Respond(string error,long sequence,long lastWriteSequence){
        var buffer=new ArrayBufferWriter<byte>(256);
        using(var writer=new Utf8JsonWriter(buffer)){
            writer.WriteStartObject();
            writer.WriteBoolean("ok",false);
            writer.WriteNumber("seq",sequence);
            writer.WriteNumber("wseq",lastWriteSequence);
            writer.WriteString("error",error);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
    public void Dispose(){
        this.stopping=true;
        try{
            this.listener.Stop();
        }catch(SocketException){
        }
        this.queue.CompleteAdding();
        if(this.workerThread.IsAlive){
            this.workerThread.Join(TimeSpan.FromSeconds(10));
        }
        this.engine.Database.Log.Observer=null;
    }
}
