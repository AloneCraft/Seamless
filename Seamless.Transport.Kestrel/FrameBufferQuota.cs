namespace Seamless.Transport.Kestrel;

/// <summary>通信メッセージの復元用バッファに使う共有予算。</summary>
public sealed class FrameBufferQuota{
    private long used;
    public long MaximumBytes{get;}
    public long UsedBytes=>Volatile.Read(ref this.used);

    public FrameBufferQuota(long maximumBytes){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        this.MaximumBytes=maximumBytes;
    }

    public IDisposable? TryReserve(long bytes){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        if(!this.TryAcquire(bytes)){return null;}
        return new Reservation(this,bytes);
    }
    public GrowingReservation ReserveGrowing()=>new(this);

    internal bool TryAcquire(long bytes){
        while(true){
            var current=Volatile.Read(ref this.used);
            if(bytes>this.MaximumBytes-current){return false;}
            if(Interlocked.CompareExchange(ref this.used,current+bytes,current)==current){return true;}
        }
    }

    internal void Release(long bytes)=>Interlocked.Add(ref this.used,-bytes);

    private sealed class Reservation(FrameBufferQuota quota,long bytes):IDisposable{
        private FrameBufferQuota? owner=quota;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release(bytes);
    }

    public sealed class GrowingReservation:IDisposable{
        private readonly FrameBufferQuota quota;
        private readonly Lock gate=new();
        private long bytes;
        private bool disposed;
        internal GrowingReservation(FrameBufferQuota quota){this.quota=quota;}
        public long Bytes=>Volatile.Read(ref this.bytes);
        public bool TryGrowTo(long totalBytes){
            lock(this.gate){
                ObjectDisposedException.ThrowIf(this.disposed,this);
                ArgumentOutOfRangeException.ThrowIfLessThan(totalBytes,this.bytes);
                var growth=totalBytes-this.bytes;
                if(growth!=0&&!this.quota.TryAcquire(growth)){return false;}
                this.bytes=totalBytes;
                return true;
            }
        }
        public void Dispose(){
            lock(this.gate){
                if(this.disposed){return;}
                this.quota.Release(this.bytes);
                this.bytes=0;
                this.disposed=true;
            }
        }
    }
}
