using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
/// <summary>書き込みの記録(L2・L3 用)。計測の内側では積むだけ。</summary>
public readonly record struct WriteRecord(int TableId,WriteKind Kind,object Key,object? Row);
internal sealed class WriteRecorder:IWriteObserver{
    public readonly List<WriteRecord> Records=[];
    public void OnWrite(int tableId,WriteKind kind,object key,object? row)=>this.Records.Add(new(tableId,kind,key,row));
}
/// <summary>1 つの組み合わせの実行中の資源と計測。</summary>
public sealed class VariantSession(ITpcEVariant variant,string directory){
    public ITpcEVariant Variant{get;}=variant;
    public string Directory{get;}=directory;
    public Store? Store{get;set;}
    public ITpcEEngine? Engine{get;set;}
    internal WriteRecorder Recorder{get;}=new();
    public System.Collections.Generic.Dictionary<TxnKind,LatencyHistogram> Timings{get;}=[];
    public System.Collections.Generic.Dictionary<string,double> PhaseSeconds{get;}=new(StringComparer.Ordinal);
    public ITpcEEngine RequireEngine=>this.Engine??throw new InvalidOperationException($"{this.Variant.Name} is not open.");
    public void AddPhase(string name,double seconds){
        this.PhaseSeconds[name]=this.PhaseSeconds.GetValueOrDefault(name)+seconds;
    }
}
/// <summary>実行の結果の要約。</summary>
public sealed record RunOutcome(int ExitCode,string RunDirectory,TpcEFailure? Failure,DriverState? FinalState);
/// <summary>
/// 4 通りを 1 プロセス・1 スレッドで交互に流し、取引 1 件ごとに出力(L1)・書き込みの中身(L2)・読み戻し(L3)を比べる。
/// 保存→閉じる→開き直しの前後と最後に、全表と全索引のダイジェスト(L4)を比べる。食い違いや例外はその場で止める。
/// </summary>
public sealed class LockstepRunner{
    private readonly RunOptions options;
    private readonly IReadOnlyList<VariantSession> sessions;
    private readonly TextWriter log;
    private readonly string runDirectory;
    private Driver? driver;
    private long lastAgreedTxn;
    private int saveCount;
    private readonly Stopwatch wall=new();
    public LockstepRunner(RunOptions options,IReadOnlyList<ITpcEVariant> variants,string runDirectory,TextWriter log){
        this.options=options;
        this.runDirectory=runDirectory;
        this.log=log;
        var sessions=new List<VariantSession>();
        foreach(var variant in variants){
            sessions.Add(new(variant,Path.Combine(runDirectory,"stores",variant.Name)));
        }
        this.sessions=sessions;
    }
    public IReadOnlyList<VariantSession> Sessions=>this.sessions;
    public Driver? Driver=>this.driver;
    private volatile bool stopRequested;
    /// <summary>Ctrl+C から呼ぶ。取引の境目で止めて、保存と比較をしてから終わる。</summary>
    public void RequestStop()=>this.stopRequested=true;
    public bool Stopped{get;private set;}
    private StoreLimits Limits{
        get{
            var memory=TpcESizing.DefaultMemoryBytes(this.options.Scale);
            if(this.options.MemoryMegabytes>0){
                memory=this.options.MemoryMegabytes*1024*1024;
            }
            return new(){MemoryBytes=memory,DiskBytes=this.options.DiskGigabytes*1024L*1024*1024};
        }
    }
    public RunOutcome Run(){
        this.wall.Start();
        try{
            this.Prepare();
            this.Loop();
            this.Finish();
            this.WriteSummary(null);
            var exitCode=0;
            if(this.Stopped){
                exitCode=130;
            }
            return new(exitCode,this.runDirectory,null,this.driver?.State);
        }catch(TpcEFailure failure){
            this.Report(failure);
            return new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }catch(Exception error) when(IsResourceLimit(error)){
            var failure=new TpcEFailure(FailureKind.Resource,"資源の上限に達しました: "+error.Message,error){TxnIndex=this.driver?.State.TxnIndex??0};
            this.Report(failure);
            return new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }catch(Exception error){
            var failure=new TpcEFailure(FailureKind.Exception,"想定外の例外: "+error.Message,error){TxnIndex=this.driver?.State.TxnIndex??0};
            this.Report(failure);
            return new(failure.ExitCode,this.runDirectory,failure,this.driver?.State);
        }finally{
            this.CloseAll();
            if(!this.options.KeepStores&&!this.failureWritten){
                TryDelete(Path.Combine(this.runDirectory,"stores"));
            }
        }
    }
    private bool failureWritten;
    private readonly System.Collections.Generic.Dictionary<(TxnKind Kind,int Status),long> statusCounts=[];
    // ---- 準備: ストアを開き、初期データを 1 回だけ生成して 4 通りへ配る ----
    private void Prepare(){
        foreach(var session in this.sessions){
            if(System.IO.Directory.Exists(session.Directory)&&System.IO.Directory.EnumerateFileSystemEntries(session.Directory).Any()){
                throw new ArgumentException($"The store directory {session.Directory} is not empty.");
            }
            this.OpenSession(session);
        }
        var generator=new TpcEDataGenerator(new TpcEModel(this.options.Seed,this.options.Scale));
        var loadWatch=Stopwatch.StartNew();
        foreach(var session in this.sessions){
            session.RequireEngine.Database.BeginBulkLoad();
        }
        var sinks=new List<ILoadSink>();
        foreach(var session in this.sessions){
            sinks.Add(session.RequireEngine.Loader);
        }
        var generation=generator.Generate(new FanOutLoadSink(sinks));
        foreach(var session in this.sessions){
            var watch=Stopwatch.StartNew();
            session.RequireEngine.Database.EndBulkLoad();
            session.AddPhase("load-flush",watch.Elapsed.TotalSeconds);
        }
        this.Log($"初期データ: 取引 {generation.LastInitialTradeId:N0} 件、未約定 {generation.UnfinishedTrades} 件、{loadWatch.Elapsed.TotalSeconds:F1} 秒");
        this.driver=new Driver(Driver.Initial(this.options.Seed,this.options.Scale,generation));
        this.CompareDigests("初期データ");
        if(this.options.SaveEvery>0){
            this.SaveAll(false);
        }
    }
    private void OpenSession(VariantSession session){
        var watch=Stopwatch.StartNew();
        var store=Store.Open(session.Directory,this.Limits);
        session.Store=store;
        // 運転状態は最初に登録する(保存済みなら登録時に読み戻され、ここで照合する)。
        store.Register(new DriverStateComponent(()=>this.driver!.State,state=>this.CheckRestoredState(session,state)));
        session.Engine=session.Variant.Open(store,this.options.Scale);
        session.AddPhase("open",watch.Elapsed.TotalSeconds);
    }
    private void CheckRestoredState(VariantSession session,DriverState restored){
        if(this.driver is null){
            return;
        }
        var expected=JsonSerializer.Serialize(this.driver.State,DriverJson.Options);
        var actual=JsonSerializer.Serialize(restored,DriverJson.Options);
        if(expected!=actual){
            throw new TpcEFailure(FailureKind.Reopen,$"{session.Variant.Name}: 開き直した運転状態が保存前と違います。"){
                TxnIndex=this.driver.State.TxnIndex,
                Engine=session.Variant.Name,
                Details=["保存前: "+expected,"開き直し後: "+actual],
            };
        }
    }
    // ---- 取引を流す ----
    private void Loop(){
        var driver=this.driver!;
        var lastSavedCe=driver.State.CeCount;
        var nextProgress=Stopwatch.GetTimestamp()+Stopwatch.Frequency*10;
        while(driver.State.CeCount<this.options.Transactions){
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
            var measured=driver.State.CeCount>this.options.Warmup;
            var results=this.ExecuteAll(request,txnIndex,measured);
            var statusKey=(request.Kind,results[0].Status);
            this.statusCounts[statusKey]=this.statusCounts.GetValueOrDefault(statusKey)+1;
            driver.Completed(request,results[0]);
            this.lastAgreedTxn=txnIndex;
            if(this.options.TraceFrom>0&&txnIndex>=this.options.TraceFrom){
                this.Trace(txnIndex,request,results[0]);
            }
            if(this.options.SaveEvery>0&&driver.State.CeCount-lastSavedCe>=this.options.SaveEvery){
                lastSavedCe=driver.State.CeCount;
                var reopen=this.options.ReopenEvery>0&&(this.saveCount+1)%this.options.ReopenEvery==0;
                this.SaveAll(reopen);
            }
            if(Stopwatch.GetTimestamp()>=nextProgress){
                nextProgress=Stopwatch.GetTimestamp()+Stopwatch.Frequency*10;
                this.Log($"取引 {txnIndex:N0} 件(CE {driver.State.CeCount:N0}/{this.options.Transactions:N0})、{this.wall.Elapsed.TotalSeconds:F0} 秒");
            }
        }
    }
    private TxnResult[] ExecuteAll(TxnRequest request,long txnIndex,bool measured){
        var count=this.sessions.Count;
        var results=new TxnResult[count];
        // 呼ぶ順を取引ごとに回す(キャッシュと GC の偏りをならす)。
        var offset=(int)(txnIndex%count);
        for(var step=0;step<count;step++){
            var index=(offset+step)%count;
            var session=this.sessions[index];
            var engine=session.RequireEngine;
            session.Recorder.Records.Clear();
            engine.Database.Log.Observer=session.Recorder;
            var gcBefore=GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2);
            var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
            var started=Stopwatch.GetTimestamp();
            TxnResult result;
            try{
                result=engine.Execute(request);
            }catch(Exception error){
                var kind=FailureKind.Exception;
                if(IsResourceLimit(error)){
                    kind=FailureKind.Resource;
                }
                throw new TpcEFailure(kind,$"{session.Variant.Name}: {request.Kind} で例外 {error.GetType().Name}: {error.Message}",error){
                    TxnIndex=txnIndex,
                    Transaction=request.Kind,
                    Engine=session.Variant.Name,
                    Request=request,
                };
            }finally{
                engine.Database.Log.Observer=null;
            }
            var elapsed=Stopwatch.GetTimestamp()-started;
            if(measured){
                if(!session.Timings.TryGetValue(request.Kind,out var histogram)){
                    histogram=new();
                    session.Timings.Add(request.Kind,histogram);
                }
                histogram.Add(elapsed,GC.GetAllocatedBytesForCurrentThread()-allocatedBefore,GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2)-gcBefore);
            }
            results[index]=FaultInjection.AfterTransaction(this.options.InjectFault,session,txnIndex,result);
        }
        this.CompareOutputs(request,txnIndex,results);
        this.CompareWrites(request,txnIndex);
        this.ReadBack(request,txnIndex);
        return results;
    }
    // ---- L1: 出力 ----
    private void CompareOutputs(TxnRequest request,long txnIndex,TxnResult[] results){
        var expected=CanonicalWriter.ToBytes(results[0]);
        for(var index=1;index<results.Length;index++){
            if(!CanonicalWriter.ToBytes(results[index]).AsSpan().SequenceEqual(expected)){
                throw new TpcEFailure(FailureKind.Output,$"{request.Kind} の出力が食い違いました({this.Grouping(results)})。"){
                    TxnIndex=txnIndex,
                    Transaction=request.Kind,
                    Request=request,
                    Outputs=this.ByName(results),
                    Details=[this.Grouping(results)],
                };
            }
        }
    }
    /// <summary>結果のハッシュで組み合わせを組に分ける(原因の当たり: 保存先か FLinq か)。</summary>
    private string Grouping(TxnResult[] results){
        var groups=new System.Collections.Generic.Dictionary<UInt128,List<string>>();
        for(var index=0;index<results.Length;index++){
            var hash=CanonicalWriter.Hash(results[index]);
            if(!groups.TryGetValue(hash,out var names)){
                names=[];
                groups.Add(hash,names);
            }
            names.Add(this.sessions[index].Variant.ShortName);
        }
        var parts=new List<string>();
        foreach(var names in groups.Values){
            parts.Add("{"+string.Join(",",names)+"}");
        }
        return string.Join(" 対 ",parts);
    }
    private IReadOnlyDictionary<string,object?> ByName<T>(T[] values){
        var result=new System.Collections.Generic.Dictionary<string,object?>(StringComparer.Ordinal);
        for(var index=0;index<values.Length;index++){
            result[this.sessions[index].Variant.Name]=values[index];
        }
        return result;
    }
    // ---- L2: 書き込みの中身 ----
    private void CompareWrites(TxnRequest request,long txnIndex){
        UInt128? expected=null;
        var hashes=new UInt128[this.sessions.Count];
        for(var index=0;index<this.sessions.Count;index++){
            var builder=new TableDigestBuilder("writes");
            foreach(var record in this.sessions[index].Recorder.Records){
                builder.Add(record);
            }
            hashes[index]=builder.Sum^(UInt128)builder.Count;
            expected??=hashes[index];
        }
        for(var index=1;index<hashes.Length;index++){
            if(hashes[index]!=expected){
                var writes=new object?[this.sessions.Count];
                for(var session=0;session<this.sessions.Count;session++){
                    writes[session]=this.sessions[session].Recorder.Records.Select(static record=>$"{record.TableId}:{record.Kind}:{TpcEFailure.Describe(record.Key)}:{TpcEFailure.Describe(record.Row)}").ToArray();
                }
                throw new TpcEFailure(FailureKind.WriteSet,$"{request.Kind} が書いた中身が食い違いました。"){
                    TxnIndex=txnIndex,
                    Transaction=request.Kind,
                    Request=request,
                    Outputs=this.ByName(writes),
                };
            }
        }
    }
    // ---- L3: 読み戻し ----
    private void ReadBack(TxnRequest request,long txnIndex){
        foreach(var session in this.sessions){
            var tables=session.RequireEngine.Database.Tables;
            var last=new System.Collections.Generic.Dictionary<(int,string),WriteRecord>();
            foreach(var record in session.Recorder.Records){
                last[(record.TableId,Convert.ToHexString(CanonicalWriter.ToBytes(record.Key)))]=record;
            }
            foreach(var record in last.Values){
                var table=tables[record.TableId-1];
                var found=table.TryGetBoxed(record.Key,out var row);
                var expectPresent=record.Kind is WriteKind.Insert or WriteKind.Update or WriteKind.UndoUpdate or WriteKind.UndoDelete;
                if(found!=expectPresent||(found&&!CanonicalWriter.ToBytes(row).AsSpan().SequenceEqual(CanonicalWriter.ToBytes(record.Row)))){
                    throw new TpcEFailure(FailureKind.ReadBack,$"{session.Variant.Name}: {table.Name} の {TpcEFailure.Describe(record.Key)} を読み直すと書いた値と違います。"){
                        TxnIndex=txnIndex,
                        Transaction=request.Kind,
                        Engine=session.Variant.Name,
                        Request=request,
                        Outputs=new System.Collections.Generic.Dictionary<string,object?>{["書いた値"]=record.Row,["読んだ値"]=row},
                    };
                }
            }
        }
    }
    // ---- L4: ダイジェスト ----
    private DatabaseDigest[] CompareDigests(string when){
        var digests=new DatabaseDigest[this.sessions.Count];
        for(var index=0;index<this.sessions.Count;index++){
            var watch=Stopwatch.StartNew();
            digests[index]=StateDigest.Of(this.sessions[index].RequireEngine.Database);
            this.sessions[index].AddPhase("digest",watch.Elapsed.TotalSeconds);
            foreach(var table in digests[index].Tables){
                if(table.Name.EndsWith("#count-mismatch",StringComparison.Ordinal)){
                    throw new TpcEFailure(FailureKind.State,$"{this.sessions[index].Variant.Name}: {when}、{table.Name} 表の件数と列挙した行数が違います。"){
                        TxnIndex=this.driver?.State.TxnIndex??0,
                        Engine=this.sessions[index].Variant.Name,
                    };
                }
            }
        }
        for(var index=1;index<digests.Length;index++){
            var differences=digests[0].Differences(digests[index]);
            if(differences.Count>0){
                throw this.StateFailure(when,0,index,differences);
            }
        }
        return digests;
    }
    private TpcEFailure StateFailure(string when,int left,int right,IReadOnlyList<string> differences){
        var details=new List<string>{$"{when}: {this.sessions[left].Variant.Name} と {this.sessions[right].Variant.Name} で食い違う表と索引: {string.Join(", ",differences)}"};
        var leftDb=this.sessions[left].RequireEngine.Database;
        var rightDb=this.sessions[right].RequireEngine.Database;
        foreach(var name in differences){
            var leftTable=leftDb.Tables.FirstOrDefault(table=>table.Name==name);
            var rightTable=rightDb.Tables.FirstOrDefault(table=>table.Name==name);
            if(leftTable is null||rightTable is null){
                continue;
            }
            foreach(var difference in StateDigest.DifferentRows(leftTable,rightTable,10)){
                details.Add($"{difference.Table} {TpcEFailure.Describe(difference.Key)}: {this.sessions[left].Variant.ShortName}={TpcEFailure.Describe(difference.Left)} / {this.sessions[right].Variant.ShortName}={TpcEFailure.Describe(difference.Right)}");
            }
        }
        return new(FailureKind.State,$"{when}に表または索引が食い違いました。"){
            TxnIndex=this.driver?.State.TxnIndex??0,
            Details=details,
        };
    }
    // ---- 保存と開き直し ----
    private void SaveAll(bool reopen){
        this.saveCount++;
        DatabaseDigest[]? before=null;
        if(reopen){
            before=this.CompareDigests("保存の前");
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
            FaultInjection.AfterReopen(this.options.InjectFault,session,this.driver!.State.TxnIndex);
        }
        var after=this.CompareDigests("開き直しの後");
        for(var index=0;index<this.sessions.Count;index++){
            var differences=before![index].Differences(after[index]);
            if(differences.Count>0){
                throw new TpcEFailure(FailureKind.Reopen,$"{this.sessions[index].Variant.Name}: 保存→開き直しで表または索引が変わりました: {string.Join(", ",differences)}"){
                    TxnIndex=this.driver!.State.TxnIndex,
                    Engine=this.sessions[index].Variant.Name,
                };
            }
        }
        this.Log($"保存→開き直し {this.saveCount} 回目: 一致(取引 {this.driver!.State.TxnIndex:N0} 件の時点)");
    }
    private void Finish(){
        if(this.options.SaveEvery>0){
            this.SaveAll(true);
        }else{
            this.CompareDigests("最後");
        }
        this.Log($"完了: 取引 {this.driver!.State.TxnIndex:N0} 件、{this.wall.Elapsed.TotalSeconds:F1} 秒、全組み合わせが一致");
    }
    private void CloseAll(){
        foreach(var session in this.sessions){
            try{
                session.Engine?.Dispose();
            }catch(Exception error){
                this.Log($"{session.Variant.Name}: エンジンを閉じる途中の例外: {error.Message}");
            }
            session.Engine=null;
            try{
                session.Store?.Dispose();
            }catch(Exception error){
                this.Log($"{session.Variant.Name}: ストアを閉じる途中の例外: {error.Message}");
            }
            session.Store=null;
        }
    }
    // ---- 記録 ----
    private void Trace(long txnIndex,TxnRequest request,TxnResult result){
        var line=JsonSerializer.Serialize(new{Txn=txnIndex,Kind=request.Kind.ToString(),Request=TpcEFailure.Describe(request),Result=TpcEFailure.Describe(result)});
        File.AppendAllText(Path.Combine(this.runDirectory,"trace.log"),line+Environment.NewLine);
    }
    private void Report(TpcEFailure failure){
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
    private void WriteSummary(TpcEFailure? failure){
        var summary=RunSummary.Build(this.options,this.sessions,this.driver?.State,this.wall.Elapsed,failure,this.statusCounts);
        File.WriteAllText(Path.Combine(this.runDirectory,"summary.json"),JsonSerializer.Serialize(summary,ReportJson.Options));
        var text=RunSummary.ToText(summary);
        File.WriteAllText(Path.Combine(this.runDirectory,"summary.txt"),text);
        File.WriteAllText(Path.Combine(this.runDirectory,"txn-stats.csv"),RunSummary.ToCsv(summary));
        this.log.WriteLine(text);
    }
    private void Log(string message){
        this.log.WriteLine($"[{this.wall.Elapsed:hh\\:mm\\:ss}] {message}");
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
