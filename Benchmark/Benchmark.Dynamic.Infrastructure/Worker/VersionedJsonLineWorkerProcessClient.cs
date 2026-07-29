using Benchmark.Dynamic.Core;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

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

    private static ProcessStartInfo CreateStartInfo(string workerPath,string? workingDirectory){
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
    private readonly Channel<WorkerMessageEnvelope> responses;
    private readonly SemaphoreSlim writeGate=new(1,1);
    private readonly Task outputPumpTask;
    private readonly Task<string> standardErrorTask;
    private int responseReaderStarted;
    private int stopStarted;
    private int disposed;

    internal WorkerProcessSession(Process process){
        this.process=process;
        this.responses=Channel.CreateUnbounded<WorkerMessageEnvelope>(
            new UnboundedChannelOptions{
                SingleReader=true,
                SingleWriter=true,
                AllowSynchronousContinuations=false,
            });
        this.outputPumpTask=this.PumpOutputAsync();
        this.standardErrorTask=process.StandardError.ReadToEndAsync();
    }

    public int ProcessID=>this.process.Id;
    public bool HasExited=>this.process.HasExited;

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
        if(!this.process.HasExited){
            throw new InvalidOperationException(
                "Worker standard error is available after the process exits.");
        }
        return await this.standardErrorTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync(){
        if(Interlocked.Exchange(ref this.disposed,1)!=0){
            return;
        }
        try{
            if(!this.process.HasExited){
                await this.SendControlAndStopAsync(
                    $"dispose-{Guid.NewGuid():N}",
                    "shutdown").ConfigureAwait(false);
            }
            await this.outputPumpTask.ConfigureAwait(false);
            await this.standardErrorTask.ConfigureAwait(false);
        }finally{
            this.writeGate.Dispose();
            this.process.Dispose();
        }
    }

    private async Task<bool> SendControlAndStopAsync(string requestID,string messageType){
        if(Interlocked.Exchange(ref this.stopStarted,1)!=0){
            if(!this.process.HasExited){
                await this.process.WaitForExitAsync().ConfigureAwait(false);
            }
            return false;
        }

        if(this.process.HasExited){
            await this.outputPumpTask.ConfigureAwait(false);
            return false;
        }

        try{
            var line=DynamicWorkerProtocol.Serialize(
                DynamicWorkerProtocol.Create(
                    requestID,
                    messageType,
                    new WorkerControlPayload(DateTimeOffset.UtcNow)));
            await this.WriteLineAsync(line,CancellationToken.None).ConfigureAwait(false);
        }catch(IOException) when(this.process.HasExited){
        }catch(InvalidOperationException) when(this.process.HasExited){
        }

        var exitTask=this.process.WaitForExitAsync();
        var completed=await Task.WhenAny(
            exitTask,
            Task.Delay(ForcedTerminationDelay)).ConfigureAwait(false);
        var forced=completed!=exitTask;
        if(forced&&!this.process.HasExited){
            this.process.Kill(true);
        }
        await this.process.WaitForExitAsync().ConfigureAwait(false);
        await this.outputPumpTask.ConfigureAwait(false);
        await this.standardErrorTask.ConfigureAwait(false);
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
        if(Volatile.Read(ref this.disposed)!=0){
            throw new ObjectDisposedException(nameof(WorkerProcessSession));
        }
        if(this.process.HasExited){
            throw new InvalidOperationException(
                $"Worker process has already exited with code {this.process.ExitCode}.");
        }
    }

    private sealed record WorkerControlPayload(DateTimeOffset RequestedAt);
}
