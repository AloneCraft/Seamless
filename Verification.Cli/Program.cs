using System.Text.Json;
using Benchmark.Dynamic.Core;
using Verification.Core;
using Verification.Infrastructure;

namespace Verification.Cli;

internal static class Program {
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};

    public static async Task<int> Main(string[] args) {
        try {
            var root=Path.GetFullPath(Option(args,"--root")??Directory.GetCurrentDirectory());
            var command=args.FirstOrDefault()??"help";
            switch(command) {
                case "init":
                    VerificationWorkspace.Initialize(root);
                    Console.WriteLine("初期化: "+VerificationWorkspace.GetDatabasePath(root));
                    return 0;
                case "plan":
                    return await WritePlanAsync(root,args).ConfigureAwait(false);
                case "run":
                case "replay":
                case "resume":
                    return await RunAsync(command,root,args).ConfigureAwait(false);
                case "shrink":
                    return await ShrinkAsync(root,args).ConfigureAwait(false);
                case "baseline": {
                    var workspace=OpenWorkspace(root);
                    workspace.SetBaseline(Required(args,"--run"));
                    Console.WriteLine("比較基準: "+workspace.BaselineId);
                    return 0;
                }
                case "list":
                    Console.WriteLine(JsonSerializer.Serialize(OpenWorkspace(root).GetRuns(),Json));
                    return 0;
                case "show": {
                    var workspace=OpenWorkspace(root);
                    var runId=Required(args,"--run");
                    IReadOnlyList<ComparisonResult>? comparison=null;
                    if(workspace.BaselineId is not null&&workspace.BaselineId!=runId) {
                        comparison=workspace.Compare(runId);
                    }
                    Console.WriteLine(JsonSerializer.Serialize(new {
                        Run=workspace.GetRun(runId),
                        Results=workspace.GetResults(runId),
                        Comparison=comparison,
                    },Json));
                    return 0;
                }
                case "compare": {
                    var results=OpenWorkspace(root).Compare(
                        Required(args,"--run"),Option(args,"--baseline"));
                    Console.WriteLine(JsonSerializer.Serialize(results,Json));
                    return results.Count>0&&results.All(static result=>result.Status==ComparisonStatus.Passed)?0:1;
                }
                default:
                    WriteUsage();
                    return command=="help"?0:2;
            }
        } catch(Exception error) {
            Console.Error.WriteLine(error.Message);
            return 2;
        }
    }

    private static async Task<int> WritePlanAsync(string root,string[] args) {
        var plan=CreatePlan(root,args);
        var text=JsonSerializer.Serialize(plan,Json);
        if(Option(args,"--output") is string output) {
            await File.WriteAllTextAsync(output,text).ConfigureAwait(false);
        } else {
            Console.WriteLine(text);
        }
        return 0;
    }

    private static async Task<int> RunAsync(string command,string root,string[] args) {
        var workspace=OpenWorkspace(root);
        VerificationPlan plan;
        string? parent=null;
        if(command=="replay") {
            parent=Required(args,"--run");
            plan=workspace.CreateReplayPlan(parent,Option(args,"--case"));
        } else if(command=="resume") {
            parent=Required(args,"--run");
            plan=workspace.CreateResumePlan(parent);
        } else if(Option(args,"--plan") is string planPath) {
            plan=JsonSerializer.Deserialize<VerificationPlan>(await File.ReadAllTextAsync(planPath).ConfigureAwait(false))
                ??throw new InvalidDataException("計画を読み込めません。");
            if(!Path.GetFullPath(plan.RepositoryRoot).Equals(root,StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("計画のリポジトリが一致しません。");
            }
            if(plan.SourceFingerprint!=RepositoryInspection.Fingerprint(root)) {
                throw new InvalidDataException("計画作成後にソースが変更されています。計画を作り直してください。");
            }
        } else {
            plan=CreatePlan(root,args);
        }
        if(plan.Cases.Length==0){throw new InvalidOperationException("実行対象がありません。");}
        using var cancellation=new CancellationTokenSource();
        ConsoleCancelEventHandler handler=(_,eventArgs)=>{eventArgs.Cancel=true;cancellation.Cancel();};
        Console.CancelKeyPress+=handler;
        try {
            var run=await workspace.RunAsync(plan,new ConsoleProgress(),parent,cancellation.Token).ConfigureAwait(false);
            var results=workspace.GetResults(run.Id);
            IReadOnlyList<ComparisonResult>? comparison=null;
            if(workspace.BaselineId is not null&&workspace.BaselineId!=run.Id) {
                comparison=workspace.Compare(run.Id);
            }
            Console.WriteLine(JsonSerializer.Serialize(new{Run=run,Results=results,Comparison=comparison},Json));
            var comparisonPassed=comparison is null||comparison.Count>0&&comparison.All(static result=>result.Status==ComparisonStatus.Passed);
            return run.Status==CaseStatus.Passed&&comparisonPassed?0:1;
        } finally {
            Console.CancelKeyPress-=handler;
        }
    }

    private static async Task<int> ShrinkAsync(string root,string[] args) {
        var workspace=OpenWorkspace(root);
        var parent=Required(args,"--run");
        var caseId=Required(args,"--case");
        var maximumCandidates=ReadInt(args,"--maximum-candidates",32);
        var timeBudgetMilliseconds=ReadInt(args,"--time-budget-ms",30_000);
        var plan=workspace.CreateShrinkPlan(parent,caseId,new(maximumCandidates,timeBudgetMilliseconds));
        using var cancellation=new CancellationTokenSource();
        ConsoleCancelEventHandler handler=(_,eventArgs)=>{eventArgs.Cancel=true;cancellation.Cancel();};
        Console.CancelKeyPress+=handler;
        try {
            var run=await workspace.RunAsync(plan,new ConsoleProgress(),parent,cancellation.Token).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new{Run=run,Results=workspace.GetResults(run.Id)},Json));
            return run.Status==CaseStatus.Passed?0:1;
        } finally {
            Console.CancelKeyPress-=handler;
        }
    }

    private static VerificationPlan CreatePlan(string root,string[] args) {
        var profile=Option(args,"--profile")??"quick";
        var suite=(Option(args,"--suite")??"changed").ToLowerInvariant();
        if(args.Contains("--all",StringComparer.Ordinal)){suite="all";}
        return suite switch {
            "changed"=>RepositoryInspection.Plan(root,profile),
            "all"=>RepositoryInspection.Plan(root,profile,changedOnly:false),
            "benchmark"=>VerificationProfiles.Benchmark(
                root,profile,Option(args,"--tab")??"System.Array",Option(args,"--type")??"Int32",Option(args,"--method")??"Where_predicate"),
            "sweep"=>VerificationProfiles.Sweep(root,profile),
            "scalable"=>VerificationProfiles.Scalable(root,profile),
            "dynamic"=>VerificationProfiles.Dynamic(
                root,
                profile,
                Option(args,"--seed")??"1",
                ReadInt(args,"--queries",1),
                Enum.Parse<SourceTarget>(Option(args,"--source")??"SystemArray",ignoreCase:true),
                ReadInt(args,"--input-count",256),
                ReadInt(args,"--operators",4),
                ReadInt(args,"--depth",4)),
            _=>throw new ArgumentException("未対応の検証スイートです: "+suite),
        };
    }

    private static VerificationWorkspace OpenWorkspace(string root) {
        var workspace=new VerificationWorkspace(root);
        if(workspace.RecoveredInterruptedRuns>0) {
            Console.Error.WriteLine("完了記録のない実行を中断状態へ回収しました: "+workspace.RecoveredInterruptedRuns);
        }
        return workspace;
    }

    private static int ReadInt(string[] args,string option,int fallback)=>
        int.Parse(Option(args,option)??fallback.ToString(System.Globalization.CultureInfo.InvariantCulture),System.Globalization.CultureInfo.InvariantCulture);

    private static string Required(string[] args,string option)=>
        Option(args,option)??throw new ArgumentException("必須引数: "+option);

    private static string? Option(string[] args,string option) {
        var index=Array.IndexOf(args,option);
        if(index<0){return null;}
        if(index+1>=args.Length||args[index+1].StartsWith("--",StringComparison.Ordinal)) {
            throw new ArgumentException("引数の値がありません: "+option);
        }
        return args[index+1];
    }

    private static void WriteUsage()=>Console.WriteLine(
        "seamless-verify init|plan|run|replay|resume|shrink|baseline|compare|list|show "
        +"[--root <repo>] [--profile quick|full] [--suite changed|all|dynamic|benchmark|sweep|scalable] "
        +"[--output <plan.json>] [--plan <plan.json>] [--run <id>] [--case <id>] [--baseline <id>] "
        +"[--seed <seed>] [--queries <count>] [--source <target>] [--input-count <count>] "
        +"[--operators <count>] [--depth <count>] [--maximum-candidates <count>] [--time-budget-ms <ms>]");

    private sealed class ConsoleProgress:IProgress<VerificationProgress> {
        public void Report(VerificationProgress value)=>
            Console.WriteLine(value.CaseId+" "+value.Status+" "+value.Message);
    }
}
