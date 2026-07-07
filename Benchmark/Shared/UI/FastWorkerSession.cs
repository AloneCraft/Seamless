using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace UI;
public sealed class FastWorkerSession:IAsyncDisposable {
    private readonly Process process;
    private readonly string runRoot;
    private readonly SemaphoreSlim writeGate = new(1,1);
    private readonly ConcurrentDictionary<string,TaskCompletionSource<WorkerResultMessage>> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource closedSource = new();
    private readonly Task readLoop;
    private bool isClosed;

    internal FastWorkerSession(Process process,string runRoot) {
        this.process=process;
        this.runRoot=runRoot;
        this.readLoop=Task.Run(this.ReadLoopAsync);
    }

    public static async Task<FastWorkerSession> StartAsync(string runRoot,CancellationToken cancellationToken) {
        var workerPath = WorkerClient.ResolveWorkerPath();
        var process = WorkerClient.StartWorker(workerPath,runRoot,-1);
        Trace.WriteLine($"[Worker] StartWorker(fast) pid={process.Id} path={workerPath}");
        _=WorkerClient.DrainStandardErrorAsync(process,-1);
        try {
            var ready = await process.StandardOutput.ReadLineAsync(cancellationToken);
            Trace.WriteLine($"[Worker] StartAsync(fast) ready-line received: \"{ready}\" pid={process.Id}");
            if(!string.Equals(ready,"ready",StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Benchmark fast worker did not become ready. First output: {ready}");
            }

            return new FastWorkerSession(process,runRoot);
        } catch {
            WorkerClient.KillProcess(process);
            process.Dispose();
            throw;
        }
    }

    public async Task<BenchmarkJobRunResult> RunAsync(BenchmarkJob job,int logicalCpu,CancellationToken cancellationToken) {
        if(this.isClosed) {
            throw new ObjectDisposedException(nameof(FastWorkerSession));
        }

        var benchmark = job.ToBenchmarkCase();
        var loopCount = Math.Max(1,job.LoopCount);
        var artifactFolder = WorkerClient.CreateArtifactFolder(this.runRoot,benchmark,job.SourceTab,job.ElementCount,loopCount);
        var taskId = Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture);
        var completion = new TaskCompletionSource<WorkerResultMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!this.pending.TryAdd(taskId,completion)) {
            throw new InvalidOperationException("Duplicate fast worker task id.");
        }

        var message = new WorkerTaskMessage(
            "fast-run",
            taskId,
            benchmark.AssemblyName,
            benchmark.TypeFullName,
            benchmark.MethodName,
            benchmark.ArgumentKey,
            benchmark.Arguments.ToArray(),
            job.SourceTab,
            job.PreparedElementCounts.ToArray(),
            job.ElementCount,
            loopCount,
            "1",
            artifactFolder,
            logicalCpu);
        using var registration = cancellationToken.Register(() => {
            this.Kill();
            completion.TrySetCanceled(cancellationToken);
        });
        try {
            await this.writeGate.WaitAsync(cancellationToken);
            try {
                await this.process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(),cancellationToken);
                await this.process.StandardInput.FlushAsync(cancellationToken);
            } finally {
                this.writeGate.Release();
            }

            var result = await completion.Task.WaitAsync(cancellationToken);
            return new BenchmarkJobRunResult(
                benchmark.Project,
                benchmark.MethodName,
                benchmark.TypeFullName,
                job.ElementCount,
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
                result.Log,
                result.RandomSeed,
                result.ResultDigest,
                benchmark.ArgumentKey,
                benchmark.Arguments,
                loopCount,
                job.RunItemIndex);
        } catch {
            this.pending.TryRemove(taskId,out _);
            throw;
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken) {
        if(this.isClosed) {
            return;
        }

        this.isClosed=true;
        try {
            if(!this.process.HasExited) {
                await this.writeGate.WaitAsync(cancellationToken);
                try {
                    await this.process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new WorkerTaskMessage("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,[],BenchmarkSourceTab.SystemArray,[],0,0,string.Empty,string.Empty)).AsMemory(),cancellationToken);
                    await this.process.StandardInput.FlushAsync(cancellationToken);
                } finally {
                    this.writeGate.Release();
                }
                await this.process.WaitForExitAsync(cancellationToken);
            }
        } catch {
            this.Kill();
            throw;
        } finally {
            this.closedSource.Cancel();
            await this.CompleteReadLoopAsync();
            this.process.Dispose();
        }
    }

    public async ValueTask DisposeAsync() {
        if(this.isClosed) {
            this.process.Dispose();
            return;
        }

        using var shutdownSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            await this.ShutdownAsync(shutdownSource.Token);
        } catch {
            this.Kill();
            this.process.Dispose();
        } finally {
            this.closedSource.Dispose();
            this.writeGate.Dispose();
        }
    }

    private async Task ReadLoopAsync() {
        try {
            while(!this.closedSource.IsCancellationRequested&&await this.process.StandardOutput.ReadLineAsync(this.closedSource.Token) is { } line) {
                if(string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                var result = JsonSerializer.Deserialize<WorkerResultMessage>(line);
                if(result is null||string.IsNullOrWhiteSpace(result.TaskId)) {
                    continue;
                }

                if(this.pending.TryRemove(result.TaskId,out var completion)) {
                    completion.TrySetResult(result);
                }
            }
        } catch(OperationCanceledException) {
        } catch(Exception ex) {
            foreach(var pair in this.pending.ToArray()) {
                if(this.pending.TryRemove(pair.Key,out var completion)) {
                    completion.TrySetException(ex);
                }
            }
        }
    }

    private async Task CompleteReadLoopAsync() {
        try {
            await this.readLoop.WaitAsync(TimeSpan.FromSeconds(2));
        } catch {
        }
    }

    private void Kill() {
        this.isClosed=true;
        this.closedSource.Cancel();
        WorkerClient.KillProcess(this.process);
        foreach(var pair in this.pending.ToArray()) {
            if(this.pending.TryRemove(pair.Key,out var completion)) {
                completion.TrySetCanceled();
            }
        }
    }
}
