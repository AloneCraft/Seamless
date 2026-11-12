using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.IO.Pipes;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Native;

public sealed partial class NativeQueryServer{
    private LocalPipeOwner? localOwner;
    private Task? pipeAccept;
    private readonly ConcurrentDictionary<long,Task> pipeConnections=new();
    public string? PipeName{get;private set;}
    public event Action<Exception>? LocalConnectionFailed;
    /// <summary>同一OS所有者に許可した、有限枠の正式業務IPC入口を作成する。</summary>
    public static NativeQueryServer CreateLocalOwner(DatabaseRegistry databases,string pipeName,NativeServerLimits? limits=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        if(pipeName.Length>128||pipeName.Any(static value=>!char.IsAsciiLetterOrDigit(value)&&value is not '-' and not '_' and not '.')){throw new ArgumentException("Invalid local pipe name.",nameof(pipeName));}
        limits??=new(){MaximumConnections=32};limits.Validate();ArgumentOutOfRangeException.ThrowIfGreaterThan(limits.MaximumConnections,254);
        return new(databases,0,limits.MaximumConnections,limits.MaximumConcurrentRequests,limits.IncompleteFrameTimeout,limits.FrameWriteTimeout,
            new FrameBufferQuota(limits.MaximumReceiveBufferBytes),new FrameBufferQuota(limits.MaximumBufferedRequestBytes),new FrameBufferQuota(limits.MaximumPendingWriteBytes)){
            PipeName=pipeName,localOwner=LocalPipeOwner.Capture()
        };
    }
    private NamedPipeServerStream NewPipe(bool first){
        var options=global::System.IO.Pipes.PipeOptions.Asynchronous|global::System.IO.Pipes.PipeOptions.CurrentUserOnly;
        if(first&&OperatingSystem.IsWindows()){options|=global::System.IO.Pipes.PipeOptions.FirstPipeInstance;}
        return new(this.PipeName!,PipeDirection.InOut,NamedPipeServerStream.MaxAllowedServerInstances,PipeTransmissionMode.Byte,options);
    }
    private async Task AcceptPipesAsync(NamedPipeServerStream pending){
        try{
            while(!this.stop!.IsCancellationRequested){
                try{await pending.WaitForConnectionAsync(this.stop.Token).ConfigureAwait(false);}
                catch(UnauthorizedAccessException error){pending.Dispose();this.LocalConnectionFailed?.Invoke(error);pending=this.NewPipe(false);continue;}
                var pipe=pending;
                try{pending=this.NewPipe(false);}catch{pipe.Dispose();throw;}
                var permit=this.connectionQuota!.TryEnter();
                if(permit is null){pipe.Dispose();continue;}
                var id=Interlocked.Increment(ref this.nextConnection);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);this.pipeConnections[id]=completion.Task;
                _=Task.Run(async()=>{
                    try{await this.ServePipeAsync(pipe,id).ConfigureAwait(false);}
                    catch(Exception error){this.LocalConnectionFailed?.Invoke(error);}
                    finally{pipe.Dispose();permit.Dispose();this.pipeConnections.TryRemove(id,out _);completion.TrySetResult();}
                });
            }
        }catch(OperationCanceledException)when(this.stop!.IsCancellationRequested){}
        finally{pending.Dispose();}
    }
    private async Task ServePipeAsync(NamedPipeServerStream pipe,long id){
        var input=PipeReader.Create(pipe,new(leaveOpen:true));var output=PipeWriter.Create(pipe,new(leaveOpen:true));
        var transport=new LocalDuplex(input,output);var frames=new LengthPrefixedPipe(transport,NativeFrame.MaximumBytes,false,this.incompleteFrameTimeout,this.frameWriteTimeout,this.receiveBuffers,this.pendingWrites);
        try{await this.ConnectionAsync(frames,this.stop!.Token,id,null,()=>this.localOwner!.Verify(pipe)).ConfigureAwait(false);}
        finally{try{await input.CompleteAsync().ConfigureAwait(false);}finally{await output.CompleteAsync().ConfigureAwait(false);}}
    }
    private sealed record LocalDuplex(PipeReader Input,PipeWriter Output):IDuplexPipe;
}
