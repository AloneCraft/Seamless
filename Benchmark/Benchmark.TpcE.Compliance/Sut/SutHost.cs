using System.Diagnostics;
using System.Globalization;
using Benchmark.TpcE.Core;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Compliance;
/// <summary>`sut` コマンドの引数。</summary>
internal sealed record SutArguments{
    /// <summary>ストアのフォルダ(データ側)。無ければ作り、空なら EGen のデータを取り込む。</summary>
    public string StoreDirectory{get;init;}="";
    /// <summary>再実行ログのファイル。耐久性のため、できればデータとは別のドライブに置く。</summary>
    public string RedoPath{get;init;}="";
    /// <summary>EGenLoader が出したフラットファイルのフォルダ(ストアが空のときだけ使う)。</summary>
    public string? FlatDirectory{get;init;}
    public int Customers{get;init;}=1000;
    public int Days{get;init;}=1;
    public int MarketDays{get;init;}=1305;
    public int NewsItemBytes{get;init;}=100_000;
    public long ExpectedRunTrades{get;init;}=500_000;
    public int Port{get;init;}
    /// <summary>EGenHarness.dll(build-egen.ps1 の出力)。取引の手順は EGenTxnHarness に任せる。</summary>
    public string HarnessDll{get;init;}="";
    public long MemoryMegabytes{get;init;}=2048;
    public long DiskGigabytes{get;init;}=64;
    public double CheckpointMinutes{get;init;}=7.5;
    public TpcEScale Scale=>new(){Customers=this.Customers,InitialTradeDays=this.Days,MarketDays=this.MarketDays,NewsItemBytes=this.NewsItemBytes,ExpectedRunTrades=this.ExpectedRunTrades};
    public static SutArguments Parse(IReadOnlyList<string> args){
        var result=new SutArguments();
        var index=0;
        while(index<args.Count){
            var name=args[index++];
            if(index>=args.Count){
                throw new ArgumentException($"{name} needs a value.");
            }
            var value=args[index++];
            result=name switch{
                "--store"=>result with{StoreDirectory=value},
                "--redo"=>result with{RedoPath=value},
                "--flat"=>result with{FlatDirectory=value},
                "--customers"=>result with{Customers=int.Parse(value,CultureInfo.InvariantCulture)},
                "--days"=>result with{Days=int.Parse(value,CultureInfo.InvariantCulture)},
                "--market-days"=>result with{MarketDays=int.Parse(value,CultureInfo.InvariantCulture)},
                "--news-bytes"=>result with{NewsItemBytes=int.Parse(value,CultureInfo.InvariantCulture)},
                "--expected-run-trades"=>result with{ExpectedRunTrades=long.Parse(value,CultureInfo.InvariantCulture)},
                "--port"=>result with{Port=int.Parse(value,CultureInfo.InvariantCulture)},
                "--harness-dll"=>result with{HarnessDll=value},
                "--memory-mb"=>result with{MemoryMegabytes=long.Parse(value,CultureInfo.InvariantCulture)},
                "--disk-gb"=>result with{DiskGigabytes=long.Parse(value,CultureInfo.InvariantCulture)},
                "--checkpoint-minutes"=>result with{CheckpointMinutes=double.Parse(value,CultureInfo.InvariantCulture)},
                _=>throw new ArgumentException($"Unknown option {name}."),
            };
        }
        if(result.StoreDirectory.Length==0){
            throw new ArgumentException("--store is required.");
        }
        if(result.HarnessDll.Length==0){
            throw new ArgumentException("--harness-dll is required (EGenHarness.dll built by build-egen.ps1).");
        }
        if(result.RedoPath.Length==0){
            result=result with{RedoPath=Path.Combine(result.StoreDirectory,"..","redo.log")};
        }
        return result;
    }
}
/// <summary>SUT を起動する: ストアを開く → (空なら)EGen のデータを取り込む → 再実行ログで復旧 → 要求を待つ。</summary>
internal static class SutHost{
    public static int Run(IReadOnlyList<string> args,TextWriter output,CancellationToken stop){
        SutArguments arguments;
        try{
            arguments=SutArguments.Parse(args);
        }catch(ArgumentException error){
            Console.Error.WriteLine(error.Message);
            return 3;
        }
        Directory.CreateDirectory(arguments.StoreDirectory);
        var redoDirectory=Path.GetDirectoryName(Path.GetFullPath(arguments.RedoPath));
        if(redoDirectory is not null){
            Directory.CreateDirectory(redoDirectory);
        }
        var limits=new StoreLimits{MemoryBytes=arguments.MemoryMegabytes*1024*1024,DiskBytes=arguments.DiskGigabytes*1024L*1024*1024};
        using var store=Store.Open(arguments.StoreDirectory,limits);
        var checkpoint=new RedoCheckpoint();
        store.Register(checkpoint);
        var variant=new Benchmark.TpcE.Tsavorite.FLinq.TsavoriteFLinqVariant();
        using var engine=variant.Open(store,arguments.Scale);
        var database=engine.Database;
        void Save(long sequence){
            var watch=Stopwatch.StartNew();
            database.ReleaseHandles();
            store.Save();
            database.AcquireHandles();
            output.WriteLine($"[sut] checkpoint at sequence {sequence:N0} ({watch.Elapsed.TotalSeconds:F1} s)");
        }
        long startSequence;
        long validLength=0;
        string phase;
        if(database.Customer.Count==0){
            if(arguments.FlatDirectory is null){
                Console.Error.WriteLine("The store is empty. Pass --flat <folder written by EGenLoader>.");
                return 3;
            }
            var watch=Stopwatch.StartNew();
            output.WriteLine($"[sut] loading EGen data from {arguments.FlatDirectory}");
            database.BeginBulkLoad();
            var counts=new EGenFlatFileImporter(arguments.FlatDirectory).Import(engine.Loader,(file,rows)=>output.WriteLine($"[sut]   {file}: {rows:N0}"));
            database.EndBulkLoad();
            output.WriteLine($"[sut] loaded {counts.Values.Sum():N0} rows in {watch.Elapsed.TotalSeconds:F1} s");
            if(File.Exists(arguments.RedoPath)){
                File.Delete(arguments.RedoPath);
            }
            checkpoint.LastSequence=0;
            startSequence=0;
            Save(0);
            phase="after-load";
        }else{
            var watch=Stopwatch.StartNew();
            var recovery=RedoLogReader.Replay(database,arguments.RedoPath,checkpoint.LastSequence);
            validLength=recovery.ValidLength;
            startSequence=recovery.LastSequence;
            phase="after-recovery";
            output.WriteLine($"[sut] recovered: checkpoint at {checkpoint.LastSequence:N0}, replayed {recovery.AppliedTransactions:N0} transactions up to {recovery.LastSequence:N0} ({watch.Elapsed.TotalSeconds:F1} s)");
        }
        // 仕様 7.3.3: 初期投入の後と、障害からの復旧の後に、一貫性の 3 条件を点検する。
        var consistencyWatch=Stopwatch.StartNew();
        foreach(var result in ConsistencyChecker.Check(database)){
            var verdict="FAIL";
            if(result.Passed){
                verdict="PASS";
            }
            output.WriteLine($"[sut] consistency {phase} {result.Clause} {verdict} checked={result.Checked} violations={result.Violations} {result.Detail}");
        }
        output.WriteLine($"[sut] consistency checked in {consistencyWatch.Elapsed.TotalSeconds:F1} s");
        using var writer=new RedoLogWriter(arguments.RedoPath,validLength);
        using var harness=new HarnessHost(arguments.HarnessDll);
        var options=new SutOptions{Port=arguments.Port,CheckpointInterval=TimeSpan.FromMinutes(arguments.CheckpointMinutes)};
        using var server=new SutServer(engine,harness,writer,checkpoint,startSequence,Save,options);
        server.Start();
        output.WriteLine($"[sut] READY port {server.Port}");
        output.Flush();
        var report=Stopwatch.StartNew();
        long lastCount=0;
        while(!stop.IsCancellationRequested){
            Thread.Sleep(200);
            if(report.Elapsed>=TimeSpan.FromSeconds(10)){
                var count=server.Sequence;
                output.WriteLine($"[sut] sequence {count:N0} ({(count-lastCount)/report.Elapsed.TotalSeconds:F1}/s) {server.Diagnostics()}");
                lastCount=count;
                report.Restart();
            }
        }
        return 0;
    }
}
