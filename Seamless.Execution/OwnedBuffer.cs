using System.Buffers;
namespace Seamless.Execution;
public sealed class OwnedBuffer:IMemoryOwner<byte>{
    private readonly ExecutionScope scope;
    private readonly ResourceReservation reservation;
    private byte[]? bytes;
    internal OwnedBuffer(ExecutionScope scope,int length){
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        this.scope=scope;
        var size=checked((long)length+64);
        if(size>scope.Budget.MemoryLimitBytes){throw new ResourceLimitException(ResourceLimitKind.SingleElement,size,scope.Budget.MemoryLimitBytes);}
        this.reservation=scope.Budget.ReserveMemory(size);
        try{this.bytes=new byte[length];scope.Own(this);}
        catch{this.reservation.Dispose();throw;}
    }
    public Memory<byte> Memory{
        get{
            ObjectDisposedException.ThrowIf(this.bytes is null,this);
            this.scope.ThrowIfUnavailable();
            return this.bytes;
        }
    }
    public void Dispose(){
        if(this.bytes is null){return;}
        this.bytes=null;
        this.reservation.Dispose();
        this.scope.Release(this);
    }
}
