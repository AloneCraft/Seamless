using System.Security.Cryptography;
using Benchmark.Dynamic.Core;
using Verification.Core;

namespace Verification.Infrastructure;

public enum VerificationSuite {
    Changed,
    All,
    Benchmark,
    Scalable,
    Sweep,
    Dynamic,
}

public sealed record VerificationPlanRequest(
    VerificationSuite Suite=VerificationSuite.Changed,
    string Profile="quick",
    string BenchmarkTab="System.Array",
    string BenchmarkType="Int32",
    string BenchmarkMethod="Where_predicate",
    string Seed="1",
    int QueryCount=1,
    SourceTarget Source=SourceTarget.SystemArray,
    int InputCount=256,
    int OperatorCount=4,
    int MaximumDepth=4);

public sealed class VerificationWorkspace {
    private readonly VerificationRepository repository;

    public VerificationWorkspace(string repositoryRoot) {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        this.RepositoryRoot=Path.GetFullPath(repositoryRoot);
        this.DatabasePath=GetDatabasePath(this.RepositoryRoot);
        this.ArtifactRoot=GetArtifactRoot(this.RepositoryRoot);
        this.repository=new(this.DatabasePath);
        this.RecoveredInterruptedRuns=this.repository.RecoverInterruptedRuns(this.ArtifactRoot);
    }

    public string RepositoryRoot { get; }
    public string DatabasePath { get; }
    public string ArtifactRoot { get; }
    public int RecoveredInterruptedRuns { get; }
    public string? BaselineId=>this.repository.GetBaseline();

    public static string GetDatabasePath(string repositoryRoot)=>
        Path.Combine(Path.GetFullPath(repositoryRoot),"Work","Verification","Verification.db");

    public static string GetArtifactRoot(string repositoryRoot)=>
        Path.Combine(Path.GetFullPath(repositoryRoot),"Work","Verification","Artifacts");

    public static bool IsInitialized(string repositoryRoot)=>File.Exists(GetDatabasePath(repositoryRoot));

    public static void Initialize(string repositoryRoot)=>
        VerificationRepository.Initialize(GetDatabasePath(repositoryRoot));

    public VerificationPlan CreatePlan(VerificationPlanRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        return request.Suite switch {
            VerificationSuite.Changed=>RepositoryInspection.Plan(this.RepositoryRoot,request.Profile),
            VerificationSuite.All=>RepositoryInspection.Plan(this.RepositoryRoot,request.Profile,changedOnly:false),
            VerificationSuite.Benchmark=>VerificationProfiles.Benchmark(
                this.RepositoryRoot,request.Profile,request.BenchmarkTab,request.BenchmarkType,request.BenchmarkMethod),
            VerificationSuite.Scalable=>VerificationProfiles.Scalable(this.RepositoryRoot,request.Profile),
            VerificationSuite.Sweep=>VerificationProfiles.Sweep(this.RepositoryRoot,request.Profile),
            VerificationSuite.Dynamic=>VerificationProfiles.Dynamic(
                this.RepositoryRoot,
                request.Profile,
                request.Seed,
                request.QueryCount,
                request.Source,
                request.InputCount,
                request.OperatorCount,
                request.MaximumDepth),
            _=>throw new ArgumentOutOfRangeException(nameof(request),request.Suite,"Unknown verification suite."),
        };
    }

    public Task<VerificationRun> RunAsync(
        VerificationPlan plan,
        IProgress<VerificationProgress>? progress=null,
        string? parentId=null,
        CancellationToken cancellationToken=default)=>
        new VerificationRunner(this.repository,VerificationProfiles.Adapters())
            .RunAsync(plan,this.ArtifactRoot,progress,parentId,cancellationToken);

    public IReadOnlyList<VerificationRun> GetRuns(int limit=100)=>this.repository.GetRuns(limit);
    public VerificationRun GetRun(string runId)=>this.repository.GetRun(runId);
    public IReadOnlyList<CaseResult> GetResults(string runId)=>this.repository.GetResults(runId);
    public void SetBaseline(string runId)=>this.repository.SetBaseline(runId);

    public IReadOnlyList<ComparisonResult> Compare(string candidateRunId,string? baselineRunId=null) {
        baselineRunId??=this.repository.GetBaseline()
            ??throw new InvalidOperationException("比較基準が登録されていません。成功した実行を基準に設定してください。");
        var baseline=this.repository.GetResults(baselineRunId).ToDictionary(static result=>result.CaseId,StringComparer.Ordinal);
        var candidate=this.repository.GetResults(candidateRunId).ToDictionary(static result=>result.CaseId,StringComparer.Ordinal);
        var comparisons=new List<ComparisonResult>();
        foreach(var id in baseline.Keys.Union(candidate.Keys).Order(StringComparer.Ordinal)) {
            if(!baseline.TryGetValue(id,out var before)||!candidate.TryGetValue(id,out var after)) {
                comparisons.Add(new(id,ComparisonStatus.Incomplete,"基準または候補にケースがありません。"));
                continue;
            }
            comparisons.Add(VerificationComparison.Compare(before,after));
        }
        return comparisons;
    }

    public VerificationPlan CreateReplayPlan(string parentRunId,string? caseId=null) {
        var parent=this.repository.GetRun(parentRunId);
        var previous=this.repository.GetResults(parentRunId).ToDictionary(static result=>result.CaseId,StringComparer.Ordinal);
        var cases=parent.Plan.Cases.AsEnumerable();
        if(caseId is not null) {
            cases=cases.Where(item=>item.Id==caseId);
        } else {
            cases=cases.Where(item=>previous.TryGetValue(item.Id,out var result)&&result.Status!=CaseStatus.Passed);
        }
        return this.CreateChildPlan(parent,previous,cases,"再現するケースがありません。",requireSameSource:false);
    }

    public VerificationPlan CreateResumePlan(string parentRunId) {
        var parent=this.repository.GetRun(parentRunId);
        var currentFingerprint=RepositoryInspection.Fingerprint(this.RepositoryRoot);
        if(currentFingerprint!=parent.Plan.SourceFingerprint) {
            throw new InvalidOperationException("測定対象のソースが変わっています。resumeには同じソース状態が必要です。replayで新しいビルド上の再現を行ってください。");
        }
        var previous=this.repository.GetResults(parentRunId).ToDictionary(static result=>result.CaseId,StringComparer.Ordinal);
        var cases=parent.Plan.Cases.Where(item=>
            !previous.TryGetValue(item.Id,out var result)
            ||result.Status is CaseStatus.NotRun or CaseStatus.Running or CaseStatus.Cancelled or CaseStatus.Interrupted);
        return this.CreateChildPlan(parent,previous,cases,"再開する未完了ケースがありません。",requireSameSource:true);
    }

    public VerificationPlan CreateShrinkPlan(
        string parentRunId,
        string parentCaseId,
        DynamicShrinkOptions? options=null)=>
        DynamicShrinkPlanFactory.Create(
            this.repository,this.ArtifactRoot,this.RepositoryRoot,parentRunId,parentCaseId,options);

    private VerificationPlan CreateChildPlan(
        VerificationRun parent,
        IReadOnlyDictionary<string,CaseResult> previous,
        IEnumerable<VerificationCase> selected,
        string emptyMessage,
        bool requireSameSource) {
        var cases=selected.Select(item=>this.ExactReplayInput(item,previous.GetValueOrDefault(item.Id))).ToArray();
        if(cases.Length==0){throw new InvalidOperationException(emptyMessage);}
        var fingerprint=requireSameSource
            ?parent.Plan.SourceFingerprint
            :RepositoryInspection.Fingerprint(this.RepositoryRoot);
        return parent.Plan with {
            RepositoryRoot=this.RepositoryRoot,
            SourceFingerprint=fingerprint,
            Cases=cases,
        };
    }

    private VerificationCase ExactReplayInput(VerificationCase item,CaseResult? previous) {
        if(item.Adapter!="dynamic-worker"||previous?.Artifacts is null){return item;}
        var reference=previous.Artifacts.SingleOrDefault(static artifact=>
            artifact.RelativePath.EndsWith("/replay-request.json",StringComparison.Ordinal));
        if(reference is null){return item;}
        var root=Path.GetFullPath(this.ArtifactRoot)+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(
            this.ArtifactRoot,reference.RelativePath.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("再現成果物のパスが成果物領域外です。");
        }
        using(var input=File.OpenRead(path)) {
            var hash=Convert.ToHexString(SHA256.HashData(input));
            if(!hash.Equals(reference.Sha256,StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("再現成果物のハッシュが記録と一致しません: "+reference.RelativePath);
            }
        }
        return item with{Arguments=[File.ReadAllText(path)]};
    }
}
