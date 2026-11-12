using System.IO.Pipelines;
using System.IO.Pipes;
using System.Buffers;
using Seamless.Execution;

namespace Seamless.Management.Server;

public sealed partial class ManagementServer{
    private sealed class ResourceMemoryPool(ExecutionScope resources):MemoryPool<byte>{
        private int disposed;
        public override int MaxBufferSize=>ManagementWire.MaximumBytes;
        public override IMemoryOwner<byte> Rent(int minBufferSize=-1){
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
            if(minBufferSize<0){minBufferSize=4096;}
            ArgumentOutOfRangeException.ThrowIfGreaterThan(minBufferSize,this.MaxBufferSize);
            return resources.Rent(minBufferSize);
        }
        protected override void Dispose(bool disposing)=>Interlocked.Exchange(ref this.disposed,1);
    }
    private sealed class PipeTransport:IAsyncDisposable,IDuplexPipe{
        private readonly Pipe input;
        private readonly CancellationTokenSource stopping=new();
        private readonly CancellationTokenSource closed=new();
        private readonly Task receiving;
        public PipeReader Input=>this.input.Reader;
        public PipeWriter Output{get;}
        public CancellationToken ConnectionClosed=>this.closed.Token;
        public PipeTransport(NamedPipeServerStream stream,MemoryPool<byte> pool){
            this.input=new(new System.IO.Pipelines.PipeOptions(pool:pool,pauseWriterThreshold:65536,resumeWriterThreshold:32768,useSynchronizationContext:false));
            this.Output=PipeWriter.Create(stream,new(leaveOpen:true));this.receiving=this.ReceiveAsync(stream);
        }
        private async Task ReceiveAsync(NamedPipeServerStream stream){
            Exception? failure=null;
            try{
                while(true){
                    var buffer=this.input.Writer.GetMemory(4096);var count=await stream.ReadAsync(buffer[..4096],this.stopping.Token).ConfigureAwait(false);
                    if(count==0){break;}this.input.Writer.Advance(count);
                    var flush=await this.input.Writer.FlushAsync(this.stopping.Token).ConfigureAwait(false);if(flush.IsCompleted||flush.IsCanceled){break;}
                }
            }catch(OperationCanceledException)when(this.stopping.IsCancellationRequested){}
            catch(ResourceLimitException error){failure=error;}
            catch(IOException error){failure=error;}
            catch(ObjectDisposedException){}
            finally{this.closed.Cancel();await this.input.Writer.CompleteAsync(failure).ConfigureAwait(false);}
        }
        public async ValueTask DisposeAsync(){
            this.stopping.Cancel();await this.input.Reader.CompleteAsync().ConfigureAwait(false);
            try{await this.receiving.ConfigureAwait(false);}
            finally{await this.Output.CompleteAsync().ConfigureAwait(false);this.closed.Dispose();this.stopping.Dispose();}
        }
    }
}
