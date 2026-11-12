using System.IO.Pipelines;

namespace Seamless.Transport.Kestrel;

/// <summary>借用したPipeを既存のStreamプロトコルへ接続する。Pipeの完了はトランスポート所有者が行う。</summary>
public sealed class DuplexPipeStream:Stream{
    private readonly Stream input;
    private readonly PipeWriter output;
    private bool disposed;
    public DuplexPipeStream(IDuplexPipe pipe){ArgumentNullException.ThrowIfNull(pipe);this.input=pipe.Input.AsStream(leaveOpen:true);this.output=pipe.Output;}
    public override bool CanRead=>!this.disposed;
    public override bool CanWrite=>!this.disposed;
    public override bool CanSeek=>false;
    public override long Length=>throw new NotSupportedException();
    public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
    public override int Read(byte[] buffer,int offset,int count)=>this.input.Read(buffer,offset,count);
    public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken cancellationToken)=>this.input.ReadAsync(buffer,offset,count,cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>this.input.ReadAsync(buffer,cancellationToken);
    public override void Write(byte[] buffer,int offset,int count)=>this.WriteAsync(buffer.AsMemory(offset,count)).AsTask().GetAwaiter().GetResult();
    public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken cancellationToken)=>this.WriteAsync(buffer.AsMemory(offset,count),cancellationToken).AsTask();
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default){ObjectDisposedException.ThrowIf(this.disposed,this);Check(await this.output.WriteAsync(buffer,cancellationToken).ConfigureAwait(false),cancellationToken);}
    public override void Flush()=>this.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
    public override async Task FlushAsync(CancellationToken cancellationToken){ObjectDisposedException.ThrowIf(this.disposed,this);Check(await this.output.FlushAsync(cancellationToken).ConfigureAwait(false),cancellationToken);}
    private static void Check(FlushResult result,CancellationToken cancellation){if(result.IsCanceled){throw new OperationCanceledException(cancellation);}if(result.IsCompleted){throw new IOException("The transport output has completed.");}}
    public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
    public override void SetLength(long value)=>throw new NotSupportedException();
    protected override void Dispose(bool disposing){if(this.disposed){return;}this.disposed=true;if(disposing){this.input.Dispose();}base.Dispose(disposing);}
}
