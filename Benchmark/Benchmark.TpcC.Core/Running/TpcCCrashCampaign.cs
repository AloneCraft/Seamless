using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>
/// 強制終了の検査(親)。子プロセス(crash-child)に 1 つの組み合わせで取引を流させ、保存の決まった段階や時間で強制終了させる。
/// 子が終わったら、ストアを複製して(証拠)から開き、運転状態 <c>~driver</c> の通し番号が許される範囲にあるか、
/// 同じ種で別に進めた参照のダイジェストと一致するかを確かめる。次の周期は同じフォルダの続きから動かす。
/// </summary>
public sealed class TpcCCrashCampaign(TpcCRunOptions options,IReadOnlyList<ITpcCVariant> variants,string runDirectory,TextWriter log,(string FileName,IReadOnlyList<string> Prefix) childCommand){
    private readonly Stopwatch wall=Stopwatch.StartNew();
    private readonly StringBuilder cycles=new("cycle,variant,kill,kill_at,kill_write,last_done,in_progress,recovered,expected,result,open_ms,digest_ms,store_bytes\n");
    public int Run(){
        try{
            if(options.SaveEvery<=0||options.SaveEvery>int.MaxValue/3||options.Cycles<=0){
                throw new ArgumentException("crash needs a positive bounded --save-every and --cycles.");
            }
            for(var index=0;index<variants.Count;index++){
                this.RunVariant(variants[index]);
            }
            this.Log($"強制終了の検査: すべて一致({this.wall.Elapsed.TotalSeconds:F0} 秒)");
            return 0;
        }catch(TpcCFailure failure){
            failure.Write(Path.Combine(runDirectory,"failure"),options,0);
            this.Log($"失敗: {failure.Kind}: {failure.Message}");
            foreach(var detail in failure.Details){
                this.Log("  "+detail);
            }
            return failure.ExitCode;
        }catch(Exception error){
            var kind=TpcCFailureKind.Exception;
            if(TpcCLockstepRunner.IsResourceLimit(error)){
                kind=TpcCFailureKind.Resource;
            }
            var failure=new TpcCFailure(kind,"強制終了の検査で例外: "+error.Message,error);
            failure.Write(Path.Combine(runDirectory,"failure"),options,0);
            this.Log(failure.Message);
            return failure.ExitCode;
        }finally{
            Directory.CreateDirectory(Path.Combine(runDirectory,"crash"));
            File.WriteAllText(Path.Combine(runDirectory,"crash","cycles.csv"),this.cycles.ToString());
        }
    }
    private void RunVariant(ITpcCVariant variant){
        var root=Path.Combine(runDirectory,"crash",variant.Name);
        var storeDirectory=Path.Combine(root,"store");
        Directory.CreateDirectory(storeDirectory);
        using var reference=new ReferenceTrack(variant,options with{Transactions=options.SaveEvery*3});
        long committed=-1;
        for(var cycle=1;cycle<=options.Cycles;cycle++){
            var rng=Rng.For(options.Seed,"crash-"+variant.ShortName,cycle);
            TpcCKillMode[] modes=[TpcCKillMode.BeforeSave,TpcCKillMode.InFirstSave,TpcCKillMode.InLastFlush,TpcCKillMode.AfterManifest,TpcCKillMode.AfterSave,TpcCKillMode.MidTransaction,TpcCKillMode.Timed];
            int[] weights=[15,10,10,15,10,25,15];
            var mode=modes[rng.Weighted(weights)];
            var killAt=(long)rng.Range(1,3);
            var killWrite=rng.Range(1,6);
            if(mode==TpcCKillMode.MidTransaction){
                killAt=Math.Max(1,committed)+rng.Range(1,(int)Math.Max(2,options.SaveEvery*2));
            }
            var arguments=new TpcCCrashChild.Arguments(options with{Command="crash-child",Transactions=options.SaveEvery*3},storeDirectory,variant.ShortName,mode,killAt,killWrite);
            var (lines,exitCode,timedOut)=this.RunChild(arguments,mode,ref rng);
            if(timedOut){
                throw new TpcCFailure(TpcCFailureKind.Hang,$"{variant.Name}: 子プロセスが時間内に終わりませんでした(周期 {cycle})。"){Details=lines};
            }
            var lastDone=committed;
            long inProgress=-1;
            var killed=false;
            var ended=false;
            foreach(var line in lines){
                if(line.StartsWith("@@SAVE_BEGIN ",StringComparison.Ordinal)){
                    inProgress=long.Parse(line[13..],CultureInfo.InvariantCulture);
                }else if(line.StartsWith("@@SAVE_DONE ",StringComparison.Ordinal)){
                    lastDone=long.Parse(line[12..],CultureInfo.InvariantCulture);
                    inProgress=-1;
                }else if(line.StartsWith("@@KILL",StringComparison.Ordinal)){
                    killed=true;
                }else if(line.StartsWith("@@END",StringComparison.Ordinal)){
                    ended=true;
                }
            }
            if(!killed&&!ended&&mode!=TpcCKillMode.Timed){
                throw new TpcCFailure(TpcCFailureKind.Exception,$"{variant.Name}: 子プロセスが異常終了しました(終了コード {exitCode}、周期 {cycle})。"){Details=lines};
            }
            // 許される復旧点。
            var allowed=new List<long>();
            if(mode==TpcCKillMode.AfterManifest&&killed){
                allowed.Add(inProgress);
            }else if(mode==TpcCKillMode.Timed){
                allowed.Add(lastDone);
                if(inProgress>=0){
                    allowed.Add(inProgress);
                }
            }else{
                allowed.Add(lastDone);
            }
            // 証拠を残してから開く(開くと保存先がファイルを片付けるため)。
            var evidence=Path.Combine(root,$"cycle-{cycle:D3}");
            CopyDirectory(storeDirectory,Path.Combine(evidence,"store"));
            File.WriteAllLines(Path.Combine(evidence,"child.log"),lines);
            var openWatch=Stopwatch.StartNew();
            var recovered=Recover(variant,storeDirectory,options,out var digest,out var digestMilliseconds);
            var openMilliseconds=openWatch.ElapsedMilliseconds-digestMilliseconds;
            var recoveredIndex=-1L;
            if(recovered is not null){
                recoveredIndex=recovered.TxnIndex;
            }
            var result="ok";
            try{
                if(!allowed.Contains(recoveredIndex)){
                    throw new TpcCFailure(TpcCFailureKind.Crash,$"{variant.Name}: 強制終了({mode})の後、開き直した状態の通し番号 {recoveredIndex} が期待 {string.Join(" か ",allowed)} と違います(周期 {cycle})。"){
                        TxnIndex=recoveredIndex,
                        Engine=variant.Name,
                        Details=[..lines,"証拠: "+evidence],
                    };
                }
                if(recovered is not null&&digest is not null){
                    var expected=reference.AdvanceTo(recoveredIndex);
                    var differences=expected.Digest.Differences(digest);
                    if(differences.Count>0){
                        throw new TpcCFailure(TpcCFailureKind.Crash,$"{variant.Name}: 強制終了({mode})から開き直した表が、同じ通し番号 {recoveredIndex} の参照と違います: {string.Join(", ",differences)}(周期 {cycle})。"){
                            TxnIndex=recoveredIndex,
                            Engine=variant.Name,
                            Details=[..lines,"証拠: "+evidence],
                        };
                    }
                    var expectedState=JsonSerializer.Serialize(expected.State,TpcCDriverJson.Options);
                    var actualState=JsonSerializer.Serialize(recovered,TpcCDriverJson.Options);
                    if(expectedState!=actualState){
                        throw new TpcCFailure(TpcCFailureKind.Crash,$"{variant.Name}: 開き直した運転状態が参照と違います(通し番号 {recoveredIndex}、周期 {cycle})。"){
                            TxnIndex=recoveredIndex,
                            Engine=variant.Name,
                            Details=["参照: "+expectedState,"開き直し: "+actualState],
                        };
                    }
                }
            }catch(TpcCFailure){
                result="fail";
                throw;
            }finally{
                this.cycles.AppendLine(string.Create(CultureInfo.InvariantCulture,$"{cycle},{variant.ShortName},{mode},{killAt},{killWrite},{lastDone},{inProgress},{recoveredIndex},{string.Join("|",allowed)},{result},{openMilliseconds},{digestMilliseconds},{DirectorySize(storeDirectory)}"));
            }
            if(result=="ok"){
                Directory.Delete(evidence,true);
            }
            committed=recoveredIndex;
            this.Log($"[{variant.ShortName}] 周期 {cycle}: {mode}(引数 {killAt})→ 通し番号 {recoveredIndex} に復旧、参照と一致");
        }
    }
    private (List<string> Lines,int ExitCode,bool TimedOut) RunChild(TpcCCrashChild.Arguments arguments,TpcCKillMode mode,ref Rng rng){
        var start=new ProcessStartInfo(childCommand.FileName){
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
            CreateNoWindow=true,
        };
        foreach(var item in childCommand.Prefix){
            start.ArgumentList.Add(item);
        }
        foreach(var item in TpcCCrashChild.ToArguments(arguments)){
            start.ArgumentList.Add(item);
        }
        var lines=new List<string>();
        using var process=new Process{StartInfo=start};
        process.OutputDataReceived+=(_,e)=>{
            if(e.Data is not null){
                lock(lines){
                    lines.Add(e.Data);
                }
            }
        };
        process.ErrorDataReceived+=(_,e)=>{
            if(e.Data is not null){
                lock(lines){
                    lines.Add("stderr: "+e.Data);
                }
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if(mode==TpcCKillMode.Timed){
            Thread.Sleep(rng.Range(300,4000));
            try{
                process.Kill();
            }catch(InvalidOperationException){
            }
        }
        var timedOut=!process.WaitForExit(TimeSpan.FromMinutes(10));
        if(timedOut){
            process.Kill(true);
        }
        process.WaitForExit();
        lock(lines){
            return ([..lines],process.ExitCode,timedOut);
        }
    }
    private static TpcCDriverState? Recover(ITpcCVariant variant,string directory,TpcCRunOptions options,out DatabaseDigest? digest,out long digestMilliseconds){
        TpcCDriverState? state=null;
        var limits=TpcCLockstepRunner.LimitsFor(options);
        using var store=OpenWithRetry(directory,limits);
        store.Register(new TpcCDriverStateComponent(()=>throw new InvalidOperationException("The checker does not save."),restored=>state=restored));
        using var engine=variant.Open(store,options.Scale,options.Mode);
        var watch=Stopwatch.StartNew();
        digest=null;
        if(state is not null){
            digest=TpcCStateDigest.Of(engine.Database);
        }
        digestMilliseconds=watch.ElapsedMilliseconds;
        return state;
    }
    /// <summary>強制終了の直後はファイルの共有違反が残ることがあるので、少し待ってやり直す。</summary>
    private static Store OpenWithRetry(string directory,StoreLimits limits){
        for(var attempt=0;;attempt++){
            try{
                return Store.Open(directory,limits);
            }catch(IOException) when(attempt<20){
                Thread.Sleep(250);
            }
        }
    }
    private static void CopyDirectory(string from,string to){
        Directory.CreateDirectory(to);
        foreach(var file in Directory.EnumerateFiles(from)){
            var name=Path.GetFileName(file);
            if(name=="store.lock"){
                continue;
            }
            File.Copy(file,Path.Combine(to,name),true);
        }
        foreach(var child in Directory.EnumerateDirectories(from)){
            CopyDirectory(child,Path.Combine(to,Path.GetFileName(child)));
        }
    }
    private static long DirectorySize(string directory){
        long total=0;
        foreach(var file in Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories)){
            total+=new FileInfo(file).Length;
        }
        return total;
    }
    private void Log(string message){
        log.WriteLine($"[{this.wall.Elapsed:hh\\:mm\\:ss}] {message}");
    }
    /// <summary>参照: 同じ組み合わせを一時ストアで最初から流し、指定の通し番号の状態を作る(番号は戻れない)。</summary>
    private sealed class ReferenceTrack:IDisposable{
        private readonly Store store;
        private readonly ITpcCEngine engine;
        private readonly TpcCDriver driver;
        public ReferenceTrack(ITpcCVariant variant,TpcCRunOptions options){
            this.store=Store.CreateTemporary(TpcCLockstepRunner.LimitsFor(options));
            this.engine=variant.Open(this.store,options.Scale,options.Mode);
            this.engine.Database.BeginBulkLoad();
            new TpcCDataGenerator(options.Scale,TpcCLoadRules.For(options.Mode),options.Seed).Generate(this.engine.Loader);
            this.engine.Database.EndBulkLoad();
            this.driver=new TpcCDriver(options.Scale,options.Mode,options.Seed,options.Terminals,TpcCDriver.Initial(options.Seed));
        }
        public (DatabaseDigest Digest,TpcCDriverState State) AdvanceTo(long txnIndex){
            if(this.driver.State.TxnIndex>txnIndex){
                throw new TpcCFailure(TpcCFailureKind.Crash,$"開き直した通し番号 {txnIndex} が、前の周期で確かめた番号 {this.driver.State.TxnIndex} より前に戻りました(確定した保存が失われた)。"){TxnIndex=txnIndex};
            }
            while(this.driver.State.TxnIndex<txnIndex){
                var request=this.driver.Next();
                var result=this.engine.Execute(request);
                this.driver.Completed(request,result);
            }
            return (TpcCStateDigest.Of(this.engine.Database),this.driver.State.DeepClone());
        }
        public void Dispose(){
            this.engine.Dispose();
            this.store.Dispose();
        }
    }
    /// <summary>子プロセスの起動方法(apphost の exe か、dotnet と dll)。</summary>
    public static (string FileName,IReadOnlyList<string> Prefix) ResolveChildCommand(){
        var processPath=Environment.ProcessPath;
        if(processPath is not null&&Path.GetFileNameWithoutExtension(processPath).Equals("Benchmark.TpcC",StringComparison.OrdinalIgnoreCase)){
            return (processPath,[]);
        }
        var directory=AppContext.BaseDirectory;
        var exe=Path.Combine(directory,"Benchmark.TpcC.exe");
        if(File.Exists(exe)){
            return (exe,[]);
        }
        var dll=Path.Combine(directory,"Benchmark.TpcC.dll");
        if(processPath is not null&&Path.GetFileNameWithoutExtension(processPath).Equals("dotnet",StringComparison.OrdinalIgnoreCase)){
            return (processPath,[dll]);
        }
        return ("dotnet",[dll]);
    }
}
