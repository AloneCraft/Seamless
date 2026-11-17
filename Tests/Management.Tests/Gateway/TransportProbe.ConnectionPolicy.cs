using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using Seamless.Database.Client;
using ClientPool=Seamless.Database.Client.NativeConnectionPool;

namespace Seamless.Database.Gateway.Tests;

internal static partial class TransportProbe{
    private static async Task MeasurePoliciesAsync(int port,int workers,Func<Task<Snapshot>> capture,List<object> results,CancellationToken token,PolicyProbeAuth? auth=null){
        const int rounds=3,continuousRequests=128,bursts=5,requestsPerBurst=12,rowsPerWorker=128;
        var idleTimeout=TimeSpan.FromMilliseconds(125);
        var gap=TimeSpan.FromMilliseconds(350);
        var policies=new NativeConnectionPolicy[]{
            new(NativeConnectionLifetime.KeepAlive),new(NativeConnectionLifetime.ReleaseWhenIdle,idleTimeout),new(NativeConnectionLifetime.ReleaseAfterOperation)
        };
        var fingerprint=auth?.Fingerprint??Fingerprint;
        using var sockets=new SocketConnectCounter();
        for(var round=0;round<rounds;round++){
            for(var position=0;position<6;position++){
                var mode=(position+2*round)%6;
                var pooled=mode>=3;
                var policy=policies[mode%3];
                ClientPool? selectedPool=null;
                if(pooled){
                    var options=new NativeConnectionPoolOptions{MaximumConnections=workers,MaximumWaiters=0,ConnectionPolicy=policy};
                    if(auth is null){selectedPool=new("127.0.0.1",port,options);}
                    else{selectedPool=ClientPool.ForAuthenticatedTls("127.0.0.1",port,options,auth.Identities[0].Principal,auth.Identities[0].TokenProvider,auth.Certificate);}
                }
                using var pool=selectedPool;
                var clients=new NativeQueryClient[workers];
                if(pool is null){
                    for(var worker=0;worker<workers;worker++){
                        if(auth is null){clients[worker]=new("127.0.0.1",port,policy);}
                        else{clients[worker]=NativeQueryClient.ForAuthenticatedTls(auth.Identities[0].TokenProvider,"127.0.0.1",port,auth.Certificate,policy);}
                    }
                }
                try{
                    await Task.WhenAll(Enumerable.Range(0,workers).Select(worker=>Small(worker,0)));
                    var continuous=await MeasureSmall(1,continuousRequests,TimeSpan.Zero);
                    var burst=await MeasureSmall(bursts,requestsPerBurst,gap);
                    var waiting=await WaitingConnectionsAsync();
                    var keepAlive=policy.Lifetime==NativeConnectionLifetime.KeepAlive;
                    if(keepAlive&&(waiting<1||waiting>workers)||!keepAlive&&waiting!=0){
                        throw new InvalidDataException($"Unexpected waiting connection count for {policy.Lifetime}: {waiting}.");
                    }
                    if(policy.Lifetime==NativeConnectionLifetime.ReleaseAfterOperation&&
                        (continuous.ConnectAttempts!=continuous.Requests||burst.ConnectAttempts!=burst.Requests)){
                        throw new InvalidDataException("The per-operation connection count does not match the request count.");
                    }
                    if(keepAlive&&!pooled&&(continuous.ConnectAttempts!=0||burst.ConnectAttempts!=0||waiting!=workers)){
                        throw new InvalidDataException("The warmed persistent connections did not remain available.");
                    }
                    if(policy.Lifetime==NativeConnectionLifetime.ReleaseWhenIdle&&burst.ConnectAttempts<bursts-1){
                        throw new InvalidDataException("The idle policy did not reconnect between bursts.");
                    }
                    var before=await capture();
                    var start=Stopwatch.GetTimestamp();
                    await Task.WhenAll(Enumerable.Range(0,workers).Select(Large));
                    var seconds=Stopwatch.GetElapsedTime(start).TotalSeconds;
                    var after=await capture();
                    var bytes=(long)workers*rowsPerWorker*32768;
                    results.Add(new{Round=round+1,Order=position+1,Policy=policy.Lifetime.ToString(),Pooled=pooled,Workers=workers,
                        IdleMilliseconds=idleTimeout.TotalMilliseconds,GapMilliseconds=gap.TotalMilliseconds,Continuous=continuous,Burst=burst,
                        WaitingConnections=waiting,Large=new{Bytes=bytes,Seconds=seconds,MiBPerSecond=bytes/(1024.0*1024)/seconds,Before=before,After=after}});
                }finally{
                    if(pool is null){foreach(var client in clients){await client.DisposeAsync();}}
                }

                async Task Small(int worker,int value){
                    if(pool is not null){
                        await using var client=await pool.RentAsync(token);
                        await Call(client);
                    }else{await Call(clients[worker]);}
                    async Task Call(NativeQueryClient client){
                        var actual=await client.ExecuteScalarAsync<int,int>("probe","small","fixture",fingerprint,value,token);
                        if(actual!=value+1){throw new InvalidDataException("Probe result mismatch.");}
                    }
                }
                async Task Large(int worker){
                    if(pool is not null){
                        await using var client=await pool.RentAsync(token);
                        await Read(client);
                    }else{await Read(clients[worker]);}
                    async Task Read(NativeQueryClient client){
                        var count=0;
                        await foreach(var row in client.QueryAsync<int,byte[]>("probe","large","fixture",fingerprint,rowsPerWorker,token)){
                            if(row.Length!=32768){throw new InvalidDataException("Probe row length mismatch.");}
                            count++;
                        }
                        if(count!=rowsPerWorker){throw new InvalidDataException("Probe row count mismatch.");}
                    }
                }
                async Task<PolicySample> MeasureSmall(int segments,int requests,TimeSpan pause){
                    var latencies=new double[segments*workers*requests];
                    var before=await capture();
                    var clientBefore=MeasureClient();
                    var initialAttempts=sockets.Count;
                    var started=Stopwatch.GetTimestamp();
                    for(var segment=0;segment<segments;segment++){
                        await Task.WhenAll(Enumerable.Range(0,workers).Select(Worker));
                        if(segment+1<segments){await Task.Delay(pause,token);}
                        async Task Worker(int worker){
                            var offset=(segment*workers+worker)*requests;
                            for(var index=0;index<requests;index++){
                                var time=Stopwatch.GetTimestamp();
                                await Small(worker,offset+index);
                                latencies[offset+index]=Stopwatch.GetElapsedTime(time).TotalMicroseconds;
                            }
                        }
                    }
                    var seconds=Stopwatch.GetElapsedTime(started).TotalSeconds;
                    var attempts=sockets.Count-initialAttempts;
                    var clientAfter=MeasureClient();
                    var after=await capture();
                    var authenticatedSessions=after.AuthenticatedSessions-before.AuthenticatedSessions;
                    if(auth is not null&&authenticatedSessions!=attempts){throw new InvalidDataException("The authenticated session count does not match the TCP connect count.");}
                    Array.Sort(latencies);
                    double Percentile(double percent)=>latencies[(int)Math.Ceiling(percent*latencies.Length)-1];
                    return new(latencies.Length,seconds,latencies.Length/seconds,Percentile(0.50),Percentile(0.95),Percentile(0.99),attempts,authenticatedSessions,before,after,clientBefore,clientAfter);
                }
                async Task<int> WaitingConnectionsAsync(){
                    await Task.Delay(gap,token);
                    return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Count(connection=>
                        connection.RemoteEndPoint.Port==port&&connection.State==TcpState.Established&&IPAddress.IsLoopback(connection.RemoteEndPoint.Address));
                }
            }
        }
    }

    private static Snapshot MeasureClient(){
        using var process=Process.GetCurrentProcess();
        return new(process.PrivateMemorySize64,process.WorkingSet64,GC.GetTotalAllocatedBytes(),GC.GetTotalMemory(false),
            GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2),process.TotalProcessorTime.TotalMilliseconds,process.Threads.Count);
    }

    private sealed record PolicySample(int Requests,double Seconds,double RequestsPerSecond,double P50Us,double P95Us,double P99Us,int ConnectAttempts,
        int? AuthenticatedSessions,Snapshot ServerBefore,Snapshot ServerAfter,Snapshot ClientBefore,Snapshot ClientAfter);
    private sealed record PolicyProbeAuth(X509Certificate2 Certificate,string Fingerprint,PolicyProbeIdentity[] Identities);
    private sealed record PolicyProbeIdentity(NativeAuthenticatedPrincipal Principal,Func<CancellationToken,ValueTask<string>> TokenProvider,string Query);

    private sealed class SocketConnectCounter:EventListener{
        private int count;
        internal int Count=>Volatile.Read(ref this.count);
        protected override void OnEventSourceCreated(EventSource eventSource){
            if(eventSource.Name=="System.Net.Sockets"){this.EnableEvents(eventSource,EventLevel.Informational);}
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData){
            if(eventData.EventName=="ConnectStart"){Interlocked.Increment(ref this.count);}
        }
    }
}
