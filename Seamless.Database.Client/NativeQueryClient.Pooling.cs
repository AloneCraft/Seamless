using System.Security.Cryptography.X509Certificates;

namespace Seamless.Database.Client;

public sealed partial class NativeQueryClient{
    private NativeConnectionPool? pool;

    internal static NativeQueryClient CreatePooled(NativeConnectionPool pool,string host,int port,NativeConnectionPoolOptions options,
        bool tls,X509Certificate2? trustedRoot,NativeAuthenticatedPrincipal? principal,Func<CancellationToken,ValueTask<string>>? tokenProvider,
        NativeConnectionPool.Transport? transport){
        var client=new NativeQueryClient(host,port,tls,trustedRoot,tokenProvider){
            pool=pool,UseServerConnectionPolicy=options.UseServerConnectionPolicy,ServerConnectionPolicyProfile=options.ServerConnectionPolicyProfile,
            RecordBudget=options.RecordBudget,RecordLimits=options.RecordLimits
        };
        client.configuredConnectionPolicy=options.ConnectionPolicy;
        client.reconnectPrincipal=principal;
        if(transport is not null){
            client.socket=transport.Socket;
            client.stream=transport.Stream;
            client.AuthenticatedPrincipal=transport.Principal;
            client.ServerRecommendedConnectionPolicy=transport.Recommendation;
            client.connectionPolicyDiscovered=transport.Discovered;
            client.idleSince=transport.IdleSince;
            lock(client.gate){
                if(transport.Remaining is{} time){client.ScheduleIdleExpiry(time);}
            }
        }
        return client;
    }

    private void ReturnToPool(){
        NativeConnectionPool.Transport? transport=null;
        lock(this.gate){
            if(this.disposed){return;}
            if(!this.gate.Wait(0)){throw new InvalidOperationException("Complete the transaction or result enumeration before returning the native connection.");}
            try{
                this.disposed=true;
                this.idleTimer?.Dispose();
                if(this.socket is not null&&this.ConnectionPolicy.Lifetime!=NativeConnectionLifetime.ReleaseAfterOperation){
                    transport=new(this.socket,this.stream!,this.AuthenticatedPrincipal,this.ServerRecommendedConnectionPolicy,
                        this.connectionPolicyDiscovered,this.ConnectionPolicy.IdleTimeout,this.idleSince);
                    this.socket=null;this.stream=null;this.AuthenticatedPrincipal=null;
                }else{this.CloseConnection();}
            }finally{
                this.gate.Release();
                this.pool!.Return(transport);
            }
        }
    }
}
