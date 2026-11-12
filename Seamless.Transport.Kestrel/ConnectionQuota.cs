using Microsoft.AspNetCore.Connections;

namespace Seamless.Transport.Kestrel;

/// <summary>TCPと別のIPCアダプターも同じ接続枠へ参加できる。</summary>
public sealed class ConnectionQuota{
    private int count;
    public int Maximum{get;}
    public int Count=>Volatile.Read(ref this.count);
    public ConnectionQuota(int maximum){ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);this.Maximum=maximum;}
    public IDisposable? TryEnter(){while(true){var current=this.Count;if(current>=this.Maximum){return null;}if(Interlocked.CompareExchange(ref this.count,current+1,current)==current){return new Lease(this);}}}
    internal ConnectionDelegate Limit(ConnectionDelegate next)=>async connection=>{
        using var permit=this.TryEnter();
        if(permit is null){connection.Abort();return;}
        await next(connection).ConfigureAwait(false);
    };
    private sealed class Lease(ConnectionQuota quota):IDisposable{
        private ConnectionQuota? owner=quota;
        public void Dispose(){var value=Interlocked.Exchange(ref this.owner,null);if(value is not null){Interlocked.Decrement(ref value.count);}}
    }
}
