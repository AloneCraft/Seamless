using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace UI;

public sealed class BenchmarkWorkerClient:IBenchmarkJobRunner {
    public Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        this.RunAsync(job.ToBenchmarkCase(),runRoot,job.SourceTab,job.PreparedElementCounts,job.ElementCount,job.LoopCount,"1",logicalCpu,timeout,cancellationToken);

    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkCase benchmark,
        string runRoot,
        BenchmarkSourceTab sourceTab,
        IReadOnlyList<int> preparedElementCounts,
        int elementCount,
        int loopCount,
        string elementScale,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeoutSource.Token);
        var effectiveToken = linkedSource.Token;
        try {
            await using var session = await this.StartSessionAsync(runRoot,logicalCpu,effectiveToken);
            var result = await session.RunAsync(benchmark,sourceTab,preparedElementCounts,elementCount,loopCount,elementScale,effectiveToken);
            await session.ShutdownAsync(effectiveToken);
            return result;
        } catch(OperationCanceledException) {
            if(timeoutSource.IsCancellationRequested&&!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException($"Benchmark job timed out after {timeout}.");
            }
            throw;
        }
    }

    public async Task<BenchmarkWorkerSession> StartSessionAsync(
        string runRoot,
        int logicalCpu,
        CancellationToken cancellationToken) {
        var workerPath = ResolveWorkerPath();
        var process = StartWorker(workerPath,runRoot,logicalCpu);
        System.Diagnostics.Debug.WriteLine($"[Worker] StartWorker pid={process.Id} cpu={logicalCpu} path={workerPath}");
        _=DrainStandardErrorAsync(process,logicalCpu);
        try {
            var ready = await process.StandardOutput.ReadLineAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[Worker] StartSessionAsync ready-line received: \"{ready}\" pid={process.Id}");
            if(!string.Equals(ready,"ready",StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Benchmark worker did not become ready. First output: {ready}");
            }

            return new BenchmarkWorkerSession(process,runRoot);
        } catch {
            KillProcess(process);
            process.Dispose();
            throw;
        }
    }

    internal static async Task DrainStandardErrorAsync(Process process,int logicalCpu) {
        try {
            while(await process.StandardError.ReadLineAsync() is { } line) {
                System.Diagnostics.Debug.WriteLine($"[Worker][stderr] cpu={logicalCpu} pid={process.Id}: {line}");
            }
        } catch {
        }
    }

    internal static string CreateArtifactFolder(string runRoot,BenchmarkCase benchmark,BenchmarkSourceTab sourceTab,int elementCount,int loopCount) {
        var artifactFolder = Path.Combine(
            runRoot,
            BenchmarkSourceTabs.ToPathPart(sourceTab),
            "N"+elementCount.ToString(CultureInfo.InvariantCulture),
            "Loop"+Math.Max(1,loopCount).ToString(CultureInfo.InvariantCulture),
            SanitizePathPart(benchmark.Project),
            SanitizePathPart(benchmark.TypeFullName),
            SanitizePathPart(benchmark.MethodName),
            string.IsNullOrWhiteSpace(benchmark.ArgumentKey) ? "default" : SanitizePathPart(benchmark.ArgumentKey));
        Directory.CreateDirectory(artifactFolder);
        return artifactFolder;
    }

    internal static Process StartWorker(string workerPath,string runRoot,int logicalCpu) {
        var isDll = string.Equals(Path.GetExtension(workerPath),".dll",StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo {
            FileName=isDll?"dotnet":workerPath,
            UseShellExecute=false,
            RedirectStandardInput=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,
            StandardErrorEncoding=Encoding.UTF8,
            CreateNoWindow=true,
        };

        if(isDll) {
            startInfo.ArgumentList.Add(workerPath);
        }

        startInfo.ArgumentList.Add("--logical-cpu");
        startInfo.ArgumentList.Add(logicalCpu.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--run-root");
        startInfo.ArgumentList.Add(runRoot);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Worker.");
    }

    internal static string ResolveWorkerPath() {
        var baseDirectory = AppContext.BaseDirectory;
        var exePath = Path.Combine(baseDirectory,"Worker.exe");
        if(File.Exists(exePath)) {
            return exePath;
        }

        var dllPath = Path.Combine(baseDirectory,"Worker.dll");
        if(File.Exists(dllPath)) {
            return dllPath;
        }

        throw new FileNotFoundException("Worker was not found in the application output directory.",exePath);
    }

    internal static string SanitizePathPart(string text) {
        var builder = new StringBuilder(text.Length);
        foreach(var ch in text) {
            builder.Append(Path.GetInvalidFileNameChars().Contains(ch)?'_':ch);
        }

        return builder.ToString();
    }

    internal static void KillProcess(Process process) {
        try {
            if(!process.HasExited) {
                process.Kill(entireProcessTree:true);
            }
        } catch(InvalidOperationException) {
        } catch(System.ComponentModel.Win32Exception) {
        }
    }
}

public sealed class BenchmarkFastWorkerSession:IAsyncDisposable {
    private readonly Process process;
    private readonly string runRoot;
    private readonly SemaphoreSlim writeGate = new(1,1);
    private readonly ConcurrentDictionary<string,TaskCompletionSource<WorkerResultMessage>> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource closedSource = new();
    private readonly Task readLoop;
    private bool isClosed;

    internal BenchmarkFastWorkerSession(Process process,string runRoot) {
        this.process=process;
        this.runRoot=runRoot;
        this.readLoop=Task.Run(this.ReadLoopAsync);
    }

    public static async Task<BenchmarkFastWorkerSession> StartAsync(string runRoot,CancellationToken cancellationToken) {
        var workerPath = BenchmarkWorkerClient.ResolveWorkerPath();
        var process = BenchmarkWorkerClient.StartWorker(workerPath,runRoot,-1);
        System.Diagnostics.Debug.WriteLine($"[Worker] StartWorker(fast) pid={process.Id} path={workerPath}");
        _=BenchmarkWorkerClient.DrainStandardErrorAsync(process,-1);
        try {
            var ready = await process.StandardOutput.ReadLineAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[Worker] StartAsync(fast) ready-line received: \"{ready}\" pid={process.Id}");
            if(!string.Equals(ready,"ready",StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Benchmark fast worker did not become ready. First output: {ready}");
            }

            return new BenchmarkFastWorkerSession(process,runRoot);
        } catch {
            BenchmarkWorkerClient.KillProcess(process);
            process.Dispose();
            throw;
        }
    }

    public async Task<BenchmarkJobRunResult> RunAsync(BenchmarkJob job,int logicalCpu,CancellationToken cancellationToken) {
        if(this.isClosed) {
            throw new ObjectDisposedException(nameof(BenchmarkFastWorkerSession));
        }

        var benchmark = job.ToBenchmarkCase();
        var loopCount = Math.Max(1,job.LoopCount);
        var artifactFolder = BenchmarkWorkerClient.CreateArtifactFolder(this.runRoot,benchmark,job.SourceTab,job.ElementCount,loopCount);
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
                job.RunItemIndex,
                result.DiagnosticCounters);
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
        BenchmarkWorkerClient.KillProcess(this.process);
        foreach(var pair in this.pending.ToArray()) {
            if(this.pending.TryRemove(pair.Key,out var completion)) {
                completion.TrySetCanceled();
            }
        }
    }
}

public sealed class BenchmarkWorkerSession:IAsyncDisposable {
    private readonly Process process;
    private readonly string runRoot;
    private bool isClosed;

    internal BenchmarkWorkerSession(Process process,string runRoot) {
        this.process=process;
        this.runRoot=runRoot;
    }

    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkCase benchmark,
        BenchmarkSourceTab sourceTab,
        IReadOnlyList<int> preparedElementCounts,
        int elementCount,
        int loopCount,
        string elementScale,
        CancellationToken cancellationToken) {
        if(this.isClosed) {
            throw new ObjectDisposedException(nameof(BenchmarkWorkerSession));
        }

        var artifactFolder = BenchmarkWorkerClient.CreateArtifactFolder(this.runRoot,benchmark,sourceTab,elementCount,loopCount);
        var message = new WorkerTaskMessage(
            "run",
            Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            benchmark.AssemblyName,
            benchmark.TypeFullName,
            benchmark.MethodName,
            benchmark.ArgumentKey,
            benchmark.Arguments.ToArray(),
            sourceTab,
            preparedElementCounts.ToArray(),
            elementCount,
            loopCount,
            elementScale,
            artifactFolder);

        try {
            System.Diagnostics.Debug.WriteLine($"[Worker] RunAsync send pid={this.process.Id} method={benchmark.MethodName} elements={elementCount}");
            await this.process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(),cancellationToken);
            await this.process.StandardInput.FlushAsync(cancellationToken);

            var line = await this.process.StandardOutput.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("Benchmark worker closed without a result.");
            System.Diagnostics.Debug.WriteLine($"[Worker] RunAsync received pid={this.process.Id} method={benchmark.MethodName} line-length={line.Length}");
            var result = JsonSerializer.Deserialize<WorkerResultMessage>(line)
                ?? throw new InvalidOperationException("Benchmark worker returned an empty result.");
            return new BenchmarkJobRunResult(
                benchmark.Project,
                benchmark.MethodName,
                benchmark.TypeFullName,
                elementCount,
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
                DiagnosticCounterValues: result.DiagnosticCounters);
        } catch(OperationCanceledException) {
            this.Kill();
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
                await this.process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new WorkerTaskMessage("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,[],BenchmarkSourceTab.SystemArray,[],0,0,string.Empty,string.Empty)).AsMemory(),cancellationToken);
                await this.process.StandardInput.FlushAsync(cancellationToken);
                await this.process.WaitForExitAsync(cancellationToken);
            }
        } catch {
            this.Kill();
            throw;
        } finally {
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
        }
    }

    private void Kill() {
        this.isClosed=true;
        BenchmarkWorkerClient.KillProcess(this.process);
    }
}

public sealed record BenchmarkRunResult(
    string Project,
    string Method,
    string TypeFullName,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    double? MeanNanoseconds,
    double? ErrorNanoseconds,
    double? StdDevNanoseconds,
    long? AllocatedBytes,
    int ExitCode,
    string ArtifactFolder,
    string Log,
    int LoopCount = 1,
    int? RandomSeed = null,
    string ResultDigest = "");

internal sealed record WorkerTaskMessage(
    string Command,
    string TaskId,
    string AssemblyName,
    string TypeFullName,
    string MethodName,
    string ArgumentKey,
    BenchmarkArgument[] Arguments,
    BenchmarkSourceTab SourceTab,
    int[] PreparedElementCounts,
    int ElementCount,
    int LoopCount,
    string ElementScale,
    string ArtifactFolder,
    int? AssignedLogicalCpu = null);

internal sealed record WorkerResultMessage(
    string TaskId,
    string Status,
    string Mean,
    string Error,
    string StdDev,
    double? MeanNanoseconds,
    double? ErrorNanoseconds,
    double? StdDevNanoseconds,
    long? AllocatedBytes,
    int ExitCode,
    string ArtifactFolder,
    string Log,
    int? RandomSeed = null,
    string ResultDigest = "",
    IReadOnlyList<BenchmarkDiagnosticCounter>? DiagnosticCounters = null);
