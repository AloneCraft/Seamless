using Benchmark.Dynamic.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

public interface IWorkerExecutionRunner{
    Task<WorkerSourceExecutionCompletedEvent> ExecuteAsync(
        WorkerRunRequest 要求,
        QueryIrDocument Query,
        int QueryIndex,
        SourceTarget 対象,
        int Source順序,
        CancellationToken キャンセルToken);
}

public sealed record WorkerExecutionArtifact(
    long SourceEmissionNanoseconds,
    DynamicCompilationTimings CompilationTimings,
    BalancedMeasurementResult? Measurement,
    int RewriteCount,
    bool ReachedFixedPoint);

public sealed class DynamicWorkerExecutionRunner:IWorkerExecutionRunner{
    private static readonly Lazy<string> ビルド指紋=new(static ()=>Benchmark.Infrastructure.BuildFingerprint.Capture(typeof(DynamicWorkerExecutionRunner).Assembly.Location));
    private static readonly DynamicImplementationKind[] 検証対象=[
        DynamicImplementationKind.System,
        DynamicImplementationKind.FLinqUnoptimized,
        DynamicImplementationKind.FLinq,
        DynamicImplementationKind.ZLinq,
    ];
    private readonly QuerySourceEmitter Source生成;
    private readonly DynamicQueryCompiler コンパイラ;
    private readonly SourceFixtureFactory Fixture生成;

    public DynamicWorkerExecutionRunner(
        QuerySourceEmitter? Source生成=null,
        DynamicQueryCompiler? コンパイラ=null,
        SourceFixtureFactory? Fixture生成=null){
        this.Source生成=Source生成??new QuerySourceEmitter();
        this.コンパイラ=コンパイラ??new DynamicQueryCompiler();
        this.Fixture生成=Fixture生成??new SourceFixtureFactory();
    }

    public Task<WorkerSourceExecutionCompletedEvent> ExecuteAsync(
        WorkerRunRequest 要求,
        QueryIrDocument Query,
        int QueryIndex,
        SourceTarget 対象,
        int Source順序,
        CancellationToken キャンセルToken){
        return Task.FromResult(this.実行(要求,Query,QueryIndex,対象,Source順序,キャンセルToken));
    }

    private WorkerSourceExecutionCompletedEvent 実行(
        WorkerRunRequest 要求,
        QueryIrDocument Query,
        int QueryIndex,
        SourceTarget 対象,
        int Source順序,
        CancellationToken キャンセルToken){
        using var 計測排他=Benchmark.Infrastructure.MeasurementLease.Acquire(キャンセルToken);
        var 完了日時=DateTimeOffset.UtcNow;
        var Status=WorkerExecutionStatus.ExecutionFailed;
        var 保存方式=対象.GetSourceKind().ToString();
        if(対象.GetSourceKind() is SourceKind.Persisted or SourceKind.Tsavorite){保存方式=(要求.StorageSettings??new()).Kind;}
        var SourceText=string.Empty;
        var SourceHash=string.Empty;
        string? ResultJSON=null;
        string? 診断JSON=null;
        string? TraceJSON=null;
        var 展開コード=string.Empty;
        string? 計測JSON=null;
        string? 入力ハッシュ=null;
        string? 環境JSON=null;
        string? パラメーターJSON=null;
        string? 拡張JSON=null;
        string? 設定変更JSON=null;
        var 入出力方式="BackendDefault";
        var 解放済み=true;
        var Source生成時間=0L;
        DynamicCompilationTimings Compilation時間=空のコンパイル時間();
        LoadedDynamicQuery? Loaded=null;
        SourceFixture? Fixture=null;
        try{
            キャンセルToken.ThrowIfCancellationRequested();
            Benchmark.Infrastructure.SystemResources.Check(starting:true);
            var 開始=Stopwatch.GetTimestamp();
            var Source=this.Source生成.Emit(Query,new QuerySourceEmissionOptions{
                GeneratedNamespace="Benchmark.Dynamic.Generated",
                ClassName=$"Q_{安全な識別子(要求.RunId)}_{QueryIndex:D6}_{(int)対象}_{SourceHash短縮(Query)}",
                OptimizationFlags=要求.OptimizationFlags,
            });
            Source生成時間=経過ナノ秒(開始);
            SourceText=Source.SourceText;
            SourceHash=Source.SourceHash;
            var Compilation=this.コンパイラ.Compile(
                new DynamicCompilationRequest(
                    $"Benchmark.Dynamic.Generated.{安全な識別子(要求.RunId)}.{QueryIndex}.{(int)対象}.{SourceHash[..12]}",
                    Source.QualifiedTypeName,
                    Source.SourceText,
                    $"{Source.ClassName}.g.cs"),
                キャンセルToken);
            Compilation時間=Compilation.Timings;
            診断JSON=JsonSerializer.Serialize(Compilation.Diagnostics,DynamicWorkerProtocol.JsonOptions);
            TraceJSON=TraceをJSON化(Compilation.OptimizationTracePayloads);
            展開コード=Compilation.ExpandedSource;
            var Trace概要=Traceを集計(Compilation.OptimizationTracePayloads);
            if(!Compilation.Success||Compilation.LoadedQuery is null){
                Status=失敗Status(Compilation.Diagnostics,Trace概要.NonConverged);
                計測JSON=ArtifactをJSON化(Source生成時間,Compilation時間,null,Trace概要);
                goto 完了;
            }
            Loaded=Compilation.LoadedQuery;
            if(Trace概要.NonConverged){
                Status=WorkerExecutionStatus.OptimizationNonConverged;
                計測JSON=ArtifactをJSON化(Source生成時間,Compilation時間,null,Trace概要);
                goto 完了;
            }
            var FixtureSeed=要求.FixtureSeedOverride??SeedDerivation.Derive(要求.BaseSeed,"fixture",QueryIndex);
            Fixture=this.Fixture生成.Create(対象,要求.InputElementCount,FixtureSeed,Query.ElementType,要求.IncludeSpecialValues,要求.StorageSettings,QueryWorkloadIdentity.Compute(Query));
            入力ハッシュ=FixtureInputIdentity.Compute(Fixture,Query.ElementType);
            環境JSON=JsonSerializer.Serialize(Benchmark.Infrastructure.EnvironmentSnapshot.Capture(要求.MeasurementProfile.ProfileId));
            var 所有者=Fixture.Stores.FirstOrDefault();
            if(所有者 is not null){
                var 採用設定=所有者.ResolvedProfiles.Values.First();
                var 実設定=所有者.GetCurrentSettings(所有者.ResolvedProfiles.Keys.First());
                パラメーターJSON=JsonSerializer.Serialize(new Benchmark.Infrastructure.ProfileParameters(WorkerStorage.Requested(要求.StorageSettings??new()),実設定,採用設定.ProfileID??"Minimum",採用設定.Reason,採用設定.ProfileVersion,採用設定.EvidenceRunIDs,採用設定.Requested.ToString(),採用設定.Settings));
                拡張JSON=JsonSerializer.Serialize(所有者.GetGrowth(所有者.ResolvedProfiles.Keys.First()).Descriptor);
                入出力方式=global::Seamless.Execution.ProfileIO.Describe(WorkerStorage.Storage(要求.StorageSettings??new()));
            }else{
                パラメーターJSON="{}";拡張JSON="{}";
            }
            設定変更JSON=JsonSerializer.Serialize(WorkerStorage.Changes(Fixture,要求.RunId,QueryIndex));
            var 検証=検証する(Loaded,Fixture.Primary,Fixture.Secondary,キャンセルToken);
            var 結果一覧=検証.Results;
            var 例外一覧=検証.Exceptions;
            ResultJSON=JsonSerializer.Serialize(
                new WorkerValidationArtifact(
                    結果一覧,
                    例外一覧,
                    new Dictionary<DynamicImplementationKind,DynamicQueryComparison>()),
                DynamicWorkerProtocol.JsonOptions);
            if(例外一覧.Count!=0){
                if(!Query.ExpectExceptions){
                    Status=WorkerExecutionStatus.ExecutionFailed;
                    goto 完了;
                }
                // 例外を許す検証: System.Linq が例外を投げたら、他の実装も同じ型の例外を投げることを一致とする。
                var 例外比較=例外の一致を比べる(Loaded,結果一覧,例外一覧);
                ResultJSON=JsonSerializer.Serialize(
                    new WorkerValidationArtifact(結果一覧,例外一覧,例外比較),
                    DynamicWorkerProtocol.JsonOptions);
                // 全実装が同じ型の例外を投げたときは、比べる値も測る処理も無いので、ここで終わる。
                Status=例外比較.Values.Any(static 比較=>!比較.IsMatch)
                    ?WorkerExecutionStatus.SemanticMismatch
                    :WorkerExecutionStatus.Succeeded;
                goto 完了;
            }
            var 比較一覧=new Dictionary<DynamicImplementationKind,DynamicQueryComparison>();
            var System結果=結果一覧[DynamicImplementationKind.System];
            foreach(var 実装 in 検証対象.Where(実装=>実装!=DynamicImplementationKind.System&&Loaded.Has(実装))){
                比較一覧[実装]=DynamicQueryComparer.Compare(System結果,結果一覧[実装]);
            }
            ResultJSON=JsonSerializer.Serialize(
                new WorkerValidationArtifact(結果一覧,例外一覧,比較一覧),
                DynamicWorkerProtocol.JsonOptions);
            if(比較一覧.Values.Any(static 比較=>!比較.IsMatch)){
                Status=WorkerExecutionStatus.SemanticMismatch;
                goto 完了;
            }
            if(検証.Fallbacks.Count!=0){
                var 内訳=string.Join(", ",検証.Fallbacks.Select(static 項目=>$"{項目.Key}={項目.Value}"));
                診断JSON=診断を追加(
                    診断JSON,
                    "Expansion",
                    new WorkerInvocationFailure(
                        "FLinqRuntimeFallback",
                        $"実行時に EnumerableFallback が数えられました({内訳})。FLinq が展開しきれていません。",
                        null));
                Status=WorkerExecutionStatus.ExpansionIncomplete;
                goto 完了;
            }
            IReadOnlyDictionary<DynamicImplementationKind,long>? 評価回数=null;
            if(要求.CheckEvaluationCounts&&QueryEvaluationCheck.Applies(Query)){
                var 評価検査=this.評価回数を検査する(要求,Query,QueryIndex,対象,Fixture,キャンセルToken);
                評価回数=評価検査.Counts;
                if(評価検査.Skipped is not null){
                    診断JSON=診断を追加(診断JSON,"Evaluation",new WorkerInvocationFailure("EvaluationCheckSkipped",評価検査.Skipped,null),"Info");
                }
                ResultJSON=JsonSerializer.Serialize(
                    new WorkerValidationArtifact(結果一覧,例外一覧,比較一覧,評価回数),
                    DynamicWorkerProtocol.JsonOptions);
                if(評価検査.Mismatch is not null){
                    if(要求.StrictEvaluationCounts){
                        診断JSON=診断を追加(診断JSON,"Evaluation",new WorkerInvocationFailure("EvaluationCountMismatch",評価検査.Mismatch,null));
                        Status=WorkerExecutionStatus.SemanticMismatch;
                        goto 完了;
                    }
                    診断JSON=診断を追加(診断JSON,"Evaluation",new WorkerInvocationFailure("EvaluationCountExcess",評価検査.Mismatch,null),"Warning");
                }
            }
            var Profile=要求.MeasurementProfile;
            var 計測器=new MeasurementEngine(new MeasurementProfile(
                Profile.ProfileId,
                Profile.WarmupCount,
                Profile.SampleCount,
                TimeSpan.FromMilliseconds(Profile.MinimumCalibrationMilliseconds),
                Profile.MaximumOperationCount));
            var 計測結果=測定する(
                計測器,
                Loaded,
                Fixture.Primary,
                Fixture.Secondary,
                要求.MeasurementQueryIndexOverride??QueryIndex,
                Source順序,
                キャンセルToken);
            if(計測結果.Failure is not null){
                診断JSON=診断を追加(診断JSON,"Measurement",計測結果.Failure);
                Status=WorkerExecutionStatus.ExecutionFailed;
                goto 完了;
            }
            var 計測=計測結果.Result!;
            計測JSON=ArtifactをJSON化(Source生成時間,Compilation時間,計測,Trace概要);
            Status=WorkerExecutionStatus.Succeeded;
            goto 完了;
        }catch(OperationCanceledException 例外){
            if(例外 is Benchmark.Infrastructure.ResourceGuardException){診断JSON=JsonSerializer.Serialize(new[]{new DynamicCompilationDiagnostic("Resource","Error","PhysicalMemoryGuard",例外.Message,string.Empty)},DynamicWorkerProtocol.JsonOptions);}
            Status=WorkerExecutionStatus.Canceled;
            goto 完了;
        }catch(Exception 例外){
            診断JSON=JsonSerializer.Serialize(
                new[]{new DynamicCompilationDiagnostic(
                    "Execution",
                    "Error",
                    例外.GetType().Name,
                    例外.Message,
                    string.Empty)},
                DynamicWorkerProtocol.JsonOptions);
            Status=WorkerExecutionStatus.ExecutionFailed;
            goto 完了;
        }finally{
            try{
                Fixture?.Dispose();
            }catch(Exception 例外){
                診断JSON=診断を追加(診断JSON,"FixtureDispose",例外);
                if(Status==WorkerExecutionStatus.Succeeded){
                    Status=WorkerExecutionStatus.ExecutionFailed;
                }
            }
            if(Loaded is not null){
                var 解放結果=Loaded.UnloadAndVerify();
                解放済み=解放結果.Unloaded;
                if(!解放済み&&Status==WorkerExecutionStatus.Succeeded){
                    Status=WorkerExecutionStatus.AssemblyUnloadFailed;
                }
            }
            完了日時=DateTimeOffset.UtcNow;
        }

        完了:
        return 結果();

        WorkerSourceExecutionCompletedEvent 結果(){
            return new(
                要求.RunId,
                Query.QueryId,
                QueryIndex,
                対象,
                Status,
                SourceText,
                SourceHash,
                ResultJSON,
                診断JSON,
                TraceJSON,
                計測JSON,
                解放済み,
                完了日時,
                展開コード.Length==0?null:展開コード,
                入力ハッシュ,環境JSON,パラメーターJSON,拡張JSON,保存方式,ビルド指紋.Value,設定変更JSON,入出力方式);
        }
    }

    /// <summary>
    /// ラムダの本体を数える形のソース(<see cref="EvaluationProbeRewriter"/>)を別にコンパイルして、System.Linq と FLinq の評価回数を比べる。
    /// 数える形にするとラムダが関数呼び出しを含む形になり、FLinq の展開の形が変わることがある(これは、通常の検証とは別の展開を見ることになる)。
    /// コンパイルできなかったときは、検査できなかったこと(Skipped)を返し、失敗にはしない。
    /// </summary>
    private (string? Mismatch,string? Skipped,Dictionary<DynamicImplementationKind,long> Counts) 評価回数を検査する(
        WorkerRunRequest 要求,
        QueryIrDocument Query,
        int QueryIndex,
        SourceTarget 対象,
        SourceFixture Fixture,
        CancellationToken キャンセルToken){
        var 回数=new Dictionary<DynamicImplementationKind,long>();
        var ラムダごとの回数=new Dictionary<DynamicImplementationKind,long[]>();
        LoadedDynamicQuery? 読込=null;
        try{
            var Source=this.Source生成.Emit(Query,new QuerySourceEmissionOptions{
                GeneratedNamespace="Benchmark.Dynamic.Generated",
                ClassName=$"C_{安全な識別子(要求.RunId)}_{QueryIndex:D6}_{(int)対象}_{SourceHash短縮(Query)}",
            });
            var 数える形=EvaluationProbeRewriter.Instrument(Source.SourceText);
            var 結果=this.コンパイラ.Compile(
                new DynamicCompilationRequest(
                    $"Benchmark.Dynamic.Generated.Counted.{安全な識別子(要求.RunId)}.{QueryIndex}.{(int)対象}.{Source.SourceHash[..12]}",
                    Source.QualifiedTypeName,
                    数える形,
                    $"{Source.ClassName}.g.cs"),
                キャンセルToken);
            if(!結果.Success||結果.LoadedQuery is null){
                var 最初の誤り=結果.Diagnostics.FirstOrDefault(static 診断=>診断.Severity=="Error");
                return (null,$"数える形のソースをコンパイルできなかったので、評価回数は検査していません({最初の誤り?.ID}: {最初の誤り?.Message})。",回数);
            }
            読込=結果.LoadedQuery;
            foreach(var 実装 in 検証対象.Where(読込.Has)){
                キャンセルToken.ThrowIfCancellationRequested();
                EvaluationProbe.Reset();
                try{
                    読込.Run(実装,Fixture.Primary,Fixture.Secondary);
                }catch(Exception 例外) when(例外 is not OperationCanceledException){
                    // 例外の型の一致は、通常の検証が見ている。ここでは、例外までに評価した回数だけを使う。
                }
                回数[実装]=EvaluationProbe.Count;
                ラムダごとの回数[実装]=EvaluationProbe.CountsByLambda;
            }
            return (EvaluationCountComparer.CompareByLambda(ラムダごとの回数),null,回数);
        }finally{
            読込?.UnloadAndVerify();
        }
    }

    private static WorkerExecutionStatus 失敗Status(
        IReadOnlyList<DynamicCompilationDiagnostic> 診断一覧,
        bool 非収束か){
        if(非収束か){
            return WorkerExecutionStatus.OptimizationNonConverged;
        }
        if(診断一覧.Any(static 診断=>
            診断.ID is "InvocationTargetMismatch" or "FLinqInterceptorMissing")){
            return WorkerExecutionStatus.InvocationTargetMismatch;
        }
        if(診断一覧.Any(static 診断=>診断.ID=="FLinqExpansionIncomplete")){
            return WorkerExecutionStatus.ExpansionIncomplete;
        }
        return WorkerExecutionStatus.CompilationFailed;
    }

    private static Dictionary<DynamicImplementationKind,DynamicQueryComparison> 例外の一致を比べる(
        LoadedDynamicQuery Loaded,
        Dictionary<DynamicImplementationKind,DynamicQueryValue> 結果一覧,
        Dictionary<DynamicImplementationKind,WorkerInvocationFailure> 例外一覧){
        var 比較一覧=new Dictionary<DynamicImplementationKind,DynamicQueryComparison>();
        例外一覧.TryGetValue(DynamicImplementationKind.System,out var 期待);
        foreach(var 実装 in 検証対象.Where(実装=>実装!=DynamicImplementationKind.System&&Loaded.Has(実装))){
            例外一覧.TryGetValue(実装,out var 実際);
            比較一覧[実装]=(期待,実際) switch{
                (null,null)=>DynamicQueryComparer.Compare(結果一覧[DynamicImplementationKind.System],結果一覧[実装]),
                ({ }e,{ }a) when e.Type==a.Type=>new(true),
                ({ }e,{ }a)=>new(false,$"System threw {e.Type} but {実装} threw {a.Type}: {a.Message}"),
                ({ }e,null)=>new(false,$"System threw {e.Type} but {実装} returned a value."),
                (null,{ }a)=>new(false,$"System returned a value but {実装} threw {a.Type}: {a.Message}"),
            };
        }
        return 比較一覧;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ValidationExecution 検証する(
        LoadedDynamicQuery Loaded,
        object? Primary,
        object? Secondary,
        CancellationToken キャンセルToken){
        var 結果一覧=new Dictionary<DynamicImplementationKind,DynamicQueryValue>();
        var 例外一覧=new Dictionary<DynamicImplementationKind,WorkerInvocationFailure>();
        var フォールバック=new Dictionary<DynamicImplementationKind,long>();
        foreach(var 実装 in 検証対象.Where(Loaded.Has)){
            キャンセルToken.ThrowIfCancellationRequested();
            try{
                // FLinq の実行時フォールバック回数は、検証の実行だけで数える(計測の実行では数えない)。
                using var カウンタ範囲=global::Seamless.Linq.LinqCounterScope.Start();
                var 値=Loaded.Run(実装,Primary,Secondary);
                値.Validate();
                結果一覧[実装]=値;
                if(実装 is DynamicImplementationKind.FLinqUnoptimized or DynamicImplementationKind.FLinq
                    &&カウンタ範囲.Counter.EnumerableFallback>0){
                    フォールバック[実装]=カウンタ範囲.Counter.EnumerableFallback;
                }
            }catch(Exception 例外) when(例外 is not OperationCanceledException){
                例外一覧[実装]=new(
                    例外.GetType().FullName??例外.GetType().Name,
                    例外.Message,
                    例外.StackTrace);
            }
        }
        return new(結果一覧,例外一覧,フォールバック);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static MeasurementExecution 測定する(
        MeasurementEngine 計測器,
        LoadedDynamicQuery Loaded,
        object? Primary,
        object? Secondary,
        int QueryIndex,
        int Source順序,
        CancellationToken キャンセルToken){
        try{
            return new(
                計測器.Measure(Loaded,Primary,Secondary,QueryIndex,Source順序,キャンセルToken),
                null);
        }catch(Exception 例外) when(例外 is not OperationCanceledException){
            return new(
                null,
                new(
                    例外.GetType().FullName??例外.GetType().Name,
                    例外.Message,
                    例外.StackTrace));
        }
    }

    private static string ArtifactをJSON化(
        long Source生成時間,
        DynamicCompilationTimings Compilation時間,
        BalancedMeasurementResult? 計測,
        TraceSummary Trace概要){
        return JsonSerializer.Serialize(
            new WorkerExecutionArtifact(
                Source生成時間,
                Compilation時間,
                計測,
                Trace概要.RewriteCount,
                Trace概要.ReachedFixedPoint),
            DynamicWorkerProtocol.JsonOptions);
    }

    private static string TraceをJSON化(IReadOnlyList<string> Payload一覧){
        var Trace一覧=Payload一覧.Select(Payloadを展開).ToArray();
        return JsonSerializer.Serialize(Trace一覧,DynamicWorkerProtocol.JsonOptions);
    }

    private static TraceSummary Traceを集計(IReadOnlyList<string> Payload一覧){
        var RewriteCount=0;
        var FixedPoint=Payload一覧.Count>0;
        var NonConverged=false;
        foreach(var Payload in Payload一覧){
            try{
                using var Document=JsonDocument.Parse(Payloadを展開(Payload));
                var Summary=Document.RootElement.GetProperty("Summary");
                RewriteCount+=Summary.GetProperty("RewriteCount").GetInt32();
                var 到達=Summary.GetProperty("ReachedFixedPoint").GetBoolean();
                FixedPoint&=到達;
                NonConverged|=!到達;
            }catch(Exception 例外) when(例外 is JsonException or InvalidOperationException or KeyNotFoundException){
                FixedPoint=false;
                NonConverged=true;
            }
        }
        return new(RewriteCount,FixedPoint,NonConverged);
    }

    private static string Payloadを展開(string Payload){
        try{
            return Encoding.UTF8.GetString(Convert.FromBase64String(Payload));
        }catch(FormatException){
            return Payload;
        }
    }

    private static string 診断を追加(string? 現在,string 段階,Exception 例外){
        var 追加=new DynamicCompilationDiagnostic(
            段階,
            "Error",
            例外.GetType().Name,
            例外.Message,
            string.Empty);
        if(string.IsNullOrWhiteSpace(現在)){
            return JsonSerializer.Serialize(new[]{追加},DynamicWorkerProtocol.JsonOptions);
        }
        try{
            var 一覧=JsonSerializer.Deserialize<List<DynamicCompilationDiagnostic>>(
                現在,
                DynamicWorkerProtocol.JsonOptions)??[];
            一覧.Add(追加);
            return JsonSerializer.Serialize(一覧,DynamicWorkerProtocol.JsonOptions);
        }catch(JsonException){
            return JsonSerializer.Serialize(new object[]{現在,追加},DynamicWorkerProtocol.JsonOptions);
        }
    }

    private static string 診断を追加(
        string? 現在,
        string 段階,
        WorkerInvocationFailure 失敗,
        string 重大度="Error"){
        var 追加=new DynamicCompilationDiagnostic(
            段階,
            重大度,
            失敗.Type,
            失敗.Message,
            string.Empty);
        if(string.IsNullOrWhiteSpace(現在)){
            return JsonSerializer.Serialize(new[]{追加},DynamicWorkerProtocol.JsonOptions);
        }
        try{
            var 一覧=JsonSerializer.Deserialize<List<DynamicCompilationDiagnostic>>(
                現在,
                DynamicWorkerProtocol.JsonOptions)??[];
            一覧.Add(追加);
            return JsonSerializer.Serialize(一覧,DynamicWorkerProtocol.JsonOptions);
        }catch(JsonException){
            return JsonSerializer.Serialize(new object[]{現在,追加},DynamicWorkerProtocol.JsonOptions);
        }
    }

    private static DynamicCompilationTimings 空のコンパイル時間(){
        return new(0,0,0,0,0,0,0);
    }

    private static string SourceHash短縮(QueryIrDocument Query){
        return QueryIrJson.ComputeHash(Query)[..12];
    }

    private static string 安全な識別子(string 値){
        var Builder=new StringBuilder(値.Length+1);
        if(値.Length==0||!(char.IsLetter(値[0])||値[0]=='_')){
            Builder.Append('_');
        }
        foreach(var 文字 in 値){
            Builder.Append(char.IsLetterOrDigit(文字)||文字=='_'?文字:'_');
        }
        return Builder.ToString();
    }

    private static long 経過ナノ秒(long 開始){
        return (long)((Stopwatch.GetTimestamp()-開始)*(1_000_000_000d/Stopwatch.Frequency));
    }

    private sealed record WorkerInvocationFailure(string Type,string Message,string? StackTrace);
    private sealed record ValidationExecution(
        Dictionary<DynamicImplementationKind,DynamicQueryValue> Results,
        Dictionary<DynamicImplementationKind,WorkerInvocationFailure> Exceptions,
        Dictionary<DynamicImplementationKind,long> Fallbacks);
    private sealed record MeasurementExecution(
        BalancedMeasurementResult? Result,
        WorkerInvocationFailure? Failure);
    private sealed record WorkerValidationArtifact(
        IReadOnlyDictionary<DynamicImplementationKind,DynamicQueryValue> Results,
        IReadOnlyDictionary<DynamicImplementationKind,WorkerInvocationFailure> Exceptions,
        IReadOnlyDictionary<DynamicImplementationKind,DynamicQueryComparison> Comparisons,
        IReadOnlyDictionary<DynamicImplementationKind,long>? EvaluationCounts=null);
    private sealed record TraceSummary(int RewriteCount,bool ReachedFixedPoint,bool NonConverged);
}

public sealed class WorkerHost{
    private readonly TextReader 入力;
    private readonly TextWriter 出力;
    private readonly TextWriter Error;
    private readonly IWorkerExecutionRunner 実行器;
    private readonly SemaphoreSlim 出力Gate=new(1,1);
    private readonly DedicatedWorkerThread 専用Thread=new();
    private CancellationTokenSource? 実行キャンセル;
    private Task? 実行Task;
    private int 終了済み;

    public WorkerHost(
        TextReader 入力,
        TextWriter 出力,
        TextWriter Error,
        IWorkerExecutionRunner? 実行器=null){
        this.入力=入力??throw new ArgumentNullException(nameof(入力));
        this.出力=出力??throw new ArgumentNullException(nameof(出力));
        this.Error=Error??throw new ArgumentNullException(nameof(Error));
        this.実行器=実行器??new DynamicWorkerExecutionRunner();
    }

    public async Task<int> RunAsync(CancellationToken キャンセルToken=default){
        await this.送信Async(
            "worker",
            WorkerMessageTypes.WorkerStarted,
            new WorkerStartedEvent(
                Environment.ProcessId,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString()??"0.0.0",
                Environment.Version.ToString(),
                DateTimeOffset.UtcNow),
            CancellationToken.None).ConfigureAwait(false);
        try{
            while(await this.入力.ReadLineAsync(キャンセルToken).ConfigureAwait(false) is{} 行){
                WorkerMessageEnvelope Envelope;
                try{
                    Envelope=DynamicWorkerProtocol.Deserialize(行);
                }catch(Exception 例外) when(例外 is DynamicWorkerProtocolException or ArgumentException){
                    await this.Error.WriteLineAsync(例外.ToString()).ConfigureAwait(false);
                    return 2;
                }
                switch(Envelope.MessageType){
                    case WorkerMessageTypes.Ping:
                        await this.PingAsync(Envelope,キャンセルToken).ConfigureAwait(false);
                        break;
                    case WorkerMessageTypes.StartRun:
                        await this.開始Async(Envelope).ConfigureAwait(false);
                        break;
                    case WorkerMessageTypes.Cancel:
                        this.実行キャンセル?.Cancel();
                        await this.実行完了を待機().ConfigureAwait(false);
                        return 0;
                    case WorkerMessageTypes.Shutdown:
                        this.実行キャンセル?.Cancel();
                        await this.実行完了を待機().ConfigureAwait(false);
                        return 0;
                    default:
                        await this.Error.WriteLineAsync($"Unknown worker message type: {Envelope.MessageType}").ConfigureAwait(false);
                        return 2;
                }
            }
            await this.実行完了を待機().ConfigureAwait(false);
            return Volatile.Read(ref this.終了済み);
        }catch(OperationCanceledException){
            this.実行キャンセル?.Cancel();
            await this.実行完了を待機().ConfigureAwait(false);
            return 0;
        }catch(Exception 例外){
            await this.Error.WriteLineAsync(例外.ToString()).ConfigureAwait(false);
            return 1;
        }finally{
            this.実行キャンセル?.Cancel();
            try{
                await this.実行完了を待機().ConfigureAwait(false);
            }catch(Exception 例外){
                await this.Error.WriteLineAsync(例外.ToString()).ConfigureAwait(false);
            }
            this.実行キャンセル?.Dispose();
            this.実行キャンセル=null;
            this.専用Thread.Dispose();
            this.出力Gate.Dispose();
        }
    }

    private async Task PingAsync(WorkerMessageEnvelope Envelope,CancellationToken キャンセルToken){
        var Ping=DynamicWorkerProtocol.DeserializePayload<WorkerPingRequest>(Envelope);
        await this.送信Async(
            Envelope.RequestId,
            WorkerMessageTypes.Pong,
            new WorkerPongEvent(
                Ping.RequestedAtUtc,
                DateTimeOffset.UtcNow,
                Assembly.GetExecutingAssembly().GetName().Version?.ToString()??"0.0.0"),
            キャンセルToken).ConfigureAwait(false);
    }

    private Task 開始Async(WorkerMessageEnvelope Envelope){
        if(this.実行Task is not null&&!this.実行Task.IsCompleted){
            throw new InvalidOperationException("A worker run is already active.");
        }
        var 要求=DynamicWorkerProtocol.DeserializePayload<WorkerRunRequest>(Envelope);
        要求を検証(要求);
        this.実行キャンセル?.Dispose();
        this.実行キャンセル=new CancellationTokenSource();
        this.実行Task=this.Runを処理Async(Envelope.RequestId,要求,this.実行キャンセル.Token);
        return Task.CompletedTask;
    }

    private async Task Runを処理Async(string RequestID,WorkerRunRequest 要求,CancellationToken キャンセルToken){
        var Total=checked(要求.QueryCount*要求.SourceTargets.Count);
        var 完了済み実行=(要求.ResumeState?.CompletedSourceExecutions??[]).ToHashSet();
        var Completed=完了済み実行.Count;
        var Failed=要求.ResumeState?.FailedSourceExecutions??0;
        var CompilationFailures=要求.ResumeState?.CompilationFailures??0;
        var SemanticMismatches=要求.ResumeState?.SemanticMismatches??0;
        var NonConvergences=要求.ResumeState?.NonConvergences??0;
        var ExecutionFailures=要求.ResumeState?.ExecutionFailures??0;
        var ConsecutiveLeaks=0;
        try{
            await this.送信Async(
                RequestID,
                WorkerMessageTypes.RunStarted,
                new WorkerRunStartedEvent(要求.RunId,DateTimeOffset.UtcNow,Total),
                CancellationToken.None).ConfigureAwait(false);
            var Capability=QueryCapabilityProfiles.CreateSet(要求.OperatorSetMode,要求.SourceTargets,要求.ElementType);
            var Generator=new RandomQueryGenerator();
            var 全組み合わせ=要求.GenerationMode==QueryGenerationMode.Exhaustive
                ?new ExhaustiveQueryProvider(要求,Capability)
                :null;
            for(var LocalIndex=0;LocalIndex<要求.QueryCount;LocalIndex++){
                var QueryIndex=要求.QueryIndexOffset+LocalIndex;
                キャンセルToken.ThrowIfCancellationRequested();
                var 未完了Source順序=Enumerable.Range(0,要求.SourceTargets.Count)
                    .Where(Source順序=>!完了済み実行.Contains(new(
                        QueryIndex,
                        要求.SourceTargets[Source順序])))
                    .ToArray();
                if(未完了Source順序.Length==0){
                    continue;
                }
                var 生成開始=Stopwatch.GetTimestamp();
                var Replay=要求.ReplayQueries?.SingleOrDefault(Query=>Query.QueryIndex==QueryIndex);
                QueryIrDocument Template;
                string 保存IRJSON;
                if(Replay is null){
                    Template=全組み合わせ is not null
                        ?全組み合わせ.Get(QueryIndex) with{QueryId=$"{要求.RunId}-q{QueryIndex:D6}"}
                        :Generator.Generate(new RandomQueryGenerationOptions{
                            MasterSeed=要求.BaseSeed,
                            QueryIndex=QueryIndex,
                            QueryId=$"{要求.RunId}-q{QueryIndex:D6}",
                            SourceTarget=要求.SourceTargets.Order().First(),
                            OperatorCount=要求.OperatorCount,
                            MaximumDepth=要求.MaximumDepth,
                            InputElementCount=要求.InputElementCount,
                            MaximumCardinality=SourceFixtureFactory.MaximumInputElementCount,
                            EnableBinaryOperators=true,
                            EnableNestedOperators=true,
                            AllowedOperatorIds=Capability.OperatorIds,
                            ExpectExceptions=要求.ExpectExceptions,
                            ElementType=要求.ElementType,
                        });
                    保存IRJSON=QueryIrJson.Serialize(Template);
                }else{
                    var 保存IR=QueryIrJson.Deserialize(Replay.IrJson);
                    Template=対象へ変換(保存IR,要求.SourceTargets.Order().First()) with{
                        QueryId=$"{要求.RunId}-q{QueryIndex:D6}",
                    };
                    QueryIrValidator.ValidateAndThrow(Template);
                    保存IRJSON=Replay.IrJson;
                }
                QueryIrValidator.ValidateForExecutionAndThrow(Template,要求.InputElementCount);
                var IR生成時間=経過ナノ秒(生成開始);
                await this.送信Async(
                    RequestID,
                    WorkerMessageTypes.QueryStarted,
                    new WorkerQueryStartedEvent(
                        要求.RunId,
                        Template.QueryId,
                        QueryIndex,
                        Template.Seed,
                        保存IRJSON,
                        IR生成時間,
                        Replay?.ParentQueryId,
                        Replay?.DerivationKind??"SharedIR"),
                    CancellationToken.None).ConfigureAwait(false);
                foreach(var Source順序 in 未完了Source順序){
                    キャンセルToken.ThrowIfCancellationRequested();
                    var 対象=要求.SourceTargets[Source順序];
                    var Query=対象へ変換(Template,対象);
                    var 開始日時=DateTimeOffset.UtcNow;
                    await this.送信Async(
                        RequestID,
                        WorkerMessageTypes.SourceExecutionStarted,
                        new WorkerSourceExecutionStartedEvent(
                            要求.RunId,
                            Query.QueryId,
                            QueryIndex,
                            対象,
                            Source順序,
                            開始日時),
                        CancellationToken.None).ConfigureAwait(false);
                    var Query要求=Replay is null
                        ?要求
                        :要求 with{
                            FixtureSeedOverride=Replay.FixtureSeed,
                            MeasurementQueryIndexOverride=Replay.MeasurementQueryIndex,
                        };
                    var 結果=await this.専用Thread.RunAsync(
                        ()=>this.実行器.ExecuteAsync(
                            Query要求,
                            Query,
                            QueryIndex,
                            対象,
                            Source順序,
                            キャンセルToken).GetAwaiter().GetResult(),
                        キャンセルToken).ConfigureAwait(false);
                    if(!完了済み実行.Add(new(QueryIndex,対象))){
                        throw new InvalidOperationException(
                            $"A source execution completed more than once: QueryIndex={QueryIndex} SourceTarget={対象}");
                    }
                    Completed=完了済み実行.Count;
                    if(結果.Status!=WorkerExecutionStatus.Succeeded){
                        Failed++;
                    }
                    switch(結果.Status){
                        case WorkerExecutionStatus.CompilationFailed:
                        case WorkerExecutionStatus.InvocationTargetMismatch:
                        case WorkerExecutionStatus.ExpansionIncomplete:
                            CompilationFailures++;
                            break;
                        case WorkerExecutionStatus.SemanticMismatch:
                            SemanticMismatches++;
                            break;
                        case WorkerExecutionStatus.OptimizationNonConverged:
                            NonConvergences++;
                            break;
                        case WorkerExecutionStatus.ExecutionFailed:
                            ExecutionFailures++;
                            break;
                    }
                    if(結果.AssemblyUnloaded){
                        ConsecutiveLeaks=0;
                    }else{
                        ConsecutiveLeaks++;
                    }
                    await this.送信Async(
                        RequestID,
                        WorkerMessageTypes.SourceExecutionCompleted,
                        結果,
                        CancellationToken.None).ConfigureAwait(false);
                    await this.送信Async(
                        RequestID,
                        WorkerMessageTypes.Progress,
                        new WorkerProgressEvent(
                            要求.RunId,
                            Completed,
                            Total,
                            CompilationFailures,
                            SemanticMismatches,
                            NonConvergences,
                            ExecutionFailures,
                            キャンセルToken.IsCancellationRequested),
                        CancellationToken.None).ConfigureAwait(false);
                    if(ConsecutiveLeaks>=3){
                        var Fatal=new WorkerFatalEvent(
                            要求.RunId,
                            WorkerFatalErrorTypes.AssemblyUnloadFailure,
                            "Three consecutive collectible AssemblyLoadContext instances failed to unload.",
                            null,
                            DateTimeOffset.UtcNow);
                        await this.送信Async(
                            RequestID,
                            WorkerMessageTypes.Fatal,
                            Fatal,
                            CancellationToken.None).ConfigureAwait(false);
                        Interlocked.Exchange(ref this.終了済み,3);
                        return;
                    }
                }
            }
            await this.送信Async(
                RequestID,
                WorkerMessageTypes.RunCompleted,
                new WorkerRunCompletedEvent(
                    要求.RunId,
                    WorkerExecutionStatus.Succeeded,
                    Completed,
                    Failed,
                    DateTimeOffset.UtcNow),
                CancellationToken.None).ConfigureAwait(false);
        }catch(OperationCanceledException){
            var 終了=new WorkerRunCompletedEvent(
                要求.RunId,
                WorkerExecutionStatus.Canceled,
                Completed,
                Failed,
                DateTimeOffset.UtcNow);
            await this.送信Async(
                RequestID,
                WorkerMessageTypes.RunCanceled,
                終了,
                CancellationToken.None).ConfigureAwait(false);
        }catch(Exception 例外){
            var Fatal=new WorkerFatalEvent(
                要求.RunId,
                例外.GetType().FullName??例外.GetType().Name,
                例外.Message,
                例外.StackTrace,
                DateTimeOffset.UtcNow);
            await this.送信Async(RequestID,WorkerMessageTypes.Fatal,Fatal,CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task 送信Async<T>(
        string RequestID,
        string MessageType,
        T Payload,
        CancellationToken キャンセルToken){
        var 行=DynamicWorkerProtocol.Serialize(DynamicWorkerProtocol.Create(RequestID,MessageType,Payload));
        await this.出力Gate.WaitAsync(キャンセルToken).ConfigureAwait(false);
        try{
            await this.出力.WriteLineAsync(行.AsMemory(),キャンセルToken).ConfigureAwait(false);
            await this.出力.FlushAsync().ConfigureAwait(false);
        }finally{
            this.出力Gate.Release();
        }
    }

    private async Task 実行完了を待機(){
        if(this.実行Task is not null){
            await this.実行Task.ConfigureAwait(false);
            this.実行Task=null;
        }
    }


    private static void 要求を検証(WorkerRunRequest 要求){
        ArgumentException.ThrowIfNullOrWhiteSpace(要求.RunId);
        if(要求.QueryCount<1||要求.QueryCount>1_000){
            throw new ArgumentOutOfRangeException(nameof(要求.QueryCount));
        }
        if(要求.OperatorCount<1||要求.OperatorCount>64){
            throw new ArgumentOutOfRangeException(nameof(要求.OperatorCount));
        }
        if(要求.MaximumDepth<1||要求.MaximumDepth>Math.Min(要求.OperatorCount,32)){
            throw new ArgumentOutOfRangeException(nameof(要求.MaximumDepth));
        }
        if(要求.InputElementCount<0||要求.InputElementCount>SourceFixtureFactory.MaximumInputElementCount){
            throw new ArgumentOutOfRangeException(nameof(要求.InputElementCount));
        }
        if(要求.SourceTargets.Count<1||要求.SourceTargets.Count>Enum.GetValues<SourceTarget>().Length
            ||要求.SourceTargets.Distinct().Count()!=要求.SourceTargets.Count){
            throw new ArgumentException("SourceTargets must contain one to eight distinct values.",nameof(要求.SourceTargets));
        }
        if(要求.QueryIndexOffset<0){
            throw new ArgumentOutOfRangeException(nameof(要求.QueryIndexOffset));
        }
        foreach(var 対象 in 要求.SourceTargets){
            if(!QueryCapabilityProfiles.SupportsElementType(対象,要求.ElementType)){
                throw new ArgumentException($"{要求.ElementType} is not supported on {対象}.",nameof(要求));
            }
        }
        if(要求.GenerationMode==QueryGenerationMode.Exhaustive){
            if(要求.ExhaustiveIntermediateCount<1
                ||要求.OperatorCount!=要求.ExhaustiveIntermediateCount+1
                ||要求.MaximumDepth!=要求.OperatorCount){
                throw new ArgumentException(
                    "Exhaustive generation requires ExhaustiveIntermediateCount >= 1, OperatorCount = ExhaustiveIntermediateCount + 1 and MaximumDepth = OperatorCount.",
                    nameof(要求));
            }
            if(要求.ReplayQueries is not null){
                throw new ArgumentException("Exhaustive generation is not supported together with ReplayQueries.",nameof(要求));
            }
        }
        if(要求.ReplayQueries is not null&&要求.QueryIndexOffset!=0){
            throw new ArgumentException(
                "QueryIndexOffset is not supported together with ReplayQueries.",
                nameof(要求.QueryIndexOffset));
        }
        if(要求.ReplayQueries is not null){
            if(要求.ReplayQueries.Count!=要求.QueryCount
                ||!要求.ReplayQueries.Select(static Query=>Query.QueryIndex).Order()
                    .SequenceEqual(Enumerable.Range(0,要求.QueryCount))){
                throw new ArgumentException(
                    "ReplayQueries must contain exactly one entry for every zero-based QueryIndex.",
                    nameof(要求.ReplayQueries));
            }
            var 許可演算子=QueryCapabilityProfiles.CreateSet(要求.OperatorSetMode,要求.SourceTargets,要求.ElementType)
                .OperatorIds.ToHashSet(StringComparer.Ordinal);
            foreach(var Replay in 要求.ReplayQueries){
                ArgumentException.ThrowIfNullOrWhiteSpace(Replay.IrJson);
                if(Replay.MeasurementQueryIndex<0){
                    throw new ArgumentOutOfRangeException(nameof(Replay.MeasurementQueryIndex));
                }
                var IR=QueryIrJson.Deserialize(Replay.IrJson);
                if(IR.ActualOperatorCount!=要求.OperatorCount||IR.ActualDepth>要求.MaximumDepth){
                    throw new ArgumentException(
                        "A replay IR does not match the requested operator count or depth budget.",
                        nameof(要求.ReplayQueries));
                }
                var 未対応=演算子一覧(IR.Root)
                    .Where(Operator=>Operator!=QueryOperatorIds.Source&&!許可演算子.Contains(Operator))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if(未対応.Length!=0){
                    throw new ArgumentException(
                        $"A replay IR contains operators outside the current capability: {string.Join(",",未対応)}",
                        nameof(要求.ReplayQueries));
                }
            }
        }
        if(要求.ResumeState is{ }再開){
            var 完了済み=再開.CompletedSourceExecutions??throw new ArgumentException(
                "ResumeState.CompletedSourceExecutions is required.",
                nameof(要求.ResumeState));
            if(完了済み.Distinct().Count()!=完了済み.Count
                ||完了済み.Any(実行=>実行.QueryIndex<要求.QueryIndexOffset
                    ||実行.QueryIndex>=要求.QueryIndexOffset+要求.QueryCount
                    ||!要求.SourceTargets.Contains(実行.SourceTarget))){
                throw new ArgumentException(
                    "ResumeState contains an invalid or duplicate QueryIndex and SourceTarget pair.",
                    nameof(要求.ResumeState));
            }
            var 集計値=new[]{
                再開.FailedSourceExecutions,
                再開.CompilationFailures,
                再開.SemanticMismatches,
                再開.NonConvergences,
                再開.ExecutionFailures,
            };
            if(集計値.Any(static 値=>値<0)
                ||再開.FailedSourceExecutions>完了済み.Count
                ||再開.CompilationFailures+再開.SemanticMismatches+再開.NonConvergences+再開.ExecutionFailures
                    >再開.FailedSourceExecutions){
                throw new ArgumentException("ResumeState counters are inconsistent.",nameof(要求.ResumeState));
            }
        }
        var Profile=要求.MeasurementProfile;
        if(Profile.WarmupCount<0||Profile.SampleCount<1||Profile.MinimumCalibrationMilliseconds<0
            ||Profile.MaximumOperationCount<1||Profile.MaximumOperationCount>1_048_576){
            throw new ArgumentException("Measurement profile values are outside the supported range.",nameof(要求.MeasurementProfile));
        }
    }

    private static IEnumerable<string> 演算子一覧(QueryNode Node){
        yield return Node.OperatorId;
        switch(Node){
            case UnaryQueryNode Unary:
                foreach(var Operator in 演算子一覧(Unary.Input)){
                    yield return Operator;
                }
                break;
            case BinaryQueryNode Binary:
                foreach(var Operator in 演算子一覧(Binary.Left)){
                    yield return Operator;
                }
                foreach(var Operator in 演算子一覧(Binary.Right)){
                    yield return Operator;
                }
                break;
            case NestedQueryNode Nested:
                foreach(var Operator in 演算子一覧(Nested.Outer)){
                    yield return Operator;
                }
                foreach(var Operator in 演算子一覧(Nested.Inner)){
                    yield return Operator;
                }
                break;
            case TerminalQueryNode Terminal:
                foreach(var Operator in 演算子一覧(Terminal.Input)){
                    yield return Operator;
                }
                break;
        }
    }

    private static QueryIrDocument 対象へ変換(QueryIrDocument Template,SourceTarget 対象){
        var Root=(TerminalQueryNode)Nodeを変換(Template.Root,対象);
        var Query=Template with{SourceTarget=対象,Root=Root};
        QueryIrValidator.ValidateAndThrow(Query);
        return Query;
    }

    private static QueryNode Nodeを変換(QueryNode Node,SourceTarget 対象){
        return Node switch{
            SourceNode Source=>Source with{
                SourceTarget=対象,
                OrderSemantics=対象.GetInitialOrder(),
            },
            UnaryQueryNode Unary=>Unaryを変換(Unary,対象),
            BinaryQueryNode Binary=>Binaryを変換(Binary,対象),
            NestedQueryNode Nested=>Nestedを変換(Nested,対象),
            TerminalQueryNode Terminal=>Terminalを変換(Terminal,対象),
            _=>throw new InvalidOperationException($"Unsupported query node: {Node.GetType().FullName}"),
        };
    }

    private static UnaryQueryNode Unaryを変換(UnaryQueryNode Node,SourceTarget 対象){
        var Input=Nodeを変換(Node.Input,対象);
        var Order=Node.Arguments switch{
            DistinctOperatorArguments=>QueryOrderSemantics.Unordered,
            OrderByOperatorArguments or ThenByOperatorArguments=>QueryOrderSemantics.TotalOrdered,
            ExtendedUnaryOperatorArguments Extended=>ExtendedUnaryShape.Order(Extended,Input.OrderSemantics),
            _=>Input.OrderSemantics,
        };
        return Node with{Input=Input,OrderSemantics=Order};
    }

    private static BinaryQueryNode Binaryを変換(BinaryQueryNode Node,SourceTarget 対象){
        var Left=Nodeを変換(Node.Left,対象);
        var Right=Nodeを変換(Node.Right,対象);
        var Order=Node.Arguments.Kind is QueryBinaryKind.Concat or QueryBinaryKind.Zip
            &&Left.OrderSemantics!=QueryOrderSemantics.Unordered
            &&Right.OrderSemantics!=QueryOrderSemantics.Unordered
                ?QueryOrderSemantics.Ordered
                :QueryOrderSemantics.Unordered;
        return Node with{Left=Left,Right=Right,OrderSemantics=Order};
    }

    private static NestedQueryNode Nestedを変換(NestedQueryNode Node,SourceTarget 対象){
        var Outer=Nodeを変換(Node.Outer,対象);
        var Inner=Nodeを変換(Node.Inner,対象);
        var Order=Outer.OrderSemantics==QueryOrderSemantics.Unordered||Inner.OrderSemantics==QueryOrderSemantics.Unordered
            ?QueryOrderSemantics.Unordered
            :QueryOrderSemantics.Ordered;
        return Node with{Outer=Outer,Inner=Inner,OrderSemantics=Order};
    }

    private static TerminalQueryNode Terminalを変換(TerminalQueryNode Node,SourceTarget 対象){
        var Input=Nodeを変換(Node.Input,対象);
        var Order=Node.OutputCardinality==QueryCardinality.Scalar
            ?QueryOrderSemantics.TotalOrdered
            :ExtendedTerminalShape.IsExtended(Node.Arguments.Kind)
                ?ExtendedTerminalShape.Order(Node.Arguments.Kind,Input.OrderSemantics)
                // QueryIrValidator の規則と同じにする。違うと、順序なしの入力が ToLookup などへ来たときに
                // 実行先への変換後の検証が例外になり、実行全体が Fatal で止まる。
                :Node.Arguments.Kind==QueryTerminalKind.ToHashSet
                    ?QueryOrderSemantics.Unordered
                    :Node.Arguments.Kind is QueryTerminalKind.ToDictionary or QueryTerminalKind.ToLookup
                        ?QueryOrderSemantics.TotalOrdered
                        :Input.OrderSemantics;
        return Node with{Input=Input,OrderSemantics=Order};
    }

    private static long 経過ナノ秒(long 開始){
        return (long)((Stopwatch.GetTimestamp()-開始)*(1_000_000_000d/Stopwatch.Frequency));
    }

}

internal sealed class DedicatedWorkerThread:IDisposable{
    private readonly BlockingCollection<I作業> Queue=new();
    private readonly Thread Thread;
    private int 破棄済み;

    public DedicatedWorkerThread(){
        this.Thread=new Thread(this.処理){
            IsBackground=true,
            Name="Benchmark.Dynamic.Worker.Execution",
        };
        this.Thread.Start();
    }

    public Task<T> RunAsync<T>(Func<T> 処理,CancellationToken キャンセルToken){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.破棄済み)!=0,this);
        キャンセルToken.ThrowIfCancellationRequested();
        var 作業=new 作業<T>(処理,キャンセルToken);
        this.Queue.Add(作業,キャンセルToken);
        return 作業.Task;
    }

    public void Dispose(){
        if(Interlocked.Exchange(ref this.破棄済み,1)!=0){
            return;
        }
        this.Queue.CompleteAdding();
        this.Thread.Join();
        this.Queue.Dispose();
    }

    private void 処理(){
        foreach(var 作業 in this.Queue.GetConsumingEnumerable()){
            作業.実行();
        }
    }

    private interface I作業{
        void 実行();
    }

    private sealed class 作業<T>:I作業{
        private readonly Func<T> 処理;
        private readonly CancellationToken キャンセルToken;
        private readonly TaskCompletionSource<T> 完了=new(TaskCreationOptions.RunContinuationsAsynchronously);

        public 作業(Func<T> 処理,CancellationToken キャンセルToken){
            this.処理=処理;
            this.キャンセルToken=キャンセルToken;
        }

        public Task<T> Task=>this.完了.Task;

        public void 実行(){
            if(this.キャンセルToken.IsCancellationRequested){
                this.完了.TrySetCanceled(this.キャンセルToken);
                return;
            }
            try{
                this.完了.TrySetResult(this.処理());
            }catch(OperationCanceledException) when(this.キャンセルToken.IsCancellationRequested){
                this.完了.TrySetCanceled(this.キャンセルToken);
            }catch(Exception 例外){
                this.完了.TrySetException(例外);
            }
        }
    }
}
