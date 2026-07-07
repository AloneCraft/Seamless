using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class RunService:IRunService {
    private readonly IQueueService queueService;
    private readonly IWorkerProcessService workerService;
    private readonly IComparisonService comparisonService;

    public RunService(
        IQueueService queueService,
        IWorkerProcessService workerService,
        IComparisonService comparisonService) {
        this.queueService=queueService;
        this.workerService=workerService;
        this.comparisonService=comparisonService;
    }

    public async Task<RunReport> RunAsync(
        IReadOnlyList<BenchmarkCase> benchmarks,
        IReadOnlyList<int> logicalCpus,
        Func<IReadOnlyList<int>> logicalCpuProvider,
        IReadOnlyList<int> elementCounts,
        BenchmarkSourceTab sourceTab,
        BenchmarkExecutionProfile executionProfile,
        TimeSpan adaptiveThreshold,
        bool restartFromRequestedElementCount,
        string? runRootOverride,
        Func<IReadOnlyList<BenchmarkCase>>? benchmarkProvider,
        Action<BenchmarkResultItem> resultReceived,
        Action<string> statusChanged,
        BenchmarkRunExecutionOptions? executionOptions,
        CancellationToken cancellationToken) {
        var orchestrationRoot = FindSolutionRoot() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var benchmarkRoot = Path.Combine(orchestrationRoot,"Work",AppProfile.WorkRootName,BenchmarkExecutionProfileNames.ToFolderName(executionProfile));
        var jobRunner = this.CreateJobRunner(executionProfile,adaptiveThreshold,executionOptions);
        var orchestrator = new BenchmarkOrchestrator(
            new BenchmarkStateManager(benchmarkRoot),
            jobRunner);
        var request = new BenchmarkRunRequest(
            benchmarks,
            logicalCpus,
            elementCounts,
            adaptiveThreshold,
            TimeSpan.FromMinutes(60),
            logicalCpuProvider,
            RestartFromRequestedElementCount: restartFromRequestedElementCount,
            ExecutionProfile: executionProfile,
            RunRootOverride: runRootOverride,
            SourceTab: sourceTab,
            BenchmarkProvider: benchmarkProvider);
        try {
            var orchestratorResult = await orchestrator.RunAsync(
                request,
                update => resultReceived(new BenchmarkResultItem(update.Job)),
                statusChanged,
                cancellationToken);
            return new RunReport(orchestratorResult.RunRoot);
        } finally {
            if(jobRunner is IAsyncDisposable asyncDisposable) {
                await asyncDisposable.DisposeAsync();
            } else if(jobRunner is IDisposable disposable) {
                disposable.Dispose();
            }
        }

    }

    private IBenchmarkJobRunner CreateJobRunner(BenchmarkExecutionProfile executionProfile,TimeSpan testSleepDuration,BenchmarkRunExecutionOptions? executionOptions) =>
        executionProfile switch{
            BenchmarkExecutionProfile.Accurate => this.workerService,
            BenchmarkExecutionProfile.Intermediate => new PersistentWorkerProcessService(),
            BenchmarkExecutionProfile.Fast => new FastWorkerProcessService(),
            BenchmarkExecutionProfile.Test => new BenchmarkTestSleepJobRunner(testSleepDuration,executionOptions?.UseInfiniteTestRunner==true),
            _ => this.workerService,
        };

    private static string CreateRunRoot() {
        var root = FindSolutionRoot() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var path = Path.Combine(root,"Work","Benchmark",DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string? FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }
            directory=directory.Parent;
        }

        return null;
    }

}
