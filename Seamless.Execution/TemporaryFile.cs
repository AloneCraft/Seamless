namespace Seamless.Execution;
public sealed class TemporaryFile:Stream{
    private readonly ExecutionScope scope;
    private readonly string path;
    private readonly FileStream stream;
    private readonly ResourceReservation reservation;
    private bool disposed;
    internal TemporaryFile(ExecutionScope scope,string path){
        this.scope=scope;this.path=path;
        this.reservation=scope.Budget.ReserveTemporaryStorage(0);
        this.stream=new(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,1,FileOptions.None);
    }
    public override bool CanRead=>!this.disposed;
    public override bool CanWrite=>!this.disposed;
    public override bool CanSeek=>!this.disposed;
    public override long Length{get{this.Check();return this.stream.Length;}}
    public override long Position{get{this.Check();return this.stream.Position;}set{this.Check();this.stream.Position=value;}}
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
    public override void Flush(){this.Check();this.stream.Flush();}
    public override long Seek(long offset,SeekOrigin origin){this.Check();return this.stream.Seek(offset,origin);}
    public override int Read(byte[] buffer,int offset,int count)=>this.Read(buffer.AsSpan(offset,count));
    public override int Read(Span<byte> buffer){this.Check();return this.stream.Read(buffer);}
    public override void Write(byte[] buffer,int offset,int count)=>this.Write(buffer.AsSpan(offset,count));
    public override void Write(ReadOnlySpan<byte> buffer){
        this.Check();
        var previous=this.reservation.Bytes;
        this.reservation.Resize(Math.Max(previous,checked(this.stream.Position+buffer.Length)));
        try{this.stream.Write(buffer);}
        catch{this.reservation.Resize(this.stream.Length);throw;}
    }
    public override void SetLength(long value){
        this.Check();
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var previous=this.reservation.Bytes;
        if(value>previous){this.reservation.Resize(value);}
        try{this.stream.SetLength(value);this.reservation.Resize(value);}
        catch{this.reservation.Resize(this.stream.Length);throw;}
    }
    protected override void Dispose(bool disposing){
        if(!disposing||this.disposed){base.Dispose(disposing);return;}
        this.disposed=true;
        try{this.stream.Dispose();}
        finally{
            try{File.Delete(this.path);}
            finally{this.reservation.Dispose();this.scope.Release(this);base.Dispose(disposing);}
        }
    }
}
