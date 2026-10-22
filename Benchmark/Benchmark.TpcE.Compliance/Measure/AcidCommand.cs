using System.Globalization;
using System.Text;
using System.Text.Json;
using Benchmark.TpcE.Core.Harness;
using System.Text.RegularExpressions;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// `acid`: 仕様 7 章のうち、この環境で実施できる試験を行う。
///   7.2 原子性(Trade-Order の確定と巻き戻し)、7.3 一貫性(初期投入後・復旧後)、7.5.3.1 処理の喪失(運転中に SUT を強制停止 → 再起動)。
/// 実施しない試験(隔離、記憶装置の喪失、電源の喪失、データへのアクセス)は、報告に理由つきで明記する。
/// 終了コード: 0 全試験に合格、1 不合格あり、2 想定外のエラー、3 引数の誤り、4 メモリ、5 EGen のビルドが無い、130 取り消し。
/// </summary>
internal static class AcidCommand{
    public static int Run(IReadOnlyList<string> args,TextWriter output,CancellationToken cancel){
        MeasureArguments arguments;
        try{
            arguments=MeasureArguments.Parse(args);
        }catch(ArgumentException error){
            Console.Error.WriteLine(error.Message);
            return 3;
        }
        var startUsed=SystemMemory.UsedPercent();
        if(startUsed>=MeasureCommand.MemoryStartLimitPercent){
            Console.Error.WriteLine($"Memory use is already {startUsed}% (start limit {MeasureCommand.MemoryStartLimitPercent}%). Close other programs and retry.");
            return 4;
        }
        var loader=Path.Combine(arguments.EGenBuild,"EGenLoader.exe");
        var driver=Path.Combine(arguments.EGenBuild,"EGenDriver.exe");
        if(!File.Exists(loader)||!File.Exists(driver)||!Directory.Exists(arguments.EGenInput)){
            Console.Error.WriteLine($"EGen is not built or its input files are missing. Run build-egen.ps1. Expected {loader}, {driver} and {arguments.EGenInput}.");
            return 5;
        }
        var runDirectory=Path.Combine(arguments.Work,"acid",$"{DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)}-{arguments.Customers}c-{arguments.Days}d");
        Directory.CreateDirectory(runDirectory);
        output.WriteLine($"[acid] {Program.Notice}");
        output.WriteLine($"[acid] run folder: {runDirectory}");
        var tests=new List<CheckResult>();
        ChildProcess? sut=null;
        try{
            var flat=MeasureCommand.EnsureFlatFiles(arguments,loader,output);
            var self=MeasureCommand.ResolveSelf();
            var sutArguments=MeasureCommand.SutArgumentList(arguments,self.Prefix,runDirectory,flat);
            var round=0;
            int port;
            (sut,port)=StartSut(self.File,sutArguments,runDirectory,round,arguments,cancel,output);
            if(sut is null){
                return 2;
            }
            tests.AddRange(ConsistencyReports.FromStartupLines(sut.FindLines("[sut] consistency ")));
            tests.AddRange(Atomicity(port,output));
            for(round=1;round<=arguments.Rounds;round++){
                if(cancel.IsCancellationRequested){
                    return 130;
                }
                if(sut is null){
                    break;
                }
                var result=DurabilityRound(arguments,driver,self,sutArguments,runDirectory,round,sut,port,cancel,output,tests,out var restarted,out var restartedPort);
                sut.Dispose();
                sut=restarted;
                port=restartedPort;
                if(!result){
                    break;
                }
            }
            File.WriteAllText(Path.Combine(runDirectory,"acid-report.md"),Markdown(arguments,tests),new UTF8Encoding(true));
            foreach(var test in tests){
                var mark="PASS";
                if(test.Outcome==CheckOutcome.Fail){
                    mark="FAIL";
                }
                output.WriteLine($"[acid] {mark} {test.Clause} {test.Name}: {test.Detail}");
            }
            output.WriteLine($"[acid] report: {Path.Combine(runDirectory,"acid-report.md")}");
            if(ComplianceChecks.AllPass(tests)){
                return 0;
            }
            return 1;
        }finally{
            sut?.Dispose();
        }
    }
    private static (ChildProcess? Process,int Port) StartSut(string file,List<string> sutArguments,string runDirectory,int round,MeasureArguments arguments,CancellationToken cancel,TextWriter output){
        var logPath=Path.Combine(runDirectory,$"sut-{round}.log");
        output.WriteLine($"[acid] starting the SUT (round {round})");
        var process=new ChildProcess(file,sutArguments,logPath);
        var ready=process.WaitForLine("READY port",TimeSpan.FromMinutes(arguments.LoadTimeoutMinutes),cancel);
        if(ready is null){
            Console.Error.WriteLine($"The SUT did not become ready. See {logPath}.");
            process.Dispose();
            return (null,0);
        }
        var port=int.Parse(ready[(ready.IndexOf("READY port",StringComparison.Ordinal)+"READY port".Length)..].Trim(),CultureInfo.InvariantCulture);
        return (process,port);
    }
    /// <summary>7.2.2: 確定する Trade-Order では TRADE と TRADE_HISTORY に行が入り、巻き戻す Trade-Order では何も残らない。</summary>
    private static IEnumerable<CheckResult> Atomicity(int port,TextWriter output){
        var sample=SutClient.Admin(port,"sample");
        var before=SutClient.Call(port,"{\"admin\":\"trade\",\"trade_id\":1}").GetProperty("trade_count").GetInt64();
        var committed=SutClient.Call(port,OrderBody(sample,rollBack:false));
        var committedId=committed.GetProperty("trade_id").GetInt64();
        var afterCommit=SutClient.Call(port,$"{{\"admin\":\"trade\",\"trade_id\":{committedId}}}");
        var commitOk=committed.GetProperty("ok").GetBoolean()&&!committed.GetProperty("rolled_back").GetBoolean()&&afterCommit.GetProperty("trade_exists").GetBoolean()&&afterCommit.GetProperty("history_rows").GetInt64()>=1&&afterCommit.GetProperty("trade_count").GetInt64()==before+1;
        yield return new CheckResult("7.2.2","原子性: 確定する Trade-Order(roll_it_back=0)",Outcome(commitOk),
            $"取引 {committedId}: TRADE の行 {afterCommit.GetProperty("trade_exists").GetBoolean()}、TRADE_HISTORY の行 {afterCommit.GetProperty("history_rows").GetInt64()}、TRADE の件数 {before} → {afterCommit.GetProperty("trade_count").GetInt64()}");
        var rolled=SutClient.Call(port,OrderBody(sample,rollBack:true));
        var rolledId=rolled.GetProperty("trade_id").GetInt64();
        var afterRollback=SutClient.Call(port,$"{{\"admin\":\"trade\",\"trade_id\":{rolledId}}}");
        var rollbackOk=rolled.GetProperty("ok").GetBoolean()&&rolled.GetProperty("rolled_back").GetBoolean()&&!afterRollback.GetProperty("trade_exists").GetBoolean()&&afterRollback.GetProperty("history_rows").GetInt64()==0&&afterRollback.GetProperty("trade_count").GetInt64()==before+1;
        yield return new CheckResult("7.2.2","原子性: 巻き戻す Trade-Order(roll_it_back=1)",Outcome(rollbackOk),
            $"取引 {rolledId}: TRADE の行 {afterRollback.GetProperty("trade_exists").GetBoolean()}、TRADE_HISTORY の行 {afterRollback.GetProperty("history_rows").GetInt64()}、TRADE の件数 {afterRollback.GetProperty("trade_count").GetInt64()}(増えていない)");
    }
    private static byte[] OrderBody(JsonElement sample,bool rollBack){
        return HarnessRequest.TradeOrder(
            sample.GetProperty("acct_id").GetInt64(),
            sample.GetProperty("exec_f_name").GetString() ?? "",
            sample.GetProperty("exec_l_name").GetString() ?? "",
            sample.GetProperty("exec_tax_id").GetString() ?? "",
            sample.GetProperty("co_name").GetString() ?? "",
            sample.GetProperty("issue").GetString() ?? "",
            sample.GetProperty("symbol").GetString() ?? "",
            25.00,
            "TMB",
            rollBack);
    }
    /// <summary>
    /// 7.5.3.1 処理の喪失: 運転側を最大速度で流している最中に SUT を強制停止し、同じフォルダで再起動する。
    /// 運転側が応答を受けた最後の「確定済みの書き込みの番号」以上が復旧後に残っていれば、応答済みの取引は 1 件も失われていない。
    /// プロセスの強制停止は電源断ではない(OS の書き込みキャッシュは残る)ので、fsync の漏れは検出できない。報告にそう明記する。
    /// </summary>
    private static bool DurabilityRound(MeasureArguments arguments,string driver,(string File,string[] Prefix) self,List<string> sutArguments,string runDirectory,int round,ChildProcess sut,int port,CancellationToken cancel,TextWriter output,List<CheckResult> tests,out ChildProcess? restarted,out int restartedPort){
        restarted=null;
        restartedPort=0;
        var resultPath=Path.Combine(runDirectory,$"driver-result-{round}.json");
        var driverArguments=new List<string>{
            "--port",port.ToString(CultureInfo.InvariantCulture),
            "--input",arguments.EGenInput+Path.DirectorySeparatorChar,
            "--customers",arguments.Customers.ToString(CultureInfo.InvariantCulture),
            "--days",arguments.Days.ToString(CultureInfo.InvariantCulture),
            "--users",arguments.Users.ToString(CultureInfo.InvariantCulture),
            "--ramp","1","--measure","600","--down","1",
            "--tpse","0",
            "--dm-interval","5",
            "--out",resultPath,
        };
        if(round>1){
            driverArguments.Add("--no-cleanup");
        }
        output.WriteLine($"[acid] durability round {round}: running the driver at full speed, then killing the SUT after {arguments.KillAfterSeconds:F0} s");
        using(var driverProcess=new ChildProcess(driver,driverArguments,Path.Combine(runDirectory,$"driver-{round}.log"),runDirectory)){
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(watch.Elapsed.TotalSeconds<arguments.KillAfterSeconds&&!driverProcess.HasExited&&!cancel.IsCancellationRequested){
                Thread.Sleep(200);
            }
            sut.Kill();
            driverProcess.WaitForExit(TimeSpan.FromSeconds(120));
            if(!driverProcess.HasExited){
                driverProcess.Kill();
            }
        }
        if(!File.Exists(resultPath)){
            tests.Add(new CheckResult("7.5.3.1",$"処理の喪失 第{round}回",CheckOutcome.Fail,"運転側が結果を残さなかった"));
            return false;
        }
        var result=DriverResult.Parse(File.ReadAllText(resultPath));
        (var restartedProcess,var newPort)=StartSut(self.File,sutArguments,runDirectory,round,arguments,cancel,output);
        restarted=restartedProcess;
        restartedPort=newPort;
        if(restartedProcess is null){
            tests.Add(new CheckResult("7.5.3.1",$"処理の喪失 第{round}回",CheckOutcome.Fail,"強制停止のあと、SUT が起動し直せなかった"));
            return false;
        }
        var recovered=RecoveredSequence(restartedProcess);
        var acked=result.LastAckedWriteSequence;
        if(recovered is null){
            tests.Add(new CheckResult("7.5.3.1",$"処理の喪失 第{round}回",CheckOutcome.Fail,"復旧のログ行が見つからない"));
            return false;
        }
        var lost=Math.Max(0,acked-recovered.Value);
        var detail=$"応答済みの最後の書き込み番号 {acked:N0}、復旧後に残っている最後の番号 {recovered.Value:N0}、失われた応答済みの書き込み {lost:N0} 件";
        tests.Add(new CheckResult("7.5.3.1",$"処理の喪失 第{round}回: 応答済みの取引が残っている",Outcome(lost==0&&acked>0),detail));
        foreach(var check in ConsistencyReports.FromStartupLines(restartedProcess.FindLines("[sut] consistency after-recovery "))){
            tests.Add(check with{Name=check.Name+$" 第{round}回"});
        }
        return true;
    }
    /// <summary>SUT の復旧ログ行 `[sut] recovered: checkpoint at X, replayed N transactions up to Y (...)` の Y。</summary>
    internal static long? RecoveredSequence(ChildProcess process){
        var line=process.FindLine("[sut] recovered:");
        if(line is null){
            return null;
        }
        var match=Regex.Match(line,@"up to ([\d,]+)");
        if(!match.Success){
            return null;
        }
        return long.Parse(match.Groups[1].Value.Replace(",",""),CultureInfo.InvariantCulture);
    }
    private static CheckOutcome Outcome(bool passed){
        if(passed){
            return CheckOutcome.Pass;
        }
        return CheckOutcome.Fail;
    }
    private static string Markdown(MeasureArguments arguments,IReadOnlyList<CheckResult> tests){
        var builder=new StringBuilder();
        builder.AppendLine("# TPC-E 準拠リハーサル: ACID 試験の結果");
        builder.AppendLine();
        builder.AppendLine($"**{Program.Notice}**");
        builder.AppendLine();
        builder.AppendLine($"顧客 {arguments.Customers:N0} 人、初期取引 {arguments.Days} 日、ユーザー {arguments.Users}、処理の喪失の試験 {arguments.Rounds} 回。監査人の立ち会いは無い自己試験です。");
        builder.AppendLine();
        builder.AppendLine("## 実施した試験");
        builder.AppendLine();
        builder.AppendLine("| 条項 | 試験 | 結果 | 内容 |");
        builder.AppendLine("|---|---|---|---|");
        foreach(var test in tests){
            var mark="合格";
            if(test.Outcome==CheckOutcome.Fail){
                mark="**不合格**";
            }
            builder.AppendLine($"| {test.Clause} | {test.Name} | {mark} | {test.Detail} |");
        }
        builder.AppendLine();
        builder.AppendLine("## 実施していない試験とその理由");
        builder.AppendLine();
        builder.AppendLine("| 条項 | 試験 | 理由 |");
        builder.AppendLine("|---|---|---|");
        builder.AppendLine("| 7.4.2 | 隔離(P0〜P3) | SUT は取引を 1 本の作業スレッドで直列に実行する設計です(利用者が決めた意図的な判断)。取引どうしが重ならないので、どの現象(P0〜P3)も起こり得ません(7.4.1.7 の代替技法)。仕様の手順(S3 がフレームの間で止まり S4 が割り込む)は実行できません。7.4.1.4『任意の取引の同時実行を許す』を満たすかは、監査人と協議すべき事項です。 |");
        builder.AppendLine("| 7.5.3.2 | 記憶装置 1 台の喪失 | 再実行ログとデータを別の物理ドライブに置いてミラーを組む構成が、この PC にありません。 |");
        builder.AppendLine("| 7.5.3.3 | 外部電源の喪失 | PC の電源を切る試験は危険なので自動では行いません。利用者が手で行う必要があります。処理の喪失の試験(プロセスの強制停止)は、OS の書き込みキャッシュが残るため、fsync の漏れを検出できません。 |");
        builder.AppendLine("| 7.6 | データへのアクセス | ドライブ 1 台の永久的な故障を作れないため。 |");
        builder.AppendLine("| 7.5.6 | 復旧時間の報告 | 測定区間での障害注入と、チェックポイントの完了時刻の記録が未実装です。 |");
        return builder.ToString();
    }
}
