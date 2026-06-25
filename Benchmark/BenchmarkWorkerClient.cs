using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Benchmark;

public sealed class BenchmarkWorkerClient {
    public async Task<BenchmarkRunResult> RunAsync(BenchmarkCase benchmark,string runRoot,string elementScale,int logicalCpu,CancellationToken cancellationToken) {
        var workerPath = ResolveWorkerPath();
        var artifactFolder = Path.Combine(
            runRoot,
            SanitizePathPart(benchmark.Project),
            SanitizePathPart(benchmark.MethodName));
        Directory.CreateDirectory(artifactFolder);

        using var process = StartWorker(workerPath,runRoot,logicalCpu);
        WorkerResultMessage result;
        try {
            var ready = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if(!string.Equals(ready,"ready",StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Benchmark worker did not become ready. First output: {ready}");
            }

            var message = new WorkerTaskMessage(
                "run",
                Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
                benchmark.AssemblyName,
                benchmark.TypeFullName,
                benchmark.MethodName,
                elementScale,
                artifactFolder);

            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(),cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);

            var line = await process.StandardOutput.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("Benchmark worker closed without a result.");
            result = JsonSerializer.Deserialize<WorkerResultMessage>(line)
                ?? throw new InvalidOperationException("Benchmark worker returned an empty result.");

            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new WorkerTaskMessage("shutdown",string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty)).AsMemory(),cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
        } catch(OperationCanceledException) {
            KillProcess(process);
            throw;
        }

        return new BenchmarkRunResult(
            benchmark.Project,
            benchmark.MethodName,
            benchmark.TypeFullName,
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

    private static Process StartWorker(string workerPath,string runRoot,int logicalCpu) {
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

    private static string ResolveWorkerPath() {
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

    private static string SanitizePathPart(string text) {
        var builder = new StringBuilder(text.Length);
        foreach(var ch in text) {
            builder.Append(Path.GetInvalidFileNameChars().Contains(ch)?'_':ch);
        }

        return builder.ToString();
    }

    private static void KillProcess(Process process) {
        try {
            if(!process.HasExited) {
                process.Kill(entireProcessTree:true);
            }
        } catch(InvalidOperationException) {
        } catch(System.ComponentModel.Win32Exception) {
        }
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
