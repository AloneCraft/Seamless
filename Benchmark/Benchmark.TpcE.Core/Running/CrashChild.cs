using System.Diagnostics;
using System.Globalization;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
/// <summary>強制終了の止め方。</summary>
public enum KillMode{
    /// <summary>止めない(最後まで流す)。</summary>
    None,
    /// <summary>K1: n 回目の保存の直前。</summary>
    BeforeSave,
    /// <summary>K2: n 回目の保存で、最初の子資源(表より前)の Save の中。</summary>
    InFirstSave,
    /// <summary>K3: n 回目の保存で、最後の子資源の Flush の中(全表を書き出し済み、マニフェストはまだ)。</summary>
    InLastFlush,
    /// <summary>K4: n 回目の保存で、マニフェストを書いた後の最初の SaveCommitted の中。</summary>
    AfterManifest,
    /// <summary>K5: n 回目の保存が戻った直後。</summary>
    AfterSave,
    /// <summary>K6: 通し番号 n の取引の m 回目の書き込みの後。</summary>
    MidTransaction,
    /// <summary>K7: 親が時間で止める(子は普通に流す)。</summary>
    Timed,
}
/// <summary>
/// 強制終了の検査の子プロセス。1 つの組み合わせで、保存済みの続きから取引を流し、決めた点で自分を強制終了する。
/// 標準出力に印を出す: @@SAVE_BEGIN 通し番号 / @@SAVE_DONE 通し番号 / @@KILL 止め方 / @@END 通し番号。
/// </summary>
public static class CrashChild{
    public sealed record Arguments(RunOptions Options,string StoreDirectory,string Variant,KillMode Kill,long KillArgument,int KillWrite);
    public static int Run(Arguments arguments,ITpcEVariant variant,TextWriter output){
        var options=arguments.Options;
        Driver? driver=null;
        DriverState? restored=null;
        var limits=new StoreLimits{MemoryBytes=TpcESizing.DefaultMemoryBytes(options.Scale),DiskBytes=options.DiskGigabytes*1024L*1024*1024};
        using var store=Store.Open(arguments.StoreDirectory,limits);
        store.Register(new DriverStateComponent(()=>driver!.State,state=>restored=state));
        var probes=new KillProbes(arguments,output);
        store.Register(probes.First);
        using var engine=variant.Open(store,options.Scale);
        store.Register(probes.Last);
        var saves=0;
        void Save(){
            saves++;
            probes.CurrentSave=saves;
            if(arguments.Kill==KillMode.BeforeSave&&arguments.KillArgument==saves){
                Die(output,"BeforeSave");
            }
            output.WriteLine($"@@SAVE_BEGIN {driver!.State.TxnIndex}");
            output.Flush();
            store.Save();
            output.WriteLine($"@@SAVE_DONE {driver.State.TxnIndex}");
            output.Flush();
            if(arguments.Kill==KillMode.AfterSave&&arguments.KillArgument==saves){
                Die(output,"AfterSave");
            }
        }
        if(restored is null){
            engine.Database.BeginBulkLoad();
            var generation=new TpcEDataGenerator(new TpcEModel(options.Seed,options.Scale)).Generate(engine.Loader);
            engine.Database.EndBulkLoad();
            driver=new Driver(Driver.Initial(options.Seed,options.Scale,generation));
            // 初期投入の保存は数えない(どの止め方でも、まず投入済みの保存を作る)。
            probes.Disarmed=true;
            output.WriteLine($"@@SAVE_BEGIN {driver.State.TxnIndex}");
            store.Save();
            output.WriteLine($"@@SAVE_DONE {driver.State.TxnIndex}");
            output.Flush();
            probes.Disarmed=false;
        }else{
            driver=new Driver(restored);
        }
        var recorder=new KillOnWrite(arguments,output);
        engine.Database.Log.Observer=recorder;
        var savedAt=driver.State.CeCount;
        // 子の --transactions は「続きから流す CE の件数」。
        var target=driver.State.CeCount+options.Transactions;
        while(driver.State.CeCount<target){
            var request=driver.Next();
            recorder.TxnIndex=driver.State.TxnIndex;
            var result=engine.Execute(request);
            driver.Completed(request,result);
            if(options.SaveEvery>0&&driver.State.CeCount-savedAt>=options.SaveEvery){
                savedAt=driver.State.CeCount;
                Save();
            }
        }
        output.WriteLine($"@@END {driver.State.TxnIndex}");
        output.Flush();
        return 0;
    }
    internal static void Die(TextWriter output,string mode){
        output.WriteLine($"@@KILL {mode}");
        output.Flush();
        Process.GetCurrentProcess().Kill();
        // Kill は戻らないことがある(非同期)。戻っても先へ進ませない。
        Thread.Sleep(Timeout.Infinite);
    }
    /// <summary>保存の各段階に差し込む探りの子資源(最初と最後に登録する)。</summary>
    private sealed class KillProbes(Arguments arguments,TextWriter output){
        public int CurrentSave{get;set;}
        public bool Disarmed{get;set;}
        public Probe First=>this.first??=new(this,"~probe-first",true);
        public Probe Last=>this.last??=new(this,"~probe-last",false);
        private Probe? first;
        private Probe? last;
        private bool Armed(KillMode mode)=>!this.Disarmed&&arguments.Kill==mode&&arguments.KillArgument==this.CurrentSave;
        public sealed class Probe(KillProbes owner,string name,bool isFirst):IStoreComponent{
            public string Name=>name;
            public string Save(){
                if(isFirst&&owner.Armed(KillMode.InFirstSave)){
                    Die(owner.Output,"InFirstSave");
                }
                return "";
            }
            public void Flush(){
                if(!isFirst&&owner.Armed(KillMode.InLastFlush)){
                    Die(owner.Output,"InLastFlush");
                }
            }
            public void SaveCommitted(){
                if(isFirst&&owner.Armed(KillMode.AfterManifest)){
                    Die(owner.Output,"AfterManifest");
                }
            }
            public void Load(string state){
            }
            public void Dispose(){
            }
        }
        private TextWriter Output=>output;
    }
    private sealed class KillOnWrite(Arguments arguments,TextWriter output):IWriteObserver{
        public long TxnIndex{get;set;}
        private int writes;
        private long counted=-1;
        public void OnWrite(int tableId,WriteKind kind,object key,object? row){
            if(arguments.Kill!=KillMode.MidTransaction||this.TxnIndex!=arguments.KillArgument){
                return;
            }
            if(this.counted!=this.TxnIndex){
                this.counted=this.TxnIndex;
                this.writes=0;
            }
            this.writes++;
            if(this.writes>=arguments.KillWrite){
                Die(output,"MidTransaction");
            }
        }
    }
    public static IReadOnlyList<string> ToArguments(Arguments arguments){
        var result=new List<string>(arguments.Options.ToArguments());
        result[0]="crash-child";
        result.AddRange(["--store",arguments.StoreDirectory,"--variant",arguments.Variant,"--kill",arguments.Kill.ToString(),"--kill-at",arguments.KillArgument.ToString(CultureInfo.InvariantCulture),"--kill-write",arguments.KillWrite.ToString(CultureInfo.InvariantCulture)]);
        return result;
    }
    /// <summary>crash-child の引数を解析する(共通の引数は RunOptions へ渡す)。</summary>
    public static Arguments Parse(IReadOnlyList<string> args){
        var common=new List<string>();
        string? storeDirectory=null;
        string? variant=null;
        var kill=KillMode.None;
        long killAt=0;
        var killWrite=1;
        for(var index=0;index<args.Count;index++){
            switch(args[index]){
                case "--store":
                    storeDirectory=args[++index];
                    break;
                case "--variant":
                    variant=args[++index];
                    break;
                case "--kill":
                    kill=Enum.Parse<KillMode>(args[++index]);
                    break;
                case "--kill-at":
                    killAt=long.Parse(args[++index],CultureInfo.InvariantCulture);
                    break;
                case "--kill-write":
                    killWrite=int.Parse(args[++index],CultureInfo.InvariantCulture);
                    break;
                default:
                    common.Add(args[index]);
                    break;
            }
        }
        if(storeDirectory is null||variant is null){
            throw new ArgumentException("crash-child needs --store and --variant.");
        }
        return new(RunOptions.Parse(common),storeDirectory,variant,kill,killAt,killWrite);
    }
}
