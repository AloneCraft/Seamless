using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Seamless.Transport.Kestrel;

internal static class KestrelTls{
    public static void Use(ListenOptions listen,X509Certificate2 certificate,ConnectionQuota? local,ConnectionQuota? shared){
        if(ReferenceEquals(local,shared)){shared=null;}
        if(local is not null||shared is not null){
            listen.Use(next=>async connection=>{
                using var localPermit=local?.TryEnter();
                if(local is not null&&localPermit is null){connection.Abort();return;}
                using var sharedPermit=shared?.TryEnter();
                if(shared is not null&&sharedPermit is null){connection.Abort();return;}
                connection.Features.Set(new HandshakePermits(localPermit,sharedPermit));
                try{await next(connection).ConfigureAwait(false);}
                finally{connection.Features.Set<HandshakePermits>(null);}
            });
        }
        listen.UseHttps(certificate,https=>https.HandshakeTimeout=TimeSpan.FromSeconds(15));
        if(local is not null||shared is not null){
            // TLS成功時は後続プロトコルの接続寿命に枠を持ち越さない。
            listen.Use(next=>connection=>{
                var permits=connection.Features.Get<HandshakePermits>()!;
                permits.Local?.Dispose();
                permits.Shared?.Dispose();
                connection.Features.Set<HandshakePermits>(null);
                return next(connection);
            });
        }
    }
    private sealed record HandshakePermits(IDisposable? Local,IDisposable? Shared);
}
