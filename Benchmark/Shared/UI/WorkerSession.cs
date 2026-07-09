using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace UI;
public sealed class WorkerSession:IAsyncDisposable {
    private readonly Process process;
    private readonly string runRoot;
    private bool isClosed;

    internal WorkerSession(Process process,string runRoot) {
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
            throw new ObjectDisposedException(nameof(WorkerSession));
        }

        var artifactFolder = WorkerClient.CreateArtifactFolder(this.runRoot,benchmark,sourceTab,elementCount,loopCount);
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
                loopCount);
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
        WorkerClient.KillProcess(this.process);
    }
}
