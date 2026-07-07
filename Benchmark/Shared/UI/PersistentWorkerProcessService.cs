using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace UI;
public sealed class PersistentWorkerProcessService:IBenchmarkJobRunner,IBenchmarkDynamicCpuSetRunner,IAsyncDisposable {
    private readonly WorkerClient client = new();
    private readonly ConcurrentDictionary<int,WorkerSession> sessions = new();
    private readonly ConcurrentDictionary<int,int> activeJobs = new();

    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        var session = await this.GetSessionAsync(runRoot,logicalCpu,cancellationToken);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeoutSource.Token);
        this.activeJobs.AddOrUpdate(logicalCpu,1,static (_,count) => count+1);
        try {
            var result = await session.RunAsync(job.ToBenchmarkCase(),job.SourceTab,job.PreparedElementCounts,job.ElementCount,job.LoopCount,"1",linkedSource.Token);
            return result with { RunItemIndex=job.RunItemIndex };
        } catch(OperationCanceledException) {
            await this.ResetSessionAsync(logicalCpu,session);
            if(timeoutSource.IsCancellationRequested&&!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException($"Benchmark job timed out after {timeout}.");
            }

            throw;
        } catch {
            await this.ResetSessionAsync(logicalCpu,session);
            throw;
        } finally {
            this.activeJobs.AddOrUpdate(logicalCpu,0,static (_,count) => Math.Max(0,count-1));
        }
    }

    public async Task ApplyCpuSetAsync(IReadOnlyList<int> logicalCpus) {
        var active = logicalCpus.ToHashSet();
        foreach(var pair in this.sessions.ToArray()) {
            if(active.Contains(pair.Key)||this.activeJobs.GetValueOrDefault(pair.Key)>0) {
                continue;
            }

            await this.ResetSessionAsync(pair.Key,pair.Value);
        }
    }

    public async ValueTask DisposeAsync() {
        var values = this.sessions.Values.ToArray();
        this.sessions.Clear();
        foreach(var session in values) {
            await session.DisposeAsync();
        }
    }

    private async Task<WorkerSession> GetSessionAsync(
        string runRoot,
        int logicalCpu,
        CancellationToken cancellationToken) {
        if(this.sessions.TryGetValue(logicalCpu,out var existing)) {
            return existing;
        }

        var created = await this.client.StartSessionAsync(runRoot,logicalCpu,cancellationToken);
        if(this.sessions.TryAdd(logicalCpu,created)) {
            return created;
        }

        await created.DisposeAsync();
        return this.sessions[logicalCpu];
    }

    private async Task ResetSessionAsync(int logicalCpu,WorkerSession session) {
        if(this.sessions.TryGetValue(logicalCpu,out var current)
            &&ReferenceEquals(current,session)
            &&this.sessions.TryRemove(logicalCpu,out _)) {
            await session.DisposeAsync();
        }
    }
}
