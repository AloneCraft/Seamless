using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

internal static partial class TransportProbe{
    private static async Task<bool> MeasureAuthenticatedChurnAsync(NativeQueryClient?[] clients,int parallelism,int intervalSeconds,int port,
        Func<int,NativeQueryClient> createClient,Func<int,PolicyProbeIdentity> identity,
        Func<NativeQueryClient,int,int,CancellationToken,Task> small,Func<Task<Snapshot>> capture,
        Func<int> countEstablished,Func<int> countTimeWait,List<object> results,CancellationToken token){
        var maximum=clients.Length;
        var batch=Math.Min(500,Math.Max(1,maximum/10));
        var rounds=intervalSeconds==0?Math.Min(5,(maximum-1)/batch):30;
        var retained=clients[^1]!;
        var limits=new ParallelOptions{MaxDegreeOfParallelism=parallelism,CancellationToken=token};
        var started=Stopwatch.GetTimestamp();
        for(var round=0;round<rounds;round++){
            var scheduled=TimeSpan.FromSeconds(round*intervalSeconds);
            var delay=scheduled-Stopwatch.GetElapsedTime(started);
            if(delay>TimeSpan.Zero){await Task.Delay(delay,token);}
            var elapsedAtStart=Stopwatch.GetElapsedTime(started).TotalSeconds;
            var before=await capture();
            var start=round*batch%(maximum-1);
            var indices=Enumerable.Range(0,batch).Select(offset=>(start+offset)%(maximum-1)).ToArray();
            foreach(var index in indices){
                await clients[index]!.DisposeAsync();
                clients[index]=null;
            }

            var expected=maximum-batch;
            var releasedAt=Stopwatch.GetTimestamp();
            var afterRelease=await capture();
            var openAfterRelease=countEstablished();
            while((afterRelease.ActiveAuthenticatedSessions!=expected||openAfterRelease!=expected)
                &&Stopwatch.GetElapsedTime(releasedAt)<TimeSpan.FromSeconds(15)){
                await Task.Delay(TimeSpan.FromMilliseconds(100),token);
                afterRelease=await capture();
                openAfterRelease=countEstablished();
            }
            var releaseMs=Stopwatch.GetElapsedTime(releasedAt).TotalMilliseconds;
            var released=afterRelease.ActiveAuthenticatedSessions==expected&&openAfterRelease==expected;
            var failures=new ConcurrentQueue<CapacityFailure>();
            ChurnFailureEvidence? firstReconnectFailure=null;
            var failureRecorded=0;
            var reconnectTimes=new double[batch];
            var retainedQueries=0;
            var verified=0;
            if(released){
                using var monitorStop=new CancellationTokenSource();
                var monitoring=MonitorRetainedAsync(monitorStop.Token);
                try{
                    await Parallel.ForEachAsync(Enumerable.Range(0,batch),limits,async(offset,cancellation)=>{
                        var index=indices[offset];
                        var client=createClient(index);
                        var since=Stopwatch.GetTimestamp();
                        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                        deadline.CancelAfter(TimeSpan.FromSeconds(20));
                        var stage="churn-connect";
                        try{
                            await client.ConnectAsync(deadline.Token);
                            if(client.AuthenticatedPrincipal!=identity(index).Principal){
                                throw new InvalidDataException("Unexpected authenticated identity after reconnect.");
                            }
                            stage="churn-rpc";
                            await small(client,index,index,deadline.Token);
                            reconnectTimes[offset]=Stopwatch.GetElapsedTime(since).TotalMilliseconds;
                            clients[index]=client;
                            Interlocked.Increment(ref verified);
                        }catch(Exception error)when(!cancellation.IsCancellationRequested){
                            failures.Enqueue(Failure(index,stage,error));
                            if(Interlocked.CompareExchange(ref failureRecorded,1,0)==0){
                                firstReconnectFailure=await ReadChurnFailureEvidenceAsync(index,stage,port,error,cancellation);
                            }
                        }finally{
                            if(clients[index] is null){await client.DisposeAsync();}
                        }
                    });
                }finally{
                    monitorStop.Cancel();
                    await monitoring;
                }
            }

            var afterReconnect=await capture();
            var openAfterReconnect=countEstablished();
            results.Add(new{Case="authenticated-churn-round",Round=round+1,StartIndex=start,ScheduledSeconds=scheduled.TotalSeconds,
                ElapsedAtStartSeconds=elapsedAtStart,ElapsedAfterReconnectSeconds=Stopwatch.GetElapsedTime(started).TotalSeconds,
                Replacements=batch,Verified=verified,RetainedQueries=retainedQueries,TimeWaitAfterReconnect=countTimeWait(),
                OpenAfterRelease=openAfterRelease,ReleaseMs=releaseMs,OpenAfterReconnect=openAfterReconnect,
                Before=before,AfterRelease=afterRelease,AfterReconnect=afterReconnect,
                ReconnectAndRpcLatency=Summarize(reconnectTimes),Failures=failures.ToArray(),FirstReconnectFailure=firstReconnectFailure});
            if(!released||verified!=batch||retainedQueries==0||!failures.IsEmpty||openAfterReconnect!=maximum
                ||before.ActiveAuthenticatedSessions!=maximum||afterReconnect.ActiveAuthenticatedSessions!=maximum
                ||afterReconnect.AuthenticatedSessions-before.AuthenticatedSessions!=batch){return false;}

            async Task MonitorRetainedAsync(CancellationToken stop){
                try{
                    do{
                        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
                        deadline.CancelAfter(TimeSpan.FromSeconds(10));
                        await small(retained,maximum-1,retainedQueries,deadline.Token);
                        retainedQueries++;
                        await Task.Delay(TimeSpan.FromMilliseconds(25),stop);
                    }while(!stop.IsCancellationRequested);
                }catch(OperationCanceledException error)when(error.CancellationToken==stop&&stop.IsCancellationRequested){}
                catch(Exception error)when(!token.IsCancellationRequested){
                    failures.Enqueue(Failure(maximum-1,"churn-retained-rpc",error));
                }
            }
        }
        return true;
    }

    private static async Task<ChurnFailureEvidence> ReadChurnFailureEvidenceAsync(int index,string stage,int port,Exception error,CancellationToken cancellation){
        var observedUtc=DateTimeOffset.UtcNow;
        var socket=error as SocketException??error.InnerException as SocketException;
        var frames=new StackTrace(socket??error,false).GetFrames().Select(static frame=>frame.GetMethod())
            .Where(static method=>method?.DeclaringType?.FullName is{} type
                &&(type.StartsWith("System.Net.",StringComparison.Ordinal)||type.StartsWith("Seamless.Database.Client.",StringComparison.Ordinal)))
            .Select(static method=>method!.DeclaringType!.FullName+"."+method.Name).ToArray();
        ChurnFailureEvidence evidence;
        try{
            var ipv4=IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Where(static connection=>
                connection.LocalEndPoint.AddressFamily==AddressFamily.InterNetwork).ToArray();
            var target=ipv4.Where(connection=>connection.RemoteEndPoint.Port==port&&IPAddress.IsLoopback(connection.RemoteEndPoint.Address)).ToArray();
            evidence=new(index,stage,observedUtc,port,frames,target.GroupBy(static item=>item.State).ToDictionary(static group=>group.Key,static group=>group.Count()),
                target.Select(static item=>item.LocalEndPoint.Port).Distinct().Count(),
                ipv4.Count(static item=>item.State==TcpState.TimeWait),null);
        }catch(NetworkInformationException observationError){
            evidence=new(index,stage,observedUtc,port,frames,null,null,null,observationError.ErrorCode);
        }
        return evidence with{WindowsTcp=await ObserveWindowsTcpAsync(port,cancellation)};
    }

    private static async Task<WindowsTcpObservation?> ObserveWindowsTcpAsync(int port,CancellationToken cancellation){
        var executable=Environment.GetEnvironmentVariable("SEAMLESS_PROBE_TCP_OBSERVER_POWERSHELL");
        var script=Environment.GetEnvironmentVariable("SEAMLESS_PROBE_TCP_OBSERVER_SCRIPT");
        var ranges=Environment.GetEnvironmentVariable("SEAMLESS_PROBE_TCP_OBSERVER_RANGES")?.Split(',');
        if(!OperatingSystem.IsWindows()||executable is null||script is null||ranges is null){return null;}
        if(!Path.IsPathFullyQualified(executable)||!Path.IsPathFullyQualified(script)||ranges.Length!=4){
            return new(null,"InvalidObserverConfiguration");
        }
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{"-NoProfile","-NonInteractive","-File",script,port.ToString(CultureInfo.InvariantCulture),
            ranges[0],ranges[1],ranges[2],ranges[3]}){start.ArgumentList.Add(argument);}
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try{
            using var observer=Process.Start(start)??throw new InvalidOperationException("Could not start the TCP observer.");
            try{
                var output=observer.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors=observer.StandardError.ReadToEndAsync(timeout.Token);
                await observer.WaitForExitAsync(timeout.Token);
                await errors;
                var json=await output;
                if(observer.ExitCode!=0){return new(null,$"ObserverExit{observer.ExitCode}");}
                using var document=JsonDocument.Parse(json);
                return new(document.RootElement.Clone(),null);
            }finally{
                if(!observer.HasExited){observer.Kill(entireProcessTree:true);await observer.WaitForExitAsync();}
            }
        }catch(Exception error)when(error is Win32Exception or IOException or InvalidOperationException or OperationCanceledException or JsonException){
            return new(null,error.GetType().Name);
        }
    }

    private sealed record ChurnFailureEvidence(int Index,string Stage,DateTimeOffset ObservedUtc,int TargetPort,string[] NetworkStack,
        Dictionary<TcpState,int>? TargetStates,int? DistinctTargetLocalPorts,int? HostIpv4TimeWait,int? ObservationError){
        public WindowsTcpObservation? WindowsTcp{get;init;}
    }
    private sealed record WindowsTcpObservation(JsonElement? Snapshot,string? Error);
}
