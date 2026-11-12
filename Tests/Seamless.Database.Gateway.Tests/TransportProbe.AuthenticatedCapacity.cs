using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

internal static partial class TransportProbe{
    private static async Task<bool> MeasureAuthenticatedCapacityAsync(int port,int parallelism,int maximum,Func<Task<Snapshot>> capture,
        List<object> results,PolicyProbeAuth auth,bool sustained,int? churnInterval,int refreshCycles,int httpsPort,int tdsPort,bool mixedLong,CancellationToken token){
        var clients=new NativeQueryClient?[maximum];
        var policy=new NativeConnectionPolicy(NativeConnectionLifetime.KeepAlive);
        var limits=new ParallelOptions{MaxDegreeOfParallelism=parallelism,CancellationToken=token};
        var targets=new[]{100,1000,3000,6000,10000}.Where(target=>target<maximum).Append(maximum);
        var established=0;
        var complete=true;
        results.Add(new{Case="authenticated-baseline",Server=await capture(),Client=MeasureClient()});
        try{
            foreach(var target in targets){
                var previous=established;
                var attempts=target-previous;
                var connectTimes=new double[attempts];
                var connectFailures=new ConcurrentQueue<CapacityFailure>();
                ChurnFailureEvidence? firstConnectFailure=null;
                var failureRecorded=0;
                var start=Stopwatch.GetTimestamp();
                await Parallel.ForEachAsync(Enumerable.Range(previous,attempts),limits,async(index,cancellation)=>{
                    var client=CreateClient(index);
                    var since=Stopwatch.GetTimestamp();
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(20));
                    try{
                        await client.ConnectAsync(deadline.Token);
                        if(client.AuthenticatedPrincipal!=Identity(index).Principal){throw new InvalidDataException("Unexpected authenticated identity.");}
                        connectTimes[index-previous]=Stopwatch.GetElapsedTime(since).TotalMilliseconds;
                        clients[index]=client;
                        Interlocked.Increment(ref established);
                    }catch(Exception error)when(!cancellation.IsCancellationRequested){
                        connectFailures.Enqueue(Failure(index,"connect",error));
                        if(churnInterval is not null&&Interlocked.CompareExchange(ref failureRecorded,1,0)==0){
                            firstConnectFailure=await ReadChurnFailureEvidenceAsync(index,"connect",port,error,cancellation);
                        }
                    }finally{if(clients[index] is null){await client.DisposeAsync();}}
                });
                var connectSeconds=Stopwatch.GetElapsedTime(start).TotalSeconds;
                var serverConnected=await capture();
                var clientConnected=MeasureClient();
                var verified=0;
                var rpcFailures=new ConcurrentQueue<CapacityFailure>();
                var rpcTimes=new double[target];
                start=Stopwatch.GetTimestamp();
                await Parallel.ForEachAsync(Enumerable.Range(0,target),limits,async(index,cancellation)=>{
                    if(clients[index] is not{} client){return;}
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(10));
                    var since=Stopwatch.GetTimestamp();
                    try{
                        await Small(client,index,index,deadline.Token);
                        rpcTimes[index]=Stopwatch.GetElapsedTime(since).TotalMilliseconds;
                        Interlocked.Increment(ref verified);
                    }catch(Exception error)when(!cancellation.IsCancellationRequested){rpcFailures.Enqueue(Failure(index,"rpc",error));}
                });
                var rpcSeconds=Stopwatch.GetElapsedTime(start).TotalSeconds;
                await Task.Delay(TimeSpan.FromMilliseconds(750),token);
                var open=CountEstablished();
                var serverIdle=await capture();
                var clientIdle=MeasureClient();
                results.Add(new{Case="authenticated-stage",Target=target,ConnectAttempts=attempts,Established=established,Verified=verified,OpenAfterIdle=open,
                    ConnectSeconds=connectSeconds,RpcSeconds=rpcSeconds,ConnectLatency=Summarize(connectTimes),RpcLatency=Summarize(rpcTimes),
                    ConnectFailures=connectFailures.ToArray(),RpcFailures=rpcFailures.ToArray(),ServerConnected=serverConnected,ClientConnected=clientConnected,
                    ServerIdle=serverIdle,ClientIdle=clientIdle,FirstConnectFailure=firstConnectFailure});
                if(established!=target||verified!=target||open!=target||serverIdle.AuthenticatedSessions!=target){
                    complete=false;
                    break;
                }
            }

            if(complete){
                using var https=httpsPort==0?null:CreateMixedHttpsClient(httpsPort,auth.Certificate);
                if(https is not null){await VerifyMixedHttpsAsync(https,await Identity(0).TokenProvider(token),token);}
                if(tdsPort>0){
                    await VerifyMixedTdsRejectsInvalidCredentialAsync(tdsPort,auth.Certificate,token);
                    await WaitMixedSharedConnectionsAsync(capture,maximum+1,token);
                }
                await using var tds=tdsPort==0?null:await OpenMixedTdsAsync(tdsPort,auth.Certificate,await Identity(0).TokenProvider(token),token);
                if(tds is not null){await VerifyMixedTdsAsync(tds,0,token);}
                var rejected=false;
                string? rejectionType=null;
                using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
                    deadline.CancelAfter(TimeSpan.FromSeconds(10));
                    await using var overflow=CreateClient(0);
                    try{await overflow.ConnectAsync(deadline.Token);}
                    catch(Exception error)when((error is IOException or AuthenticationException or SocketException)&&error is not NativeAuthenticationException){
                        rejected=true;
                        rejectionType=error.GetType().Name;
                    }
                }
                var active=clients[0]!;
                await using(var transaction=await active.BeginTransactionAsync("fixture",token)){await transaction.RollbackAsync(token);}
                var streamCount=Math.Min(8,maximum);
                await Task.WhenAll(Enumerable.Range(0,streamCount).Select(async index=>{
                    var count=0;
                    await foreach(var row in clients[index]!.QueryAsync<int,byte[]>("probe","large","fixture",auth.Fingerprint,128,token)){
                        if(row.Length!=32768){throw new InvalidDataException("Unexpected streamed row length.");}
                        count++;
                    }
                    if(count!=128){throw new InvalidDataException("Unexpected streamed row count.");}
                }));
                results.Add(new{Case="authenticated-at-capacity",RejectedExtraConnection=rejected,RejectionType=rejectionType,RollbackCompleted=true,
                    StreamCount=streamCount,StreamBytes=(long)streamCount*128*32768});
                complete=rejected;
                if(auth.Identities.Length>1&&complete){
                    var beforeDenied=await capture();
                    var denied=0;
                    for(var index=0;index<auth.Identities.Length;index++){
                        var client=clients[index]!;
                        var forbidden=Identity(index+1).Query;
                        try{await client.ExecuteScalarAsync<int,int>("probe",forbidden,"fixture",auth.Fingerprint,index,token);}
                        catch(NativeQueryException error)when(error.IsRecoverable){denied++;}
                        await Small(client,index,index,token);
                    }
                    var afterDenied=await capture();
                    var serverDenied=afterDenied.DeniedPrivateQueries-beforeDenied.DeniedPrivateQueries;
                    results.Add(new{Case="authenticated-identity-isolation",Identities=auth.Identities.Length,Denied=denied,ServerDenied=serverDenied,
                        OwnQueryVerified=auth.Identities.Length,NewAuthenticatedSessions=afterDenied.AuthenticatedSessions-beforeDenied.AuthenticatedSessions});
                    complete=denied==auth.Identities.Length&&serverDenied==denied&&afterDenied.AuthenticatedSessions==beforeDenied.AuthenticatedSessions;
                }
                if(sustained&&complete){
                    var smoke=maximum<10000;
                    if(smoke){
                        complete=await MeasureAuthenticatedRateAsync(500,3,maximum,parallelism,SmallAt,capture,results,token);
                    }else{
                        var low=await MeasureAuthenticatedRateAsync(2000,30,maximum,parallelism,SmallAt,capture,results,token);
                        var high=await MeasureAuthenticatedRateAsync(10000,30,maximum,parallelism,SmallAt,capture,results,token);
                        complete=low&&high;
                    }
                }
                if(churnInterval is{} interval&&complete){
                    complete=await MeasureAuthenticatedChurnAsync(clients,parallelism,interval,port,CreateClient,Identity,Small,capture,CountEstablished,()=>CountTcp(TcpState.TimeWait),results,token);
                }
                if(refreshCycles>0&&complete){
                    complete=await MeasureAuthenticatedRateAsync(2000,30+300*refreshCycles,maximum,parallelism,SmallAt,capture,results,token,expectedRefreshCycles:refreshCycles);
                }
                if(https is not null&&complete){
                    complete=await MeasureMixedHttpsAsync(https,httpsPort,tds,tdsPort,auth,maximum,limits,SmallAt,CreateClient,Small,capture,results,token,mixedLong);
                }
            }
        }finally{
            foreach(var client in clients){if(client is not null){await client.DisposeAsync();}}
        }

        using var cleanup=CancellationTokenSource.CreateLinkedTokenSource(token);
        cleanup.CancelAfter(TimeSpan.FromSeconds(15));
        var openAfterClose=CountEstablished();
        var afterClose=await capture();
        while((openAfterClose!=0||afterClose.ActiveAuthenticatedSessions!=0||afterClose.SharedConnections is >0)&&!cleanup.IsCancellationRequested){
            await Task.Delay(TimeSpan.FromMilliseconds(200),token);
            openAfterClose=CountEstablished();
            afterClose=await capture();
        }
        var clientAfterClose=MeasureClient();
        var cleanupVerified=openAfterClose==0&&afterClose.ActiveAuthenticatedSessions==0&&afterClose.SharedConnections is null or 0;
        var replacementVerified=false;
        if(cleanupVerified&&established==maximum){
            await using var replacement=CreateClient(0);
            await replacement.ConnectAsync(token);
            await Small(replacement,0,0,token);
            replacementVerified=replacement.AuthenticatedPrincipal==Identity(0).Principal;
        }
        results.Add(new{Case="authenticated-cleanup",OpenAfterClose=openAfterClose,ReplacementVerified=replacementVerified,Server=afterClose,Client=clientAfterClose});
        return complete&&cleanupVerified&&replacementVerified;

        PolicyProbeIdentity Identity(int index)=>auth.Identities[index%auth.Identities.Length];
        NativeQueryClient CreateClient(int index)=>NativeQueryClient.ForAuthenticatedTls(Identity(index).TokenProvider,"127.0.0.1",port,auth.Certificate,policy);
        async Task Small(NativeQueryClient client,int connection,int value,CancellationToken cancellation){
            if(await client.ExecuteScalarAsync<int,int>("probe",Identity(connection).Query,"fixture",auth.Fingerprint,value,cancellation)!=value+1){throw new InvalidDataException("Unexpected capacity probe response.");}
        }
        Task SmallAt(int index,CancellationToken cancellation){
            var connection=index%maximum;
            return Small(clients[connection]!,connection,index,cancellation);
        }
        int CountEstablished()=>CountTcp(TcpState.Established);
        int CountTcp(TcpState state)=>IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Count(connection=>
            connection.RemoteEndPoint.Port==port&&connection.State==state&&IPAddress.IsLoopback(connection.RemoteEndPoint.Address));
    }

    private static CapacityFailure Failure(int index,string stage,Exception error){
        var socket=error as SocketException??error.InnerException as SocketException;
        return new(index,stage,error.GetType().Name,socket?.SocketErrorCode.ToString(),socket?.NativeErrorCode,
            (error as NativeAuthenticationException)?.Error.ToString(),error is OperationCanceledException);
    }

    private static object? Summarize(double[] samples){
        var measured=samples.Where(static time=>time>0).Order().ToArray();
        if(measured.Length==0){return null;}
        double Percentile(double fraction)=>measured[(int)Math.Ceiling(fraction*measured.Length)-1];
        return new{Count=measured.Length,P50Ms=Percentile(0.50),P95Ms=Percentile(0.95),P99Ms=Percentile(0.99)};
    }

    private sealed record CapacityFailure(int Index,string Stage,string Type,string? SocketError,int? NativeErrorCode,string? AuthenticationError,bool TimedOut);
}
