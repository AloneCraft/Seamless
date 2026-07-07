using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class WorkerProcessService:IWorkerProcessService {
    private readonly WorkerClient client = new();

    public Task<BenchmarkJobRunResult> RunAsync(BenchmarkJob job,string runRoot,int logicalCpu,TimeSpan timeout,CancellationToken cancellationToken) =>
        this.client.RunAsync(job,runRoot,logicalCpu,timeout,cancellationToken);

    public async Task<RunResult> RunAsync(BenchmarkCase benchmark,string runRoot,BenchmarkSourceTab sourceTab,IReadOnlyList<int> preparedElementCounts,string scale,int logicalCpu,CancellationToken cancellationToken) {
        var result = await this.client.RunAsync(benchmark,runRoot,sourceTab,preparedElementCounts,0,1,scale,logicalCpu,TimeSpan.FromMinutes(60),cancellationToken);
        return new RunResult(
            result.Project,
            result.Method,
            result.TypeFullName,
            result.Status,
            result.Mean,
            result.Error,
            result.StdDev,
            result.MeanNanoseconds,
            result.ErrorNanoseconds,
            result.StdDevNanoseconds,
            result.AllocatedBytes,
            result.ExitCode,
            result.ArtifactFolder,
            result.Log);
    }
}
