using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Benchmark;

public sealed class BenchmarkWorkerClient:IBenchmarkJobRunner {
    public Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkJob job,
        string runRoot,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        this.RunAsync(job.ToBenchmarkCase(),runRoot,job.SourceTab,job.PreparedElementCounts,job.ElementCount,"1",logicalCpu,timeout,cancellationToken);

    public async Task<BenchmarkJobRunResult> RunAsync(
        BenchmarkCase benchmark,
        string runRoot,
        BenchmarkSourceTab sourceTab,
        IReadOnlyList<int> preparedElementCounts,
        int elementCount,
        string elementScale,
        int logicalCpu,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeoutSource.Token);
        var effectiveToken = linkedSource.Token;
        try {
            await using var session = await this.StartSessionAsync(runRoot,logicalCpu,effectiveToken);
            var result = await session.RunAsync(benchmark,sourceTab,preparedElementCounts,elementCount,elementScale,effectiveToken);
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
        try {
            var ready = await process.StandardOutput.ReadLineAsync(cancellationToken);
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

    internal static string CreateArtifactFolder(string runRoot,BenchmarkCase benchmark,BenchmarkSourceTab sourceTab,int elementCount) {
        var artifactFolder = Path.Combine(
            runRoot,
            BenchmarkSourceTabs.ToPathPart(sourceTab),
            "N"+elementCount.ToString(CultureInfo.InvariantCulture),
            SanitizePathPart(benchmark.Project),
            SanitizePathPart(benchmark.MethodName));
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
            ?? throw new InvalidOperationException("Could not start Benchmark.Worker.");
    }

    internal static string ResolveWorkerPath() {
        var baseDirectory = AppContext.BaseDirectory;
        var exePath = Path.Combine(baseDirectory,"Benchmark.Worker.exe");
        if(File.Exists(exePath)) {
            return exePath;
        }

        var dllPath = Path.Combine(baseDirectory,"Benchmark.Worker.dll");
        if(File.Exists(dllPath)) {
            return dllPath;
        }

        throw new FileNotFoundException("Benchmark.Worker was not found in the application output directory.",exePath);
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
        string elementScale,
        CancellationToken cancellationToken) {
        if(this.isClosed) {
            throw new ObjectDisposedException(nameof(BenchmarkWorkerSession));
        }

        var artifactFolder = BenchmarkWorkerClient.CreateArtifactFolder(this.runRoot,benchmark,sourceTab,elementCount);
        var message = new WorkerTaskMessage(
            "run",
            Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            benchmark.AssemblyName,
            benchmark.TypeFullName,
            benchmark.MethodName,
            sourceTab,
            preparedElementCounts.ToArray(),
            elementCount,
            elementScale,
            artifactFolder);

        try {
            await this.process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(),cancellationToken);
            await this.process.StandardInput.FlushAsync(cancellationToken);

            var line = await this.process.StandardOutput.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("Benchmark worker closed without a result.");
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
                result.Log);
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
                await this.process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new WorkerTaskMessage("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,BenchmarkSourceTab.SystemArray,[],0,string.Empty,string.Empty)).AsMemory(),cancellationToken);
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
    string Log);

internal sealed record WorkerTaskMessage(
    string Command,
    string TaskId,
    string AssemblyName,
    string TypeFullName,
    string MethodName,
    BenchmarkSourceTab SourceTab,
    int[] PreparedElementCounts,
    int ElementCount,
    string ElementScale,
    string ArtifactFolder);

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
    string Log);
