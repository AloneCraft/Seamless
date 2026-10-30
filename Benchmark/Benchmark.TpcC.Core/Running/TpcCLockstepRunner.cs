using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>実行の結果の要約。</summary>
public sealed record TpcCRunOutcome(int ExitCode,string RunDirectory,TpcCFailure? Failure,TpcCDriverState? FinalState);
/// <summary>
/// 組み合わせ(と、区画 2 つ以上のときは区画 1 つのシャドー)を 1 プロセスで逐次流し、取引 1 件ごとに出力(L1)・書き込みの中身(L2)・読み戻し(L3)を比べる。
/// 保存→閉じる→開き直しの前後と最後に、全表と全索引のダイジェスト(L4)と、一貫性の条件・索引と表の一致を調べる。食い違いや例外はその場で止める。
/// </summary>
public sealed class TpcCLockstepRunner{
    private readonly TpcCRunOptions options;
    private readonly IReadOnlyList<TpcCVariantSession> sessions;
    private readonly TextWriter log;
    private readonly string runDirectory;
    private TpcCDriver? driver;
    private long lastAgreedTxn;
    private int saveCount;
    private bool failureWritten;
    private readonly List<Exception> cleanupErrors=[];
    private readonly Stopwatch wall=new();
    private volatile bool stopRequested;
    private TpcCDedicatedOwner? transactionOwner;
    private bool workerRetainsOwnership;
    public TpcCLockstepRunner(TpcCRunOptions options,IReadOnlyList<ITpcCVariant> variants,string runDirectory,TextWriter log){
        this.options=options;
        this.runDirectory=runDirectory;
        this.log=log;
        var sessions=new List<TpcCVariantSession>();
        foreach(var variant in variants){
            sessions.Add(new(variant,variant.Name,variant.ShortName,options.Scale,Path.Combine(runDirectory,"stores",variant.Name)));
        }
        if(options.Shadow&&options.Scale.PartitionCount>1&&variants.Count>0){
            // 区画 1 つのシャドー: 結果も状態も区画の数に依らないはずなので、区画をまたぐ取引や区画ごとの書き込み先の誤りがここで見つかる。
            var basis=variants.FirstOrDefault(static variant=>variant.ShortName=="TF")??variants[0];
            sessions.Add(new(basis,basis.Name+"-p1",basis.ShortName+"p1",options.ScaleWith(1),Path.Combine(runDirectory,"stores",basis.Name+"-p1")));
        }
        this.sessions=sessions;
    }
    public IReadOnlyList<TpcCVariantSession> Sessions=>this.sessions;
    public TpcCDriver? Driver=>this.driver;
    public bool Stopped{get;private set;}
    /// <summary>Ctrl+C から呼ぶ。取引の境目で止めて、保存と比較をしてから終わる。</summary>
    public void RequestStop()=>this.stopRequested=true;
    /// <summary>ストア 1 つ(全区画)のメモリ上限。倉庫あたり約 96MB に土台の 256MB。--memory-mb で上書きできる。</summary>
    public static StoreLimits LimitsFor(TpcCRunOptions options){
        var memory=256L*1024*1024+options.Warehouses*96L*1024*1024*options.Items/100_000+options.Warehouses*48L*1024*1024*options.CustomersPerDistrict/3_000;
        if(options.MemoryMegabytes>0){
            memory=options.MemoryMegabytes*1024*1024;
        }
        return new(){MemoryBytes=memory,DiskBytes=options.DiskGigabytes*1024L*1024*1024};
    }
    public TpcCRunOutcome Run(){
        this.wall.Start();
        TpcCRunOutcome outcome;
        try{
            this.Prepare();
            if(this.options.HopThreads){
                this.transactionOwner=new();
                this.Log($"取引所有thread: A={Environment.CurrentManagedThreadId}, B={this.transactionOwner.ThreadId}");
                this.transactionOwner.Invoke(()=>this.CleanupLog($"取引所有thread稼働: B={Environment.CurrentManagedThreadId}"));
            }
            this.Loop();
            this.Finish();
            this.WriteSummary(null);
            var exitCode=0;
            if(this.Stopped){
                exitCode=130;
            }
            outcome=new(exitCode,this.runDirectory,null,this.driver?.State);
        }catch(TpcCFailure failure){
            this.Report(failure);
            outcome=new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }catch(Exception error) when(IsResourceLimit(error)){
            var failure=new TpcCFailure(TpcCFailureKind.Resource,"資源の上限に達しました: "+error.Message,error){TxnIndex=this.driver?.State.TxnIndex??0};
            this.Report(failure);
            outcome=new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }catch(Exception error){
            var failure=new TpcCFailure(TpcCFailureKind.Exception,"想定外の例外: "+error.Message,error){TxnIndex=this.driver?.State.TxnIndex??0};
            this.Report(failure);
            outcome=new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }finally{
            var workerStopped=true;
            try{
                this.transactionOwner?.Dispose();
            }catch(Exception error){
                workerStopped=false;
                this.cleanupErrors.Add(new InvalidOperationException("取引所有threadの終了確認に失敗しました。",error));
            }
            if(workerStopped&&this.transactionOwner is not null){
                this.CleanupLog($"取引所有thread終了: B={this.transactionOwner.ThreadId}");
            }
            if(workerStopped&&!this.workerRetainsOwnership){
                this.CloseAll();
            }
        }
        if(this.cleanupErrors.Count>0){
            var errors=new List<Exception>(this.cleanupErrors);
            if(outcome.Failure is not null){
                errors.Insert(0,outcome.Failure);
            }
            var failure=new TpcCFailure(TpcCFailureKind.Exception,"実行資源を閉じる途中で失敗しました。",new AggregateException(errors)){
                TxnIndex=this.driver?.State.TxnIndex??0,
            };
            this.Report(failure);
            outcome=new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }
        if(!this.options.KeepStores&&!this.failureWritten){
            TryDelete(Path.Combine(this.runDirectory,"stores"));
        }
        return outcome;
    }
    // ---- 準備: ストアを開き、初期データを 1 回だけ生成して全組み合わせへ配る ----
    private void Prepare(){
        foreach(var session in this.sessions){
            if(System.IO.Directory.Exists(session.Directory)&&System.IO.Directory.EnumerateFileSystemEntries(session.Directory).Any()){
                throw new ArgumentException($"The store directory {session.Directory} is not empty.");
            }
            this.OpenSession(session);
        }
        this.driver=new TpcCDriver(this.options.Scale,this.options.Mode,this.options.Seed,this.options.Terminals,TpcCDriver.Initial(this.options.Seed));
        var loadWatch=Stopwatch.StartNew();
        foreach(var session in this.sessions){
            session.RequireEngine.Database.BeginBulkLoad();
        }
        var sinks=new List<ITpcCLoadSink>();
        foreach(var session in this.sessions){
            sinks.Add(session.RequireEngine.Loader);
        }
        new TpcCDataGenerator(this.options.Scale,TpcCLoadRules.For(this.options.Mode),this.options.Seed).Generate(new TpcCFanOutLoadSink(sinks));
        foreach(var session in this.sessions){
            var watch=Stopwatch.StartNew();
            session.RequireEngine.Database.EndBulkLoad();
            session.AddPhase("load-flush",watch.Elapsed.TotalSeconds);
        }
        this.Log($"初期データ: 倉庫 {this.options.Warehouses}、区画 {this.options.Scale.PartitionCount}、{loadWatch.Elapsed.TotalSeconds:F1} 秒");
        this.CompareDigests("初期データ",true);
        if(this.options.SaveEvery>0){
            this.SaveAll(false);
        }
    }
    private void OpenSession(TpcCVariantSession session){
        var watch=Stopwatch.StartNew();
        var store=Store.Open(session.Directory,LimitsFor(this.options));
        session.Store=store;
        // 運転状態は最初に登録する(保存済みなら登録時に読み戻され、ここで照合する)。
        store.Register(new TpcCDriverStateComponent(()=>this.driver!.State,state=>this.CheckRestoredState(session,state)));
        session.Engine=session.Variant.Open(store,session.Scale,this.options.Mode);
        session.AttachRecorders();
        session.AddPhase("open",watch.Elapsed.TotalSeconds);
    }
    private void CheckRestoredState(TpcCVariantSession session,TpcCDriverState restored){
        if(this.driver is null){
            return;
        }
        var expected=TpcCDriverStateComponent.Serialize(this.driver.State);
        var actual=TpcCDriverStateComponent.Serialize(restored);
        if(expected!=actual){
            throw new TpcCFailure(TpcCFailureKind.Reopen,$"{session.Name}: 開き直した運転状態が保存前と違います。"){
                TxnIndex=this.driver.State.TxnIndex,
                Engine=session.Name,
                Details=["保存前: "+expected,"開き直し後: "+actual],
            };
        }
    }
    // ---- 取引を流す ----
    private void Loop(){
        var driver=this.driver!;
        var lastSaved=driver.State.TxnIndex;
        var nextProgress=Stopwatch.GetTimestamp()+Stopwatch.Frequency*10;
        while(driver.State.TxnIndex<this.options.Transactions){
            if(this.stopRequested){
                this.Stopped=true;
                this.Log("中断の指示で止めます。");
                return;
            }
            if(this.options.StopAt>0&&driver.State.TxnIndex+1>=this.options.StopAt){
                this.Log($"--stop-at {this.options.StopAt} で止めました。");
                return;
            }
            var request=driver.Next();
            var txnIndex=driver.State.TxnIndex;
            var measured=txnIndex>this.options.Warmup;
            TpcCResult[] results;
            if(this.transactionOwner is not null){
                results=this.ExecuteOnTransactionOwner(request,txnIndex,measured);
            }else{
                results=this.ExecuteAll(request,txnIndex,measured);
            }
            driver.Completed(request,results[0]);
            this.lastAgreedTxn=txnIndex;
            if(this.options.TraceFrom>0&&txnIndex>=this.options.TraceFrom){
                this.Trace(txnIndex,request,results[0]);
            }
            if(this.options.SaveEvery>0&&txnIndex-lastSaved>=this.options.SaveEvery){
                lastSaved=txnIndex;
                var reopen=this.options.ReopenEvery>0&&(this.saveCount+1)%this.options.ReopenEvery==0;
                this.SaveAll(reopen);
            }
            if(Stopwatch.GetTimestamp()>=nextProgress){
                nextProgress=Stopwatch.GetTimestamp()+Stopwatch.Frequency*10;
                this.Log($"取引 {txnIndex:N0}/{this.options.Transactions:N0} 件、{this.wall.Elapsed.TotalSeconds:F0} 秒");
            }
        }
    }
    private TpcCResult[] ExecuteOnTransactionOwner(TpcCRequest request,long txnIndex,bool measured){
        // 全Releaseの成功まではAが所有する。途中失敗ならBへ渡さない。
        this.TransferHandles(acquire:false);
        TpcCResult[]? results=null;
        this.transactionOwner!.Invoke(()=>{
            ExceptionDispatchInfo? failure=null;
            try{
                // Acquire開始時点から、部分取得したhandleもBが所有する。
                this.TransferHandles(acquire:true);
                results=this.ExecuteAll(request,txnIndex,measured);
            }catch(Exception error){
                failure=ExceptionDispatchInfo.Capture(error);
            }
            var cleanupFailed=false;
            foreach(var session in this.sessions){
                try{
                    this.TransferHandles(session,acquire:false);
                }catch(Exception error){
                    cleanupFailed=true;
                    this.cleanupErrors.Add(new InvalidOperationException(session.Name+": Bのhandle解放に失敗しました。",error));
                }
            }
            if(cleanupFailed){
                // 未知の部分所有をAへ返さない。Bで全terminal cleanupを試してからackする。
                this.workerRetainsOwnership=true;
                try{
                    this.CloseAll();
                }catch(Exception error){
                    this.cleanupErrors.Add(new InvalidOperationException("Bのterminal cleanup中に失敗しました。",error));
                }
            }
            failure?.Throw();
            if(cleanupFailed){
                throw new InvalidOperationException("Bのhandle解放に失敗したため所有をAへ戻せません。");
            }
        });
        // 元取引もBの解放も成功した場合だけ、Aの所有を再取得する。
        this.TransferHandles(acquire:true);
        return results!;
    }
    private void TransferHandles(bool acquire){
        foreach(var session in this.sessions){
            this.TransferHandles(session,acquire);
        }
    }
    private void TransferHandles(TpcCVariantSession session,bool acquire){
        var watch=Stopwatch.StartNew();
        try{
            if(acquire){
                session.RequireEngine.Database.AcquireHandles();
            }else{
                session.RequireEngine.Database.ReleaseHandles();
            }
        }finally{
            var phase="hop-release";
            if(acquire){
                phase="hop-acquire";
            }
            session.AddPhase(phase,watch.Elapsed.TotalSeconds);
        }
    }
    private TpcCResult[] ExecuteAll(TpcCRequest request,long txnIndex,bool measured){
        var count=this.sessions.Count;
        var results=new TpcCResult[count];
        // 呼ぶ順を取引ごとに回す(キャッシュと GC の偏りをならす)。
        var offset=(int)(txnIndex%count);
        for(var step=0;step<count;step++){
            var index=(offset+step)%count;
            var session=this.sessions[index];
            var engine=session.RequireEngine;
            session.Records.Clear();
            var gcBefore=GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2);
            var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
            var started=Stopwatch.GetTimestamp();
            TpcCResult result;
            try{
                result=engine.Execute(request);
            }catch(Exception error){
                var kind=TpcCFailureKind.Exception;
                if(IsResourceLimit(error)){
                    kind=TpcCFailureKind.Resource;
                }
                throw new TpcCFailure(kind,$"{session.Name}: {request.Kind} で例外 {error.GetType().Name}: {error.Message}",error){
                    TxnIndex=txnIndex,
                    Transaction=request.Kind,
                    Engine=session.Name,
                    Request=request,
                };
            }
            var elapsed=Stopwatch.GetTimestamp()-started;
            if(measured){
                if(!session.Timings.TryGetValue(request.Kind,out var histogram)){
                    histogram=new();
                    session.Timings.Add(request.Kind,histogram);
                }
                histogram.Add(elapsed,GC.GetAllocatedBytesForCurrentThread()-allocatedBefore,GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2)-gcBefore);
            }
            results[index]=TpcCFaultInjection.AfterTransaction(this.options.InjectFault,session,txnIndex,result);
        }
        this.CompareOutputs(request,txnIndex,results);
        this.CompareWrites(request,txnIndex);
        this.ReadBack(request,txnIndex);
        return results;
    }
    // ---- L1: 出力 ----
    private void CompareOutputs(TpcCRequest request,long txnIndex,TpcCResult[] results){
        for(var index=0;index<results.Length;index++){
            if(results[index] is null){
                throw new TpcCFailure(TpcCFailureKind.Output,$"{this.sessions[index].Name}: 取引の出力がありません。"){
                    TxnIndex=txnIndex,Transaction=request.Kind,Engine=this.sessions[index].Name,Request=request,Outputs=this.ByName(results),
                };
            }
        }
        var expected=CanonicalWriter.ToBytes(results[0]);
        for(var index=1;index<results.Length;index++){
            if(!CanonicalWriter.ToBytes(results[index]).AsSpan().SequenceEqual(expected)){
                throw new TpcCFailure(TpcCFailureKind.Output,$"{request.Kind} の出力が食い違いました({this.Grouping(results)})。"){
                    TxnIndex=txnIndex,
                    Transaction=request.Kind,
                    Request=request,
                    Outputs=this.ByName(results),
                    Details=[this.Grouping(results)],
                };
            }
        }
    }
    /// <summary>結果のハッシュで組み合わせを組に分ける(原因の当たり: 保存先か FLinq か区画か)。</summary>
    private string Grouping(TpcCResult[] results){
        var groups=new Dictionary<UInt128,List<string>>();
        for(var index=0;index<results.Length;index++){
            var hash=CanonicalWriter.Hash(results[index]);
            if(!groups.TryGetValue(hash,out var names)){
                names=[];
                groups.Add(hash,names);
            }
            names.Add(this.sessions[index].ShortName);
        }
        var parts=new List<string>();
        foreach(var names in groups.Values){
            parts.Add("{"+string.Join(",",names)+"}");
        }
        return string.Join(" 対 ",parts);
    }
    private Dictionary<string,object?> ByName<T>(T[] values){
        var result=new Dictionary<string,object?>(StringComparer.Ordinal);
        for(var index=0;index<values.Length;index++){
            result[this.sessions[index].Name]=values[index];
        }
        return result;
    }
    // ---- L2: 書き込みの中身(書いた区画は比べない: シャドーは区画が 1 つ) ----
    private void CompareWrites(TpcCRequest request,long txnIndex){
        UInt128? expected=null;
        var hashes=new UInt128[this.sessions.Count];
        for(var index=0;index<this.sessions.Count;index++){
            var builder=new TableDigestBuilder("writes");
            foreach(var record in this.sessions[index].Records){
                builder.Add(new{record.TableId,record.Kind,record.Key,record.Row});
            }
            hashes[index]=builder.Sum^(UInt128)builder.Count;
            expected??=hashes[index];
        }
        for(var index=1;index<hashes.Length;index++){
            if(hashes[index]!=expected){
                var writes=new object?[this.sessions.Count];
                for(var session=0;session<this.sessions.Count;session++){
                    writes[session]=this.sessions[session].Records.Select(static record=>$"{record.TableId}:{record.Kind}:{TpcCFailure.Describe(record.Key)}:{TpcCFailure.Describe(record.Row)}").ToArray();
                }
                throw new TpcCFailure(TpcCFailureKind.WriteSet,$"{request.Kind} が書いた中身が食い違いました。"){
                    TxnIndex=txnIndex,
                    Transaction=request.Kind,
                    Request=request,
                    Outputs=this.ByName(writes),
                };
            }
        }
    }
    // ---- L3: 読み戻し ----
    private void ReadBack(TpcCRequest request,long txnIndex){
        foreach(var session in this.sessions){
            var partitions=session.RequireEngine.Database.PartitionBases;
            var last=new Dictionary<(int,int,string),TpcCWriteRecord>();
            foreach(var record in session.Records){
                last[(record.Partition,record.TableId,Convert.ToHexString(CanonicalWriter.ToBytes(record.Key)))]=record;
            }
            foreach(var record in last.Values){
                var table=partitions[record.Partition].Tables[record.TableId-1];
                var found=table.TryGetBoxed(record.Key,out var row);
                var expectPresent=record.Kind is WriteKind.Insert or WriteKind.Update or WriteKind.UndoUpdate or WriteKind.UndoDelete;
                if(found!=expectPresent||(found&&!CanonicalWriter.ToBytes(row).AsSpan().SequenceEqual(CanonicalWriter.ToBytes(record.Row)))){
                    throw new TpcCFailure(TpcCFailureKind.ReadBack,$"{session.Name}: {table.Name} の {TpcCFailure.Describe(record.Key)} を読み直すと書いた値と違います。"){
                        TxnIndex=txnIndex,
                        Transaction=request.Kind,
                        Engine=session.Name,
                        Request=request,
                        Outputs=new Dictionary<string,object?>{["書いた値"]=record.Row,["読んだ値"]=row},
                    };
                }
            }
        }
    }
    // ---- L4: ダイジェスト・一貫性・索引 ----
    private DatabaseDigest[] CompareDigests(string when,bool initial){
        var digests=new DatabaseDigest[this.sessions.Count];
        for(var index=0;index<this.sessions.Count;index++){
            var session=this.sessions[index];
            var watch=Stopwatch.StartNew();
            digests[index]=TpcCStateDigest.Of(session.RequireEngine.Database);
            session.AddPhase("digest",watch.Elapsed.TotalSeconds);
            foreach(var table in digests[index].Tables){
                if(table.Name.EndsWith("#count-mismatch",StringComparison.Ordinal)){
                    throw new TpcCFailure(TpcCFailureKind.State,$"{session.Name}: {when}、{table.Name} 表の件数と列挙した行数が違います。"){
                        TxnIndex=this.driver?.State.TxnIndex??0,
                        Engine=session.Name,
                    };
                }
            }
            this.CheckInvariants(session,when,initial);
        }
        for(var index=1;index<digests.Length;index++){
            var differences=digests[0].Differences(digests[index]);
            if(differences.Count>0){
                throw this.StateFailure(when,0,index,differences);
            }
        }
        return digests;
    }
    /// <summary>
    /// 一貫性の条件(1〜12、条件 11 は初期データのとき)と、副索引と表の一致、ITEM の複製の一致。
    /// HammerDB の形では、初期データの明細金額が仕様と逆なので条件 10・12 は初めから外れている(Q-027・Q-046)ため、そこは失敗に数えない。
    /// </summary>
    private void CheckInvariants(TpcCVariantSession session,string when,bool initial){
        var database=session.RequireEngine.Database;
        var details=new List<string>();
        foreach(var result in TpcCConsistency.Check(database,deep:true,initial:initial)){
            var expectedToFail=this.options.Mode==TpcCMode.HammerDbMssql&&(result.Condition==10||result.Condition==12);
            if(!result.Passed&&!expectedToFail){
                details.Add($"{session.Name}: 一貫性の条件 {result.Condition} ({result.Name}): {result.Violations} 件 {result.Detail}");
            }
        }
        foreach(var problem in TpcCIndexConsistency.Check(database)){
            details.Add($"{session.Name}: 索引と表の食い違い: {problem}");
        }
        foreach(var partition in TpcCStateDigest.ItemReplicaDifferences(database)){
            details.Add($"{session.Name}: 区画 {partition} の ITEM が区画 0 と違います");
        }
        if(details.Count>0){
            throw new TpcCFailure(TpcCFailureKind.State,$"{when}に不変条件が崩れました({session.Name})。"){
                TxnIndex=this.driver?.State.TxnIndex??0,
                Engine=session.Name,
                Details=details,
            };
        }
    }
    private TpcCFailure StateFailure(string when,int left,int right,IReadOnlyList<string> differences){
        var details=new List<string>{$"{when}: {this.sessions[left].Name} と {this.sessions[right].Name} で食い違う表と索引: {string.Join(", ",differences)}"};
        return new(TpcCFailureKind.State,$"{when}に表または索引が食い違いました。"){
            TxnIndex=this.driver?.State.TxnIndex??0,
            Details=details,
        };
    }
    // ---- 保存と開き直し ----
    private void SaveAll(bool reopen){
        this.saveCount++;
        DatabaseDigest[]? before=null;
        if(reopen){
            before=this.CompareDigests("保存の前",false);
        }
        // 保存は、操作用オブジェクトを閉じて行う回と開いたまま行う回を交互にする(件数を記録する経路と記録しない経路の両方を通す)。
        var release=this.saveCount%2==0;
        foreach(var session in this.sessions){
            var watch=Stopwatch.StartNew();
            if(release){
                session.RequireEngine.Database.ReleaseHandles();
            }
            session.Store!.Save();
            if(release){
                session.RequireEngine.Database.AcquireHandles();
            }
            session.AddPhase("save",watch.Elapsed.TotalSeconds);
        }
        if(!reopen){
            return;
        }
        foreach(var session in this.sessions){
            var watch=Stopwatch.StartNew();
            session.Engine?.Dispose();
            session.Engine=null;
            session.Store?.Dispose();
            session.Store=null;
            session.AddPhase("close",watch.Elapsed.TotalSeconds);
            this.OpenSession(session);
            TpcCFaultInjection.AfterReopen(this.options.InjectFault,session,this.driver!.State.TxnIndex);
        }
        DatabaseDigest[] after;
        try{
            after=this.CompareDigests("開き直しの後",false);
        }catch(TpcCFailure failure) when(failure.Kind==TpcCFailureKind.State){
            throw new TpcCFailure(TpcCFailureKind.Reopen,failure.Message,failure){TxnIndex=failure.TxnIndex,Engine=failure.Engine,Details=failure.Details};
        }
        for(var index=0;index<this.sessions.Count;index++){
            var differences=before![index].Differences(after[index]);
            if(differences.Count>0){
                throw new TpcCFailure(TpcCFailureKind.Reopen,$"{this.sessions[index].Name}: 保存→開き直しで表または索引が変わりました: {string.Join(", ",differences)}"){
                    TxnIndex=this.driver!.State.TxnIndex,
                    Engine=this.sessions[index].Name,
                };
            }
        }
        this.Log($"保存→開き直し {this.saveCount} 回目: 一致(取引 {this.driver!.State.TxnIndex:N0} 件の時点)");
    }
    private void Finish(){
        if(this.options.SaveEvery>0){
            this.SaveAll(true);
        }else{
            this.CompareDigests("最後",false);
        }
        this.Log($"完了: 取引 {this.driver!.State.TxnIndex:N0} 件、{this.wall.Elapsed.TotalSeconds:F1} 秒、全組み合わせが一致");
    }
    private void CloseAll(){
        foreach(var session in this.sessions){
            try{
                session.Engine?.Dispose();
            }catch(Exception error){
                this.cleanupErrors.Add(new InvalidOperationException(session.Name+": engine disposal failed.",error));
                this.CleanupLog($"{session.Name}: エンジンを閉じる途中の例外: {error}");
            }
            session.Engine=null;
            try{
                session.Store?.Dispose();
            }catch(Exception error){
                this.cleanupErrors.Add(new InvalidOperationException(session.Name+": store disposal failed.",error));
                this.CleanupLog($"{session.Name}: ストアを閉じる途中の例外: {error}");
            }
            session.Store=null;
        }
    }
    // ---- 記録 ----
    private void Trace(long txnIndex,TpcCRequest request,TpcCResult result){
        var line=JsonSerializer.Serialize(new{Txn=txnIndex,Kind=request.Kind.ToString(),Request=TpcCFailure.Describe(request),Result=TpcCFailure.Describe(result)});
        File.AppendAllText(Path.Combine(this.runDirectory,"trace.log"),line+Environment.NewLine);
    }
    private void Report(TpcCFailure failure){
        this.failureWritten=true;
        try{
            failure.Write(Path.Combine(this.runDirectory,"failure"),this.options,this.lastAgreedTxn);
            this.WriteSummary(failure);
        }catch(Exception error){
            this.Log("失敗の記録を書けませんでした: "+error.Message);
        }
        this.Log($"失敗: {failure.Kind}: {failure.Message}");
        foreach(var detail in failure.Details.Take(20)){
            this.Log("  "+detail);
        }
        if(failure.InnerException is not null){
            this.Log(failure.InnerException.ToString());
        }
        var replay=this.options with{StopAt=failure.TxnIndex+1,TraceFrom=Math.Max(1,failure.TxnIndex-20)};
        this.Log("再現コマンド: "+replay.ToCommandLine());
        this.Log("記録: "+Path.Combine(this.runDirectory,"failure"));
    }
    private void WriteSummary(TpcCFailure? failure){
        var summary=TpcCRunSummary.Build(this.options,this.sessions,this.driver?.State,this.wall.Elapsed,failure);
        File.WriteAllText(Path.Combine(this.runDirectory,"summary.json"),JsonSerializer.Serialize(summary,TpcCReportJson.Options));
        var text=TpcCRunSummary.ToText(summary);
        File.WriteAllText(Path.Combine(this.runDirectory,"summary.txt"),text);
        File.WriteAllText(Path.Combine(this.runDirectory,"txn-stats.csv"),TpcCRunSummary.ToCsv(summary));
        this.log.WriteLine(text);
    }
    private void Log(string message){
        this.log.WriteLine($"[{this.wall.Elapsed:hh\\:mm\\:ss}] {message}");
    }
    private void CleanupLog(string message){
        try{
            this.Log(message);
        }catch(Exception error){
            // 診断出力の失敗は保持するが、所有権や残りのcleanupを変えない。
            this.cleanupErrors.Add(new InvalidOperationException("終了診断の記録に失敗しました。",error));
        }
    }
    internal static bool IsResourceLimit(Exception error){
        for(var current=error;current is not null;current=current.InnerException){
            if(current is ResourceLimitException||current is OutOfMemoryException||current.GetType().Name.Contains("LogFull",StringComparison.Ordinal)){
                return true;
            }
        }
        return false;
    }
    private static void TryDelete(string directory){
        try{
            if(System.IO.Directory.Exists(directory)){
                System.IO.Directory.Delete(directory,true);
            }
        }catch(IOException){
        }catch(UnauthorizedAccessException){
        }
    }
}
