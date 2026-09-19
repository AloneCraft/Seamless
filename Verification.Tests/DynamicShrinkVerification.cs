using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Benchmark.Dynamic.Core;
using Verification.Core;
using Verification.Infrastructure;

namespace Verification.Tests;

public sealed class DynamicShrinkVerification{
    [Fact]
    public async Task 同じ失敗署名を保つ候補だけを採用して再現要求を作る(){
        var query=CreateQuery();
        var request=CreateRequest(query);
        var execution=CreateExecution(WorkerExecutionStatus.SemanticMismatch,MismatchResult);
        var input=new DynamicShrinkInput(
            "parent-run",
            "parent-case",
            JsonSerializer.Serialize(request,DynamicWorkerProtocol.JsonOptions),
            JsonSerializer.Serialize(execution,DynamicWorkerProtocol.JsonOptions),
            new(128,5_000));
        var engine=new DynamicShrinkEngine(new CountFailureEvaluator());

        var result=await engine.ShrinkAsync(input,"unused-worker","unused-root");

        Assert.Equal(QueryTerminalKind.Count,result.Result.ShrunkQuery.Root.Arguments.Kind);
        Assert.Equal(1,result.Result.ShrunkQuery.ActualOperatorCount);
        Assert.Equal(1,result.Result.ShrunkInputElementCount);
        Assert.NotEmpty(result.Evaluations);
        Assert.Contains(result.Evaluations,static evaluation=>evaluation.MatchesExpectedFailure);
        Assert.Equal(1,result.ShrunkRequest.QueryCount);
        Assert.Equal(1,result.ShrunkRequest.InputElementCount);
        Assert.Single(result.ShrunkRequest.ReplayQueries!);
        Assert.Equal("Shrink",result.ShrunkRequest.ReplayQueries![0].DerivationKind);
    }

    [Fact]
    public void 記録済みhashを検証して親失敗から縮小計画を作る(){
        var repositoryRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        var artifactRoot=Path.Combine(Path.GetTempPath(),"seamless-shrink-plan-"+Guid.NewGuid().ToString("N"));
        try{
            var caseDirectory=Path.Combine(artifactRoot,"parent-run","0000");
            Directory.CreateDirectory(caseDirectory);
            var replayJson=JsonSerializer.Serialize(CreateRequest(CreateQuery()),DynamicWorkerProtocol.JsonOptions);
            var executionJson=JsonSerializer.Serialize(CreateExecution(WorkerExecutionStatus.SemanticMismatch,MismatchResult),DynamicWorkerProtocol.JsonOptions);
            var replayPath=Path.Combine(caseDirectory,"replay-request.json");
            var executionPath=Path.Combine(caseDirectory,"execution.json");
            File.WriteAllText(replayPath,replayJson);
            File.WriteAllText(executionPath,executionJson);
            var conditions=new ComparisonConditions("dynamic-v1","input","dotnet-v1","ir-v1","worker-v1","env","cpu","Release");
            var item=new VerificationCase(
                "parent-case","parent","クエリの正しさ検証","dynamic-worker","Benchmark/Benchmark.Dynamic.Worker/Benchmark.Dynamic.Worker.csproj",
                ["build","Benchmark/Benchmark.Dynamic.Worker/Benchmark.Dynamic.Worker.csproj","-c","Release"],
                [replayJson],conditions,"test");
            var parent=new VerificationRun("parent-run",null,DateTimeOffset.UtcNow,CaseStatus.Failed,new(1,repositoryRoot,"source","quick",300,[item]));
            var result=new CaseResult(
                item.Id,CaseStatus.Failed,conditions,[],null,null,"SemanticMismatch",Artifacts:[Reference(artifactRoot,replayPath),Reference(artifactRoot,executionPath)]);
            var repository=new MemoryRepository(parent,result);

            var plan=DynamicShrinkPlanFactory.Create(repository,artifactRoot,repositoryRoot,parent.Id,item.Id,new(16,1_000));

            var shrinkCase=Assert.Single(plan.Cases);
            Assert.Equal("shrink",plan.Profile);
            Assert.Equal("dynamic-shrink",shrinkCase.Adapter);
            Assert.StartsWith("dynamic-shrink-v1",shrinkCase.Conditions.CaseVersion,StringComparison.Ordinal);
            Assert.Equal(parent.Id,JsonSerializer.Deserialize<DynamicShrinkInput>(shrinkCase.Arguments[0],DynamicWorkerProtocol.JsonOptions)!.ParentRunId);

            File.AppendAllText(executionPath,"tampered");
            Assert.Throws<InvalidDataException>(()=>DynamicShrinkPlanFactory.Create(repository,artifactRoot,repositoryRoot,parent.Id,item.Id));
        }finally{
            if(Directory.Exists(artifactRoot)){Directory.Delete(artifactRoot,true);}
        }
    }

    private const string MismatchResult="""{"comparisons":{"fLinq":{"isMatch":false,"difference":"index=0"}}}""";

    private static QueryIrDocument CreateQuery(){
        return new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0x7777"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=4,
            MaximumDepth=4,
            InputElementCount=64,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.Distinct,QueryOperatorIds.ToArray],
        });
    }

    private static WorkerRunRequest CreateRequest(QueryIrDocument query){
        var seed=Seed256.Parse("0x7777");
        var replay=new WorkerReplayQuery(0,QueryIrJson.Serialize(query),"parent-query",SeedDerivation.Derive(seed,"fixture",0),0,"Replay");
        return new(
            "parent-run",
            seed,
            1,
            query.ActualOperatorCount,
            query.ActualDepth,
            64,
            OperatorSetMode.Common,
            [SourceTarget.SystemArray],
            new(),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [replay]);
    }

    private static WorkerSourceExecutionCompletedEvent CreateExecution(WorkerExecutionStatus status,string? result){
        return new(
            "parent-run","parent-query",0,SourceTarget.SystemArray,status,string.Empty,string.Empty,result,null,null,null,true,DateTimeOffset.UtcNow);
    }

    private static ArtifactReference Reference(string root,string path){
        using var input=File.OpenRead(path);
        return new(
            Path.GetRelativePath(root,path).Replace('\\','/'),
            Convert.ToHexString(SHA256.HashData(input)),
            input.Length);
    }

    private sealed class CountFailureEvaluator:IDynamicCandidateEvaluator{
        public Task<DynamicCandidateObservation> EvaluateAsync(
            WorkerRunRequest request,
            string workerPath,
            string repositoryRoot,
            CancellationToken cancellationToken){
            var query=QueryIrJson.Deserialize(request.ReplayQueries!.Single().IrJson);
            var status=query.Root.Arguments.Kind==QueryTerminalKind.Count
                ?WorkerExecutionStatus.SemanticMismatch
                :WorkerExecutionStatus.Succeeded;
            var result=status==WorkerExecutionStatus.SemanticMismatch?MismatchResult:null;
            return Task.FromResult(new DynamicCandidateObservation(CreateExecution(status,result),null,string.Empty));
        }
    }

    private sealed class MemoryRepository(VerificationRun run,CaseResult result):IVerificationRepository{
        public void CreateRun(VerificationRun value)=>throw new NotSupportedException();
        public void SaveResult(string runId,CaseResult value)=>throw new NotSupportedException();
        public void CompleteRun(string runId,CaseStatus status)=>throw new NotSupportedException();
        public VerificationRun GetRun(string runId)=>run;
        public IReadOnlyList<VerificationRun> GetRuns(int limit=100)=>[run];
        public IReadOnlyList<CaseResult> GetResults(string runId)=>[result];
        public void SetBaseline(string runId)=>throw new NotSupportedException();
        public string? GetBaseline()=>null;
    }
}
