using System.Globalization;
using System.Text.Json;

namespace Benchmark;

#if TSAVORITE2_0BETA3
[global::Seamless.FLinq.LinqIntercept(false)]
#endif
internal sealed class BenchmarkRunPersistenceService {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented=true,
    };

    private readonly BenchmarkResultWriter resultWriter = new();

    public BenchmarkRunOption SaveManagedRun(string benchmarkRoot,BenchmarkExecutionProfile executionProfile,ProgressState sourceState) {
        var profileRoot = Path.Combine(benchmarkRoot,BenchmarkExecutionProfileNames.ToFolderName(executionProfile));
        var stateManager = new BenchmarkStateManager(profileRoot);
        var runRoot = stateManager.CreateNextRunRoot();
        var runNumber = BenchmarkStateManager.TryParseRunNumber(runRoot);
        var state = CloneForSave(sourceState,runRoot,runNumber,executionProfile,true);
        stateManager.Save(state);
        this.resultWriter.WriteOutputs(state);
        stateManager.SaveLatestRun(runRoot);
        return CreateRunOption(state);
    }

    public void ExportRun(string folder,ProgressState sourceState,bool overwrite) {
        if(!overwrite&&this.HasExportOutputs(folder,sourceState.SourceTab)) {
            throw new IOException("Export folder already contains benchmark output files.");
        }

        var state = CloneForSave(sourceState,folder,0,sourceState.ExecutionProfile,false);
        var stateManager = new BenchmarkStateManager(Path.GetDirectoryName(folder)??folder);
        stateManager.Save(state);
        this.resultWriter.WriteOutputs(state);
    }

    public ProgressState? ReadProgressState(string runRoot) {
        var path = Path.Combine(runRoot,BenchmarkStateManager.ProgressFileName);
        if(!File.Exists(path)) {
            return null;
        }

        return JsonSerializer.Deserialize<ProgressState>(File.ReadAllText(path),JsonOptions);
    }

    public bool HasExportOutputs(string folder,BenchmarkSourceTab sourceTab) {
        if(File.Exists(Path.Combine(folder,BenchmarkStateManager.ProgressFileName))
            ||File.Exists(Path.Combine(folder,"BenchmarkRunSummary.txt"))) {
            return true;
        }

        var sourceRoot = Path.Combine(folder,BenchmarkSourceTabs.Get(sourceTab).Id);
        return Directory.Exists(sourceRoot)
            &&Directory.EnumerateFiles(sourceRoot,"N*.csv",SearchOption.TopDirectoryOnly).Any();
    }

    public static BenchmarkRunOption CreateRunOption(ProgressState state) {
        var elementCounts = state.ElementCounts
            .Concat(state.Jobs.Select(static job => job.ElementCount))
            .Where(static elementCount => elementCount>0)
            .Distinct()
            .Order()
            .ToArray();
        var completedJobs = state.Jobs.Count(static job => job.Status==JobStatus.Completed);
        var totalJobs = state.Jobs.Count;
        return new BenchmarkRunOption(
            state.RunNumber,
            FormatRunDisplay(state.RunNumber,state.CreatedAt,completedJobs,totalJobs),
            state.RunRoot,
            state.ExecutionProfile,
            state.CreatedAt,
            state.UpdatedAt,
            completedJobs,
            totalJobs,
            elementCounts);
    }

    private static ProgressState CloneForSave(
        ProgressState source,
        string runRoot,
        int runNumber,
        BenchmarkExecutionProfile executionProfile,
        bool resetIdentity) {
        var now = DateTimeOffset.UtcNow;
        var state = new ProgressState {
            SchemaVersion=source.SchemaVersion,
            RunId=resetIdentity ? Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture) : source.RunId,
            Mode=source.Mode,
            ExecutionProfile=executionProfile,
            RunRoot=runRoot,
            RunNumber=runNumber,
            SourceTab=source.SourceTab,
            ElementCounts=source.ElementCounts.ToList(),
            AdaptiveThresholdNanoseconds=source.AdaptiveThresholdNanoseconds,
            Jobs=source.Jobs.Select(CloneJob).ToList(),
            AdaptiveStates=source.AdaptiveStates.Select(CloneAdaptiveState).ToList(),
            CreatedAt=resetIdentity ? now : source.CreatedAt,
            UpdatedAt=now,
        };
        foreach(var job in state.Jobs.Where(static job => job.Status is JobStatus.Running or JobStatus.Paused)) {
            job.Status=JobStatus.Pending;
            job.AssignedLogicalCpu=null;
            job.StartedAt=null;
            job.FinishedAt=null;
        }

        return state;
    }

    private static BenchmarkJob CloneJob(BenchmarkJob source) =>
        new() {
            JobId=source.JobId,
            Project=source.Project,
            AssemblyName=source.AssemblyName,
            TypeFullName=source.TypeFullName,
            MethodName=source.MethodName,
            SourceTab=source.SourceTab,
            ElementCount=source.ElementCount,
            PreparedElementCounts=source.PreparedElementCounts.ToList(),
            Status=source.Status,
            AssignedLogicalCpu=source.AssignedLogicalCpu,
            Mean=source.Mean,
            Error=source.Error,
            StdDev=source.StdDev,
            MeanNanoseconds=source.MeanNanoseconds,
            ErrorNanoseconds=source.ErrorNanoseconds,
            StdDevNanoseconds=source.StdDevNanoseconds,
            AllocatedBytes=source.AllocatedBytes,
            ExitCode=source.ExitCode,
            ArtifactFolder=source.ArtifactFolder,
            FailureReason=source.FailureReason,
            StartedAt=source.StartedAt,
            FinishedAt=source.FinishedAt,
        };

    private static AdaptiveBenchmarkState CloneAdaptiveState(AdaptiveBenchmarkState source) =>
        new() {
            MethodName=source.MethodName,
            IsStopped=source.IsStopped,
            IsCompleted=source.IsCompleted,
            NextElementCountIndex=source.NextElementCountIndex,
            StoppedAtElementCount=source.StoppedAtElementCount,
            Reason=source.Reason,
            TriggerProject=source.TriggerProject,
            TriggerMeanNanoseconds=source.TriggerMeanNanoseconds,
        };

    private static string FormatRunDisplay(int runNumber,DateTimeOffset createdAt,int completedJobs,int totalJobs) =>
        runNumber.ToString("D6",CultureInfo.InvariantCulture)+" "
        +createdAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm",CultureInfo.InvariantCulture)+" "
        +completedJobs.ToString(CultureInfo.InvariantCulture)+"/"+totalJobs.ToString(CultureInfo.InvariantCulture);
}
