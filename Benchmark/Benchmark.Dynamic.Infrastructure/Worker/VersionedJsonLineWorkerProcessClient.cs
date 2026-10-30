using Benchmark.Dynamic.Core;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

[assembly:InternalsVisibleTo("Benchmark.Dynamic.Tests")]

namespace Benchmark.Dynamic.Infrastructure;

public interface IWorkerProcessClient{
    IWorkerProcessSession Start(
        string workerPath,
        IReadOnlyList<string>? arguments=null,
        string? workingDirectory=null);
}

public interface IWorkerProcessSession:IAsyncDisposable{
    int ProcessID{get;}
    bool HasExited{get;}
    Task SendAsync<T>(
        string requestID,
        string messageType,
        T payload,
        CancellationToken cancellationToken=default);
    IAsyncEnumerable<WorkerMessageEnvelope> ReadAllAsync(CancellationToken cancellationToken=default);
    Task<bool> CancelAsync(string requestID);
    Task<bool> ShutdownAsync(string requestID);
    Task<string> GetStandardErrorAsync();
}

public sealed class VersionedJsonLineWorkerProcessClient:IWorkerProcessClient{
    public WorkerProcessSession Start(
        string workerPath,
        IReadOnlyList<string>? arguments=null,
        string? workingDirectory=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(workerPath);
        var fullPath=Path.GetFullPath(workerPath);
        if(!File.Exists(fullPath)){
            throw new FileNotFoundException("Worker executable was not found.",fullPath);
        }

        var startInfo=CreateStartInfo(fullPath,workingDirectory);
        foreach(var argument in arguments??[]){
            startInfo.ArgumentList.Add(argument);
        }

        var process=new Process{
            StartInfo=startInfo,
            EnableRaisingEvents=true,
        };
        try{
            if(!process.Start()){
                throw new InvalidOperationException($"Worker process did not start: {fullPath}");
            }
            return new WorkerProcessSession(process);
        }catch{
            process.Dispose();
            throw;
        }
    }

    IWorkerProcessSession IWorkerProcessClient.Start(
        string workerPath,
        IReadOnlyList<string>? arguments,
        string? workingDirectory){
        return this.Start(workerPath,arguments,workingDirectory);
    }

    public static ProcessStartInfo CreateStartInfo(string workerPath,string? workingDirectory=null){
        var isDll=string.Equals(Path.GetExtension(workerPath),".dll",StringComparison.OrdinalIgnoreCase);
        var startInfo=new ProcessStartInfo{
            FileName=isDll?"dotnet":workerPath,
            WorkingDirectory=string.IsNullOrWhiteSpace(workingDirectory)
                ?Path.GetDirectoryName(workerPath)??Environment.CurrentDirectory
                :Path.GetFullPath(workingDirectory),
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardInput=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            StandardInputEncoding=new UTF8Encoding(false),
            StandardOutputEncoding=new UTF8Encoding(false),
            StandardErrorEncoding=new UTF8Encoding(false),
        };
        if(isDll){
            startInfo.ArgumentList.Add(workerPath);
        }
        return startInfo;
    }
}

public sealed class WorkerProcessSession:IWorkerProcessSession{
    public static readonly TimeSpan ForcedTerminationDelay=TimeSpan.FromSeconds(3);

    private readonly Process process;
    private readonly int processID;
    private readonly object lifetimeGate=new();
    private readonly Channel<WorkerMessageEnvelope> responses;
    private readonly SemaphoreSlim writeGate=new(1,1);
    private readonly Task outputPumpTask;
    private readonly Task<string> standardErrorTask;
    private readonly Func<Task>? afterProcessExit;
    private Task<bool>? stopTask;
    private Task? disposeTask;
    private bool exitConfirmed;
    private int responseReaderStarted;
    private int disposed;

    internal WorkerProcessSession(Process process,Func<Task>? afterProcessExit=null){
        this.process=process;
        this.processID=process.Id;
        this.afterProcessExit=afterProcessExit;
        this.responses=Channel.CreateUnbounded<WorkerMessageEnvelope>(
            new UnboundedChannelOptions{
                SingleReader=true,
                SingleWriter=true,
                AllowSynchronousContinuations=false,
            });
        this.outputPumpTask=this.PumpOutputAsync();
        this.standardErrorTask=process.StandardError.ReadToEndAsync();
    }

    public int ProcessID=>this.processID;
    public bool HasExited{
        get{
            lock(this.lifetimeGate){
                if(!this.exitConfirmed){
                    this.exitConfirmed=this.process.HasExited;
                }
                return this.exitConfirmed;
            }
        }
    }

    public async Task SendAsync<T>(
        string requestID,
        string messageType,
        T payload,
        CancellationToken cancellationToken=default){
        this.ThrowIfDisposed();
        var line=DynamicWorkerProtocol.Serialize(
            DynamicWorkerProtocol.Create(requestID,messageType,payload));
        await this.WriteLineAsync(line,cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteLineAsync(string line,CancellationToken cancellationToken){
        await this.writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{
            await this.process.StandardInput.WriteLineAsync(
                line.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            await this.process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }catch(InvalidOperationException exception){
            throw new IOException("Worker standard input is closed.",exception);
        }finally{
            this.writeGate.Release();
        }
    }

    public async IAsyncEnumerable<WorkerMessageEnvelope> ReadAllAsync(
        [EnumeratorCancellation]CancellationToken cancellationToken=default){
        if(Interlocked.Exchange(ref this.responseReaderStarted,1)!=0){
            throw new InvalidOperationException(
                "Worker responses can be consumed by only one reader.");
        }
        await foreach(var response in this.responses.Reader.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false)){
            yield return response;
        }
    }

    public Task<bool> CancelAsync(string requestID){
        return this.SendControlAndStopAsync(requestID,"cancel");
    }

    public Task<bool> ShutdownAsync(string requestID){
        return this.SendControlAndStopAsync(requestID,"shutdown");
    }

    public async Task<string> GetStandardErrorAsync(){
        if(!this.HasExited){
            throw new InvalidOperationException(
                "Worker standard error is available after the process exits.");
        }
        return await this.standardErrorTask.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync(){
        TaskCompletionSource completion;
        lock(this.lifetimeGate){
            if(this.disposeTask is{} existing){
                return new ValueTask(existing);
            }
            Volatile.Write(ref this.disposed,1);
            completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.disposeTask=completion.Task;
        }
        _=this.CompleteDisposeAsync(completion);
        return new ValueTask(completion.Task);
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion){
        try{
            await this.SendControlAndStopAsync(
                $"dispose-{Guid.NewGuid():N}",
                "shutdown").ConfigureAwait(false);
            lock(this.lifetimeGate){
                this.writeGate.Dispose();
                this.process.Dispose();
            }
            completion.TrySetResult();
        }catch(Exception exception){
            // 停止失敗時はProcessを保持し、後続Disposeにも同じ失敗を返す。
            completion.TrySetException(exception);
        }
    }

    private Task<bool> SendControlAndStopAsync(string requestID,string messageType){
        TaskCompletionSource<bool> completion;
        lock(this.lifetimeGate){
            if(this.stopTask is{} existing){
                return existing;
            }
            completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.stopTask=completion.Task;
        }
        _=this.CompleteStopAsync(completion,requestID,messageType);
        return completion.Task;
    }

    private async Task CompleteStopAsync(TaskCompletionSource<bool> completion,string requestID,string messageType){
        try{
            completion.TrySetResult(await this.StopCoreAsync(requestID,messageType).ConfigureAwait(false));
        }catch(Exception exception){
            completion.TrySetException(exception);
        }
    }

    private async Task<bool> StopCoreAsync(string requestID,string messageType){
        if(this.HasExited){
            await this.outputPumpTask.ConfigureAwait(false);
            await this.standardErrorTask.ConfigureAwait(false);
            return false;
        }

        try{
            var line=DynamicWorkerProtocol.Serialize(
                DynamicWorkerProtocol.Create(
                    requestID,
                    messageType,
                    new WorkerControlPayload(DateTimeOffset.UtcNow)));
            await this.WriteLineAsync(line,CancellationToken.None).ConfigureAwait(false);
        }catch(IOException) when(this.HasExited){
        }catch(InvalidOperationException) when(this.HasExited){
        }

        var exitTask=this.process.WaitForExitAsync();
        var completed=await Task.WhenAny(
            exitTask,
            Task.Delay(ForcedTerminationDelay)).ConfigureAwait(false);
        var forced=completed!=exitTask;
        if(forced&&!this.HasExited){
            this.process.Kill(true);
        }
        await exitTask.ConfigureAwait(false);
        lock(this.lifetimeGate){
            this.exitConfirmed=true;
        }
        await this.outputPumpTask.ConfigureAwait(false);
        await this.standardErrorTask.ConfigureAwait(false);
        if(this.afterProcessExit is{} phaseGate){
            await phaseGate().ConfigureAwait(false);
        }
        return forced;
    }

    private async Task PumpOutputAsync(){
        Exception? protocolFailure=null;
        try{
            while(await this.process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is{} line){
                if(protocolFailure is not null){
                    continue;
                }
                try{
                    await this.responses.Writer.WriteAsync(
                        DynamicWorkerProtocol.Deserialize(line)).ConfigureAwait(false);
                }catch(Exception exception) when(
                    exception is DynamicWorkerProtocolException or ArgumentException){
                    protocolFailure=exception;
                }
            }
        }catch(Exception exception){
            protocolFailure??=exception;
        }finally{
            this.responses.Writer.TryComplete(protocolFailure);
        }
    }

    private void ThrowIfDisposed(){
        lock(this.lifetimeGate){
            if(Volatile.Read(ref this.disposed)!=0){
                throw new ObjectDisposedException(nameof(WorkerProcessSession));
            }
            if(this.HasExited){
                throw new InvalidOperationException(
                    $"Worker process has already exited with code {this.process.ExitCode}.");
            }
        }
    }

    private sealed record WorkerControlPayload(DateTimeOffset RequestedAt);
}
