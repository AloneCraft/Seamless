using System.Diagnostics;
using System.Globalization;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// `measure`: EGenLoader でデータを作り、SUT と運転側を別プロセスで起動して、立ち上がり → 計測区間 → 片付けを流し、結果を仕様の規則で点検する。
/// 終了コード: 0 全点検に合格、1 不合格あり、2 想定外のエラー、3 引数の誤り、4 メモリ使用率の上限、5 EGen のビルドが無い、130 取り消し。
/// </summary>
internal static class MeasureCommand{
    /// <summary>実行中に PC 全体のメモリ使用率がこれに達したら両方止める。</summary>
    public const int MemoryStopPercent=90;
    /// <summary>上の使用率がこの時間続いたら止める。</summary>
    public static readonly TimeSpan MemoryStopAfter=TimeSpan.FromSeconds(30);
    /// <summary>開始時にこれ以上なら始めない(途中で止まる実行を避ける)。</summary>
    public const int MemoryStartLimitPercent=80;
    public static int Run(IReadOnlyList<string> args,TextWriter output,CancellationToken cancel){
        MeasureArguments arguments;
        try{
            arguments=MeasureArguments.Parse(args);
        }catch(ArgumentException error){
            Console.Error.WriteLine(error.Message);
            return 3;
        }
        var startUsed=SystemMemory.UsedPercent();
        if(startUsed>=MemoryStartLimitPercent){
            Console.Error.WriteLine($"Memory use is already {startUsed}% (start limit {MemoryStartLimitPercent}%). Close other programs and retry.");
            return 4;
        }
        var loader=Path.Combine(arguments.EGenBuild,"EGenLoader.exe");
        var driver=Path.Combine(arguments.EGenBuild,"EGenDriver.exe");
        if(!File.Exists(loader)||!File.Exists(driver)||!Directory.Exists(arguments.EGenInput)){
            Console.Error.WriteLine($"EGen is not built or its input files are missing. Run build-egen.ps1. Expected {loader}, {driver} and {arguments.EGenInput}.");
            return 5;
        }
        var stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
        var runDirectory=Path.Combine(arguments.Work,"runs",$"{stamp}-{arguments.Customers}c-{arguments.Days}d");
        Directory.CreateDirectory(runDirectory);
        output.WriteLine($"[measure] {Program.Notice}");
        output.WriteLine($"[measure] run folder: {runDirectory}");
        ChildProcess? sut=null;
        ChildProcess? driverProcess=null;
        try{
            var flat=EnsureFlatFiles(arguments,loader,output);
            var self=ResolveSelf();
            var sutArguments=SutArgumentList(arguments,self.Prefix,runDirectory,flat);
            output.WriteLine("[measure] starting the SUT (loads the EGen data)");
            sut=new ChildProcess(self.File,sutArguments,Path.Combine(runDirectory,"sut.log"));
            var ready=sut.WaitForLine("READY port",TimeSpan.FromMinutes(arguments.LoadTimeoutMinutes),cancel);
            if(cancel.IsCancellationRequested){
                return 130;
            }
            if(ready is null){
                Console.Error.WriteLine($"The SUT did not become ready. See {Path.Combine(runDirectory,"sut.log")}.");
                return 2;
            }
            var port=int.Parse(ready[(ready.IndexOf("READY port",StringComparison.Ordinal)+"READY port".Length)..].Trim(),CultureInfo.InvariantCulture);
            output.WriteLine($"[measure] SUT ready on port {port}");
            var resultPath=Path.Combine(runDirectory,"driver-result.json");
            var driverArguments=new List<string>{
                "--port",port.ToString(CultureInfo.InvariantCulture),
                "--input",arguments.EGenInput+Path.DirectorySeparatorChar,
                "--customers",arguments.Customers.ToString(CultureInfo.InvariantCulture),
                "--days",arguments.Days.ToString(CultureInfo.InvariantCulture),
                "--users",arguments.Users.ToString(CultureInfo.InvariantCulture),
                "--ramp",Seconds(arguments.RampMinutes),
                "--measure",Seconds(arguments.MeasureMinutes),
                "--down",Seconds(arguments.DownMinutes),
                "--tpse",arguments.EffectiveTargetTpse().ToString("F4",CultureInfo.InvariantCulture),
                "--dm-interval",arguments.DataMaintenanceSeconds.ToString(CultureInfo.InvariantCulture),
                "--out",resultPath,
            };
            output.WriteLine($"[measure] starting the driver: {arguments.Users} users, target {arguments.EffectiveTargetTpse():F3} tpsE, measure {arguments.MeasureMinutes:F1} min");
            driverProcess=new ChildProcess(driver,driverArguments,Path.Combine(runDirectory,"driver.log"),runDirectory);
            var stop=WaitForDriver(driverProcess,sut,output,cancel);
            if(stop!=0){
                return stop;
            }
            if(!File.Exists(resultPath)){
                Console.Error.WriteLine($"The driver finished without a result. See {Path.Combine(runDirectory,"driver.log")}.");
                return 2;
            }
            var result=DriverResult.Parse(File.ReadAllText(resultPath));
            var settings=new MeasureSettings(arguments.CheckpointMinutes);
            var checks=new List<CheckResult>(ComplianceChecks.Evaluate(result,settings));
            // 仕様 7.3.3: 初期投入の後(SUT 起動時のログ)と、計測の後(SUT に問い合わせる)に、一貫性の 3 条件を点検する。
            checks.Add(EGenLogChecks.Scan(Path.Combine(runDirectory,"EGenDriver.log")));
            checks.Add(EGenValidateCheck.Run(result,arguments.EGenBuild,TimeSpan.FromMinutes(30)));
            checks.AddRange(ConsistencyReports.FromStartupLines(sut.FindLines("[sut] consistency ")));
            checks.AddRange(ConsistencyReports.FromAdminResponse(ConsistencyReports.Ask(port,"consistency"),"計測後"));
            var title=$"TPC-E 準拠リハーサルの結果({arguments.Customers:N0} 顧客・初期取引 {arguments.Days} 日)";
            File.WriteAllText(Path.Combine(runDirectory,"report.md"),ReportWriter.Markdown(result,checks,settings,title),new System.Text.UTF8Encoding(true));
            File.WriteAllText(Path.Combine(runDirectory,"report.json"),ReportWriter.Json(result,checks,settings),new System.Text.UTF8Encoding(false));
            Print(result,checks,output);
            output.WriteLine($"[measure] report: {Path.Combine(runDirectory,"report.md")}");
            if(ComplianceChecks.AllPass(checks)){
                return 0;
            }
            return 1;
        }finally{
            driverProcess?.Dispose();
            sut?.Dispose();
        }
    }
    internal static string Seconds(double minutes)=>Math.Max(1,(int)Math.Round(minutes*60)).ToString(CultureInfo.InvariantCulture);
    /// <summary>`sut` コマンドの引数。ストアと再実行ログは実行フォルダの下に置く(同じフォルダで起動し直すと、復旧の経路を通る)。</summary>
    internal static List<string> SutArgumentList(MeasureArguments arguments,string[] prefix,string runDirectory,string flat){
        return new List<string>(prefix){
            "sut",
            "--store",Path.Combine(runDirectory,"store"),
            "--redo",Path.Combine(runDirectory,"redo","redo.log"),
            "--flat",flat,
            "--harness-dll",Path.Combine(arguments.EGenBuild,"EGenHarness.dll"),
            "--customers",arguments.Customers.ToString(CultureInfo.InvariantCulture),
            "--days",arguments.Days.ToString(CultureInfo.InvariantCulture),
            "--news-bytes",arguments.NewsItemBytes.ToString(CultureInfo.InvariantCulture),
            "--memory-mb",arguments.MemoryMegabytes.ToString(CultureInfo.InvariantCulture),
            "--disk-gb",arguments.DiskGigabytes.ToString(CultureInfo.InvariantCulture),
            "--checkpoint-minutes",arguments.CheckpointMinutes.ToString(CultureInfo.InvariantCulture),
        };
    }
    internal static (string File,string[] Prefix) ResolveSelf(){
        var processPath=Environment.ProcessPath ?? throw new InvalidOperationException("The process path is unknown.");
        if(string.Equals(Path.GetFileNameWithoutExtension(processPath),"dotnet",StringComparison.OrdinalIgnoreCase)){
            return (processPath,[typeof(Program).Assembly.Location]);
        }
        return (processPath,[]);
    }
    /// <summary>EGenLoader の出力が無ければ作る。顧客数と日数ごとに作業フォルダに残して使い回す。</summary>
    internal static string EnsureFlatFiles(MeasureArguments arguments,string loader,TextWriter output){
        var flat=Path.Combine(arguments.Work,$"flat-{arguments.Customers}c-{arguments.Days}d");
        var marker=Path.Combine(flat,".complete");
        if(File.Exists(marker)){
            output.WriteLine($"[measure] reusing EGen data in {flat}");
            return flat;
        }
        if(Directory.Exists(flat)){
            Directory.Delete(flat,true);
        }
        Directory.CreateDirectory(flat);
        output.WriteLine($"[measure] generating EGen data ({arguments.Customers:N0} customers, {arguments.Days} day(s))");
        var watch=Stopwatch.StartNew();
        var count=arguments.Customers.ToString(CultureInfo.InvariantCulture);
        var start=new ProcessStartInfo(loader){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        foreach(var argument in new[]{"-b","1","-c",count,"-t",count,"-f",ComplianceSpec.CustomersPerTpse.ToString(CultureInfo.InvariantCulture),"-w",arguments.Days.ToString(CultureInfo.InvariantCulture),"-i",arguments.EGenInput+Path.DirectorySeparatorChar,"-o",flat+Path.DirectorySeparatorChar,"-x"}){
            start.ArgumentList.Add(argument);
        }
        using(var process=Process.Start(start)!){
            process.OutputDataReceived+=static (_,_)=>{};
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            if(process.ExitCode!=0){
                throw new InvalidOperationException($"EGenLoader failed with exit code {process.ExitCode}.");
            }
        }
        File.WriteAllText(marker,DateTime.Now.ToString("O",CultureInfo.InvariantCulture));
        output.WriteLine($"[measure] generated in {watch.Elapsed.TotalSeconds:F1} s");
        return flat;
    }
    /// <summary>運転側が終わるのを待つ。メモリ使用率が上限に達したら両方止める。</summary>
    private static int WaitForDriver(ChildProcess driver,ChildProcess sut,TextWriter output,CancellationToken cancel){
        var report=Stopwatch.StartNew();
        Stopwatch? highSince=null;
        while(!driver.HasExited){
            if(cancel.IsCancellationRequested){
                output.WriteLine("[measure] cancelled");
                return 130;
            }
            if(sut.HasExited){
                Console.Error.WriteLine("The SUT exited while the driver was running.");
                return 2;
            }
            var used=SystemMemory.UsedPercent();
            if(used>=MemoryStopPercent){
                // 一瞬の急上昇(他のプログラムのビルドなど)では止めず、続いたときだけ止める。
                if(highSince is null){
                    highSince=Stopwatch.StartNew();
                }
                if(highSince.Elapsed>=MemoryStopAfter){
                    Console.Error.WriteLine($"Memory use stayed at {used}% for {highSince.Elapsed.TotalSeconds:F0} s. Stopping the run (limit {MemoryStopPercent}%).");
                    return 4;
                }
            }else{
                highSince=null;
            }
            if(report.Elapsed>=TimeSpan.FromMinutes(1)){
                output.WriteLine($"[measure] running... memory {used}% (SUT {sut.WorkingSetBytes/1024.0/1024/1024:F2} GB, driver {driver.WorkingSetBytes/1024.0/1024/1024:F2} GB)");
                report.Restart();
            }
            Thread.Sleep(500);
        }
        if(driver.ExitCode!=0){
            Console.Error.WriteLine($"The driver exited with code {driver.ExitCode}.");
            return 2;
        }
        return 0;
    }
    private static void Print(DriverResult result,IReadOnlyList<CheckResult> checks,TextWriter output){
        output.WriteLine($"[measure] tpsE(unaudited) {result.TpsE:F3} (nominal {ComplianceSpec.NominalTpse(result.Customers):F3}) over {result.MeasureSeconds:F0} s");
        foreach(var check in checks){
            if(check.Outcome==CheckOutcome.Fail){
                output.WriteLine($"[measure] FAIL {check.Clause} {check.Name}: {check.Detail}");
            }
        }
        if(ComplianceChecks.AllPass(checks)){
            output.WriteLine("[measure] all checks passed");
        }
    }
}
