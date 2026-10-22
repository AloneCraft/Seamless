using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Benchmark.TpcE.Core;
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
/// 被測定系(SUT)。運転側からの JSON 要求を受け、1 本の作業スレッドで直列に実行する(直列なので隔離の条件を満たす)。
/// 実行 → 書き込みを再実行ログへ追記 → 取れた分をまとめて fsync → 応答、の順で、応答を返した取引は電源断でも失わない。
/// </summary>
internal sealed class SutServer:IDisposable{
    private sealed class Pending(Stream stream,byte[] body){
        public Stream Stream{get;}=stream;
        public byte[] Body{get;}=body;
        public byte[]? Response{get;set;}
    }
    private readonly ITpcEEngine engine;
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
    private readonly long[] executed=new long[Enum.GetValues<TxnKind>().Length];
    private readonly long[] failed=new long[Enum.GetValues<TxnKind>().Length];
    private long sequence;
    private long nextTradeId;
    private long lastNow;
    private volatile bool stopping;
    public SutServer(ITpcEEngine engine,RedoLogWriter redo,RedoCheckpoint checkpoint,long startSequence,Action<long> saveCheckpoint,SutOptions options){
        this.engine=engine;
        this.redo=redo;
        this.checkpoint=checkpoint;
        this.sequence=startSequence;
        this.saveCheckpoint=saveCheckpoint;
        this.options=options;
        this.observer=new RedoObserver(engine.Database);
        engine.Database.Log.Observer=this.observer;
        this.nextTradeId=MaximumTradeId(engine.Database)+1;
        this.listener=new TcpListener(IPAddress.Loopback,options.Port);
        this.acceptThread=new Thread(this.AcceptLoop){IsBackground=true,Name="sut-accept"};
        this.workerThread=new Thread(this.WorkLoop){IsBackground=true,Name="sut-worker"};
    }
    public int Port=>((IPEndPoint)this.listener.LocalEndpoint).Port;
    public long Sequence=>Interlocked.Read(ref this.sequence);
    public long Executed(TxnKind kind)=>Interlocked.Read(ref this.executed[(int)kind]);
    public long Failed(TxnKind kind)=>Interlocked.Read(ref this.failed[(int)kind]);
    public void Start(){
        this.listener.Start();
        this.workerThread.Start();
        this.acceptThread.Start();
    }
    private static long MaximumTradeId(TpcEDatabaseBase database){
        long maximum=0;
        foreach(var table in database.Tables){
            if(table.Name!="trade"){
                continue;
            }
            foreach(var row in table.EnumerateRowsBoxed()){
                var id=((TradeRow)row).Id;
                if(id>maximum){
                    maximum=id;
                }
            }
        }
        return maximum;
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
            foreach(var pending in batch){
                pending.Response=this.Execute(pending.Body);
            }
            // 応答は fsync の後。ここを越えた取引は電源断でも残る。
            this.redo.Sync();
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
        var kind=TxnKind.TradeStatus;
        try{
            using var document=JsonDocument.Parse(body);
            var now=this.NextNow();
            var request=EGenRequestMapper.Map(document.RootElement,now);
            kind=request.Kind;
            if(request is TradeOrderInput order){
                // 取引 ID は SUT が採番する。巻き戻しでも戻さない。
                request=order with{TradeId=this.nextTradeId++};
            }
            var result=this.engine.Execute(request);
            var sequence=Interlocked.Increment(ref this.sequence);
            var record=this.observer.Seal(sequence);
            if(record is not null){
                this.redo.Append(record);
            }
            Interlocked.Increment(ref this.executed[(int)kind]);
            return Respond(true,result,null,sequence);
        }catch(Exception error){
            // エンジンは例外で書き込みを巻き戻してある。通知で積んだ分は捨てる。
            this.observer.Discard();
            Interlocked.Increment(ref this.failed[(int)kind]);
            return Respond(false,null,error.GetType().Name+": "+error.Message,this.Sequence);
        }
    }
    /// <summary>取引の時刻。実時計を使うが、単調に増える(同じ時刻の取引を作らない)。</summary>
    private long NextNow(){
        var now=TpcETime.FromDateTime(DateTime.Now);
        if(now<=this.lastNow){
            now=this.lastNow+1;
        }
        this.lastNow=now;
        return now;
    }
    private static byte[] Respond(bool ok,TxnResult? result,string? error,long sequence){
        var buffer=new ArrayBufferWriter<byte>(256);
        using(var writer=new Utf8JsonWriter(buffer)){
            writer.WriteStartObject();
            writer.WriteBoolean("ok",ok);
            writer.WriteNumber("seq",sequence);
            if(result is not null){
                writer.WriteNumber("status",result.Status);
            }
            if(error is not null){
                writer.WriteString("error",error);
            }
            if(result is TradeOrderOutput order&&!order.RolledBack&&order.Status>=0&&order.TradeId>0){
                // 市場(MEE)へ送る注文。成行は処理、指値は発動条件の登録。
                writer.WriteStartObject("trade_request");
                writer.WriteNumber("trade_id",order.TradeId);
                writer.WriteString("symbol",order.Symbol.ToString());
                writer.WriteNumber("trade_qty",order.Quantity);
                writer.WriteString("trade_type_id",order.TradeTypeId.ToString());
                writer.WriteNumber("price_quote",order.RequestedPrice);
                if(order.IsMarket){
                    writer.WriteNumber("action",0);
                }else{
                    writer.WriteNumber("action",1);
                }
                writer.WriteEndObject();
            }
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
