using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using Verification.Core;

namespace Verification.Infrastructure;

public sealed record DynamicShrinkOptions(
    int MaximumCandidates=32,
    int TimeBudgetMilliseconds=30_000);

public sealed record DynamicShrinkInput(
    string ParentRunId,
    string ParentCaseId,
    string ReplayRequestJson,
    string ExecutionJson,
    DynamicShrinkOptions Options);

public sealed record DynamicShrinkEvaluationRecord(
    int CandidateIndex,
    string DerivationKind,
    int InputElementCount,
    string QueryHash,
    string? WorkerStatus,
    FailureSignature? ObservedFailure,
    bool MatchesExpectedFailure,
    string? Error);

public sealed record DynamicShrinkArtifact(
    string ParentRunId,
    string ParentCaseId,
    DynamicShrinkOptions Options,
    FailureSignature ExpectedFailure,
    ShrinkResult Result,
    IReadOnlyList<DynamicShrinkEvaluationRecord> Evaluations);

public sealed record DynamicShrinkReproduction(
    FailureSignature ExpectedFailure,
    FailureSignature? ActualFailure,
    bool Reproduced);

public static class DynamicShrinkPlanFactory{
    public static VerificationPlan Create(
        IVerificationRepository repository,
        string artifactRoot,
        string repositoryRoot,
        string parentRunId,
        string parentCaseId,
        DynamicShrinkOptions? options=null){
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentCaseId);
        options??=new();
        Validate(options);
        var parent=repository.GetRun(parentRunId);
        var item=parent.Plan.Cases.SingleOrDefault(item=>item.Id==parentCaseId)
            ??throw new KeyNotFoundException("親実行に指定ケースがありません: "+parentCaseId);
        if(item.Adapter!="dynamic-worker"){
            throw new InvalidOperationException("Dynamic Worker のケースだけを縮小できます。");
        }
        var result=repository.GetResults(parentRunId).SingleOrDefault(result=>result.CaseId==parentCaseId)
            ??throw new KeyNotFoundException("指定ケースの結果がありません: "+parentCaseId);
        if(result.Status is CaseStatus.Passed or CaseStatus.NotRun or CaseStatus.Running){
            throw new InvalidOperationException("完了した失敗ケースだけを縮小できます。");
        }
        var replayJson=ReadVerifiedArtifact(result,artifactRoot,"/replay-request.json");
        var executionJson=ReadVerifiedArtifact(result,artifactRoot,"/execution.json");
        var request=ReadReplayRequest(replayJson);
        var execution=ReadExecution(executionJson);
        EnsureShrinkableFailure(execution.Status);
        if(execution.SourceTarget!=request.SourceTargets.Single()){
            throw new InvalidDataException("再現要求と失敗結果のSourceTargetが一致しません。");
        }
        var input=new DynamicShrinkInput(parentRunId,parentCaseId,replayJson,executionJson,options);
        var inputJson=JsonSerializer.Serialize(input,DynamicWorkerProtocol.JsonOptions);
        var inputHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputJson)));
        var conditions=item.Conditions with{
            CaseVersion="dynamic-shrink-v1|"+item.Conditions.CaseVersion,
            InputHash=inputHash,
            MeasurementVersion="dynamic-shrink-v1|candidates="+options.MaximumCandidates+"|budget-ms="+options.TimeBudgetMilliseconds,
            Configuration="Release",
        };
        var shrinkCase=new VerificationCase(
            "dynamic-shrink:"+parentCaseId+":"+inputHash[..16],
            "失敗縮小: "+item.Name,
            "クエリの正しさ検証",
            "dynamic-shrink",
            item.Project,
            item.PrepareArguments,
            [inputJson],
            conditions,
            "失敗署名を維持する最小のQuery IRと入力件数を探索し、独立Workerで再現します。");
        var budgetSeconds=Math.Max(120,checked((options.TimeBudgetMilliseconds+999)/1_000+60));
        return new(
            1,
            Path.GetFullPath(repositoryRoot),
            RepositoryInspection.Fingerprint(repositoryRoot),
            "shrink",
            budgetSeconds,
            [shrinkCase]);
    }

    internal static string ReadVerifiedArtifact(CaseResult result,string artifactRoot,string suffix){
        var reference=result.Artifacts?.SingleOrDefault(artifact=>artifact.RelativePath.EndsWith(suffix,StringComparison.Ordinal))
            ??throw new InvalidDataException("縮小に必要な成果物がありません: "+suffix);
        var root=Path.GetFullPath(artifactRoot);
        var path=Path.GetFullPath(Path.Combine(root,reference.RelativePath.Replace('/',Path.DirectorySeparatorChar)));
        var prefix=root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){
            throw new InvalidDataException("成果物のパスが成果物領域外です: "+reference.RelativePath);
        }
        using(var input=File.OpenRead(path)){
            var hash=Convert.ToHexString(SHA256.HashData(input));
            if(!hash.Equals(reference.Sha256,StringComparison.OrdinalIgnoreCase)){
                throw new InvalidDataException("成果物のハッシュが記録と一致しません: "+reference.RelativePath);
            }
        }
        return File.ReadAllText(path);
    }

    internal static WorkerRunRequest ReadReplayRequest(string json){
        var request=JsonSerializer.Deserialize<WorkerRunRequest>(json,DynamicWorkerProtocol.JsonOptions)
            ??throw new InvalidDataException("再現要求を読み込めません。");
        if(request.QueryCount!=1||request.SourceTargets.Count!=1||request.ReplayQueries?.Count!=1){
            throw new InvalidDataException("縮小元は1 Query・1 Sourceの正確な再現要求である必要があります。");
        }
        _=QueryIrJson.Deserialize(request.ReplayQueries[0].IrJson);
        return request;
    }

    internal static WorkerSourceExecutionCompletedEvent ReadExecution(string json){
        return JsonSerializer.Deserialize<WorkerSourceExecutionCompletedEvent>(json,DynamicWorkerProtocol.JsonOptions)
            ??throw new InvalidDataException("失敗結果を読み込めません。");
    }

    internal static void EnsureShrinkableFailure(WorkerExecutionStatus status){
        if(status is WorkerExecutionStatus.CompilationFailed
            or WorkerExecutionStatus.InvocationTargetMismatch
            or WorkerExecutionStatus.SemanticMismatch
            or WorkerExecutionStatus.OptimizationNonConverged
            or WorkerExecutionStatus.ExecutionFailed
            or WorkerExecutionStatus.AssemblyUnloadFailed){
            return;
        }
        throw new InvalidOperationException("縮小対象外のWorker状態です: "+status);
    }

    private static void Validate(DynamicShrinkOptions options){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumCandidates);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.TimeBudgetMilliseconds);
    }
}

internal sealed record DynamicCandidateObservation(
    WorkerSourceExecutionCompletedEvent? Completed,
    WorkerFatalEvent? Fatal,
    string StandardError){
    public FailureSignature? Failure{
        get{
            if(this.Completed is not null){
                return FailureSignatureFactory.FromWorkerArtifacts(
                    this.Completed.Status,
                    this.Completed.DiagnosticsJson,
                    this.Completed.ResultJson,
                    this.Completed.OptimizationTraceJson);
            }
            if(this.Fatal is not null){
                return new(nameof(WorkerExecutionStatus.Fatal),this.Fatal.ErrorType,string.Empty,string.Empty,string.Empty);
            }
            return null;
        }
    }
}

internal interface IDynamicCandidateEvaluator{
    Task<DynamicCandidateObservation> EvaluateAsync(
        WorkerRunRequest request,
        string workerPath,
        string repositoryRoot,
        CancellationToken cancellationToken);
}

internal sealed class WorkerDynamicCandidateEvaluator:IWorkerProcessClient,IDynamicCandidateEvaluator{
    private readonly VersionedJsonLineWorkerProcessClient client=new();

    public IWorkerProcessSession Start(string workerPath,IReadOnlyList<string>? arguments=null,string? workingDirectory=null){
        return this.client.Start(workerPath,arguments,workingDirectory);
    }

    public async Task<DynamicCandidateObservation> EvaluateAsync(
        WorkerRunRequest request,
        string workerPath,
        string repositoryRoot,
        CancellationToken cancellationToken){
        await using var session=this.Start(workerPath,workingDirectory:repositoryRoot);
        WorkerSourceExecutionCompletedEvent? completed=null;
        WorkerFatalEvent? fatal=null;
        try{
            await session.SendAsync(request.RunId,WorkerMessageTypes.StartRun,request,cancellationToken).ConfigureAwait(false);
            await foreach(var envelope in session.ReadAllAsync(cancellationToken).ConfigureAwait(false)){
                if(envelope.MessageType==WorkerMessageTypes.SourceExecutionCompleted){
                    completed=DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>(envelope);
                    break;
                }
                if(envelope.MessageType==WorkerMessageTypes.Fatal){
                    fatal=DynamicWorkerProtocol.DeserializePayload<WorkerFatalEvent>(envelope);
                    break;
                }
            }
        }catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){
            if(!session.HasExited){await session.CancelAsync(request.RunId).ConfigureAwait(false);}
            throw;
        }finally{
            if(!session.HasExited){await session.ShutdownAsync(request.RunId).ConfigureAwait(false);}
        }
        return new(completed,fatal,await session.GetStandardErrorAsync().ConfigureAwait(false));
    }
}

internal sealed record DynamicShrinkEngineResult(
    ShrinkResult Result,
    WorkerRunRequest ShrunkRequest,
    FailureSignature ExpectedFailure,
    IReadOnlyList<DynamicShrinkEvaluationRecord> Evaluations);

internal sealed class DynamicShrinkEngine(IDynamicCandidateEvaluator evaluator){
    private readonly IDynamicCandidateEvaluator evaluator=evaluator;

    public async Task<DynamicShrinkEngineResult> ShrinkAsync(
        DynamicShrinkInput input,
        string workerPath,
        string repositoryRoot,
        Func<DynamicShrinkEvaluationRecord,CancellationToken,ValueTask>? observationSink=null,
        CancellationToken cancellationToken=default){
        var originalRequest=DynamicShrinkPlanFactory.ReadReplayRequest(input.ReplayRequestJson);
        var originalExecution=DynamicShrinkPlanFactory.ReadExecution(input.ExecutionJson);
        DynamicShrinkPlanFactory.EnsureShrinkableFailure(originalExecution.Status);
        var expected=FailureSignatureFactory.FromWorkerArtifacts(
            originalExecution.Status,
            originalExecution.DiagnosticsJson,
            originalExecution.ResultJson,
            originalExecution.OptimizationTraceJson);
        var originalReplay=originalRequest.ReplayQueries!.Single();
        var originalQuery=QueryIrJson.Deserialize(originalReplay.IrJson);
        var evaluations=new List<DynamicShrinkEvaluationRecord>();
        var result=await new QueryShrinker().ShrinkAsync(
            new(originalQuery,originalRequest.InputElementCount,expected,input.Options.MaximumCandidates,input.Options.TimeBudgetMilliseconds),
            async(candidate,token)=>{
                var request=CreateRequest(originalRequest,originalReplay,candidate.Query,candidate.InputElementCount,candidate.DerivationKind,true);
                DynamicCandidateObservation observation;
                try{
                    observation=await this.evaluator.EvaluateAsync(request,workerPath,repositoryRoot,token).ConfigureAwait(false);
                }catch(Exception exception) when(exception is not OperationCanceledException){
                    observation=new(null,null,exception.ToString());
                }
                var failure=observation.Failure;
                var error=observation.StandardError;
                if(observation.Fatal is not null){error=observation.Fatal.Message+Environment.NewLine+error;}
                var record=new DynamicShrinkEvaluationRecord(
                    candidate.CandidateIndex,
                    candidate.DerivationKind,
                    candidate.InputElementCount,
                    QueryIrJson.ComputeHash(candidate.Query),
                    observation.Completed?.Status.ToString()??observation.Fatal?.ErrorType,
                    failure,
                    failure==expected,
                    string.IsNullOrWhiteSpace(error)?null:error);
                evaluations.Add(record);
                if(observationSink is not null){await observationSink(record,token).ConfigureAwait(false);}
                return failure;
            },
            cancellationToken).ConfigureAwait(false);
        var shrunk=CreateRequest(originalRequest,originalReplay,result.ShrunkQuery,result.ShrunkInputElementCount,"Shrink",false);
        return new(result,shrunk,expected,evaluations);
    }

    private static WorkerRunRequest CreateRequest(
        WorkerRunRequest original,
        WorkerReplayQuery originalReplay,
        QueryIrDocument query,
        int inputElementCount,
        string derivation,
        bool isEvaluation){
        var replay=new WorkerReplayQuery(
            0,
            QueryIrJson.Serialize(query),
            originalReplay.ParentQueryId,
            originalReplay.FixtureSeed??original.FixtureSeedOverride??SeedDerivation.Derive(original.BaseSeed,"fixture",originalReplay.MeasurementQueryIndex??0),
            originalReplay.MeasurementQueryIndex??originalReplay.QueryIndex,
            derivation);
        var measurement=original.MeasurementProfile;
        if(isEvaluation){measurement=new("Shrink-evaluation-v1",0,1,0,1);}
        return original with{
            RunId=(isEvaluation?"shrink-eval-":"shrink-replay-")+Guid.NewGuid().ToString("N"),
            QueryCount=1,
            OperatorCount=query.ActualOperatorCount,
            MaximumDepth=query.ActualDepth,
            InputElementCount=inputElementCount,
            SourceTargets=[original.SourceTargets.Single()],
            MeasurementProfile=measurement,
            ReplayQueries=[replay],
            FixtureSeedOverride=null,
            MeasurementQueryIndexOverride=null,
            ResumeState=null,
            QueryIndexOffset=0,
        };
    }
}

public sealed class DynamicShrinkVerificationAdapter:IVerificationAdapter{
    private readonly DotnetVerificationAdapter builder=new();
    private readonly DynamicVerificationAdapter replayAdapter=new();
    private readonly IDynamicCandidateEvaluator evaluator;

    public DynamicShrinkVerificationAdapter()
        :this(new WorkerDynamicCandidateEvaluator()){
    }

    internal DynamicShrinkVerificationAdapter(IDynamicCandidateEvaluator evaluator){
        this.evaluator=evaluator;
    }

    public string Name=>"dynamic-shrink";

    public Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifacts,CancellationToken cancellationToken){
        return this.builder.PrepareAsync(item,repositoryRoot,artifacts,cancellationToken);
    }

    public async Task<CaseResult> ExecuteAsync(
        VerificationCase item,
        string repositoryRoot,
        string artifacts,
        CancellationToken cancellationToken){
        if(item.Arguments.Length!=1){throw new InvalidDataException("Dynamic shrink case must contain one input.");}
        var input=JsonSerializer.Deserialize<DynamicShrinkInput>(item.Arguments[0],DynamicWorkerProtocol.JsonOptions)
            ??throw new InvalidDataException("Dynamic shrink input is missing.");
        var worker=Path.Combine(repositoryRoot,"Benchmark","Benchmark.Dynamic.Worker","bin","Release","net10.0","Benchmark.Dynamic.Worker.dll");
        await using var trace=new StreamWriter(Path.Combine(artifacts,"candidate-evaluations.jsonl"),false,new UTF8Encoding(false)){AutoFlush=true};
        var engine=new DynamicShrinkEngine(this.evaluator);
        var shrunk=await engine.ShrinkAsync(
            input,
            worker,
            repositoryRoot,
            async(record,token)=>{
                var json=JsonSerializer.Serialize(record,DynamicWorkerProtocol.JsonOptions);
                await trace.WriteLineAsync(json.AsMemory(),token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        var shrinkArtifact=new DynamicShrinkArtifact(input.ParentRunId,input.ParentCaseId,input.Options,shrunk.ExpectedFailure,shrunk.Result,shrunk.Evaluations);
        await File.WriteAllTextAsync(
            Path.Combine(artifacts,"shrink-result.json"),
            JsonSerializer.Serialize(shrinkArtifact,DynamicWorkerProtocol.JsonOptions),
            cancellationToken).ConfigureAwait(false);
        var replayJson=JsonSerializer.Serialize(shrunk.ShrunkRequest,DynamicWorkerProtocol.JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(artifacts,"shrunk-replay-request.json"),replayJson,cancellationToken).ConfigureAwait(false);
        var replayCase=item with{Adapter="dynamic-worker",Arguments=[replayJson]};
        var replayResult=await this.replayAdapter.ExecuteAsync(replayCase,repositoryRoot,artifacts,cancellationToken).ConfigureAwait(false);
        FailureSignature? actual=null;
        var executionPath=Path.Combine(artifacts,"execution.json");
        if(File.Exists(executionPath)){
            var execution=DynamicShrinkPlanFactory.ReadExecution(await File.ReadAllTextAsync(executionPath,cancellationToken).ConfigureAwait(false));
            actual=FailureSignatureFactory.FromWorkerArtifacts(execution.Status,execution.DiagnosticsJson,execution.ResultJson,execution.OptimizationTraceJson);
        }
        var reproduced=actual==shrunk.ExpectedFailure;
        var reproduction=new DynamicShrinkReproduction(shrunk.ExpectedFailure,actual,reproduced);
        await File.WriteAllTextAsync(
            Path.Combine(artifacts,"reproduction.json"),
            JsonSerializer.Serialize(reproduction,DynamicWorkerProtocol.JsonOptions),
            cancellationToken).ConfigureAwait(false);
        var status=reproduced?CaseStatus.Passed:CaseStatus.Failed;
        if(replayResult.Status==CaseStatus.Cancelled){status=CaseStatus.Cancelled;}
        var diagnostic=reproduced
            ?"同じ失敗署名を再現しました。候補="+shrunk.Result.EvaluatedCandidateCount+" 入力="+shrunk.Result.OriginalInputElementCount+"→"+shrunk.Result.ShrunkInputElementCount+" 演算子="+shrunk.Result.OriginalQuery.ActualOperatorCount+"→"+shrunk.Result.ShrunkQuery.ActualOperatorCount
            :"縮小後のケースが元の失敗署名を再現しませんでした。";
        var metrics=new Dictionary<string,double>{
            ["evaluated-candidates"]=shrunk.Result.EvaluatedCandidateCount,
            ["original-input-count"]=shrunk.Result.OriginalInputElementCount,
            ["shrunk-input-count"]=shrunk.Result.ShrunkInputElementCount,
            ["original-operator-count"]=shrunk.Result.OriginalQuery.ActualOperatorCount,
            ["shrunk-operator-count"]=shrunk.Result.ShrunkQuery.ActualOperatorCount,
            ["timed-out"]=shrunk.Result.TimedOut?1:0,
        };
        return replayResult with{
            CaseId=item.Id,
            Status=status,
            Conditions=item.Conditions,
            ResultDigest=reproduced?shrunk.ExpectedFailure.ComputeHash():replayResult.ResultDigest,
            Diagnostic=diagnostic,
            Metrics=metrics,
        };
    }
}
