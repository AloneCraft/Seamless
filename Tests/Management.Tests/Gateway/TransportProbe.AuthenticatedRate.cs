using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Seamless.Database.Gateway.Tests;

internal static partial class TransportProbe{
    private sealed record RateScheduleMiss(double ElapsedSeconds,int FirstSkippedBatch,int ResumedBatch,int MissedRequests,
        double ScheduleDelayMs,double LastWaitRequestedMs,double LastWaitElapsedMs,double? DelayTaskObservedMs,double? ResumeAfterObservationMs,
        long PendingThreadPoolWorkItems,
        int ThreadPoolThreads,int Gen0,int Gen1,int Gen2);
    private sealed record RateQueueFull(double ElapsedSeconds,int Batch,int RejectedRequests,int Queued,int Issued,int Succeeded,int Failed);

    private static async Task<bool> MeasureAuthenticatedRateAsync(int rate,int durationSeconds,int connections,int parallelism,
        Func<int,CancellationToken,Task> execute,Func<Task<Snapshot>> capture,List<object> results,CancellationToken token,int expectedRefreshCycles=0,bool sharedIssuer=false){
        const int batchesPerSecond=40,queueCapacity=512;
        var offered=rate*durationSeconds;
        var batchCount=durationSeconds*batchesPerSecond;
        var latency=new double[offered];
        var rpcLatency=new double[offered];
        var failures=new ConcurrentQueue<CapacityFailure>();
        var samples=new List<object>();
        var scheduleMisses=new List<RateScheduleMiss>();
        var queueSaturations=new List<RateQueueFull>();
        var queue=Channel.CreateBounded<(int Index,long Scheduled)>(new BoundedChannelOptions(queueCapacity){SingleWriter=true});
        var issued=0;
        var succeeded=0;
        var failed=0;
        var missed=0;
        var full=0;
        using var monitor=CancellationTokenSource.CreateLinkedTokenSource(token);
        var serverBefore=await capture();
        var clientBefore=MeasureClient();
        var started=Stopwatch.GetTimestamp();
        var reader=Task.WhenAll(Enumerable.Range(0,parallelism).Select(_=>ConsumeAsync()));
        var monitorTask=SampleAsync();
        try{
            var batch=0;
            while(batch<batchCount){
                token.ThrowIfCancellationRequested();
                var scheduled=started+batch*Stopwatch.Frequency/batchesPerSecond;
                var now=Stopwatch.GetTimestamp();
                var until=Stopwatch.GetElapsedTime(now,scheduled);
                var lastWait=TimeSpan.Zero;
                var lastWaitElapsed=TimeSpan.Zero;
                double? delayObservedMs=null;
                double? resumeAfterObservationMs=null;
                while(until>TimeSpan.Zero){
                    lastWait=until<TimeSpan.FromMilliseconds(1)?TimeSpan.FromMilliseconds(1):until;
                    var beforeWait=now;
                    var completion=Task.Delay(lastWait,token).ContinueWith(static delay=>{
                        var timestamp=Stopwatch.GetTimestamp();
                        delay.GetAwaiter().GetResult();
                        return timestamp;
                    },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                    var observed=await completion;
                    now=Stopwatch.GetTimestamp();
                    lastWaitElapsed=Stopwatch.GetElapsedTime(beforeWait,now);
                    delayObservedMs=Stopwatch.GetElapsedTime(beforeWait,observed).TotalMilliseconds;
                    resumeAfterObservationMs=Stopwatch.GetElapsedTime(observed,now).TotalMilliseconds;
                    until=Stopwatch.GetElapsedTime(now,scheduled);
                }
                var elapsedAtDispatch=Stopwatch.GetElapsedTime(started,now);
                var currentBatch=Math.Min(batchCount,(int)(elapsedAtDispatch.TotalSeconds*batchesPerSecond));
                if(currentBatch>batch){
                    // 古い到着枠をまとめて発行せず、予定との差として記録する。
                    var missedRequests=currentBatch*rate/batchesPerSecond-batch*rate/batchesPerSecond;
                    missed+=missedRequests;
                    scheduleMisses.Add(new(elapsedAtDispatch.TotalSeconds,batch,currentBatch,missedRequests,-until.TotalMilliseconds,
                        lastWait.TotalMilliseconds,lastWaitElapsed.TotalMilliseconds,delayObservedMs,resumeAfterObservationMs,
                        ThreadPool.PendingWorkItemCount,ThreadPool.ThreadCount,
                        GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)));
                    batch=currentBatch;
                    if(batch==batchCount){break;}
                    scheduled=started+batch*Stopwatch.Frequency/batchesPerSecond;
                }
                var end=(batch+1)*rate/batchesPerSecond;
                var rejected=0;
                RateQueueFull? saturation=null;
                for(var index=batch*rate/batchesPerSecond;index<end;index++){
                    if(!queue.Writer.TryWrite((index,scheduled))){
                        rejected++;
                        saturation??=new(Stopwatch.GetElapsedTime(started).TotalSeconds,batch,0,queue.Reader.Count,
                            Volatile.Read(ref issued),Volatile.Read(ref succeeded),Volatile.Read(ref failed));
                    }
                }
                if(saturation is not null){
                    full+=rejected;
                    queueSaturations.Add(saturation with{RejectedRequests=rejected});
                }
                batch++;
            }
            var remaining=TimeSpan.FromSeconds(durationSeconds)-Stopwatch.GetElapsedTime(started);
            if(remaining>TimeSpan.Zero){await Task.Delay(remaining,token);}
        }finally{
            queue.Writer.TryComplete();
            try{await reader;}
            finally{
                await monitor.CancelAsync();
                await monitorTask;
            }
        }
        var elapsed=Stopwatch.GetElapsedTime(started).TotalSeconds;
        var serverAfter=await capture();
        var clientAfter=MeasureClient();
        var newSessions=serverAfter.AuthenticatedSessions-serverBefore.AuthenticatedSessions;
        var membershipRefreshes=serverAfter.MembershipRefreshes-serverBefore.MembershipRefreshes;
        var issuerChecks=serverAfter.IssuerMembershipChecks-serverBefore.IssuerMembershipChecks;
        var sessionsRefreshedTwice=serverAfter.SessionsRefreshedTwice-serverBefore.SessionsRefreshedTwice;
        var passed=issued==offered&&succeeded==offered&&full==0&&missed==0&&newSessions==0;
        if(expectedRefreshCycles>0){
            var expectedChecks=connections*expectedRefreshCycles;
            var issuerMatches=issuerChecks==expectedChecks||sharedIssuer&&issuerChecks>=expectedChecks;
            passed=passed&&membershipRefreshes==expectedChecks&&issuerMatches&&serverAfter.ActiveAuthenticatedSessions==connections;
        }
        if(expectedRefreshCycles==2){
            passed=passed&&sessionsRefreshedTwice==connections;
        }
        results.Add(new{Case="authenticated-rate",Connections=connections,Parallelism=parallelism,Rate=rate,DurationSeconds=durationSeconds,
            QueueCapacity=queueCapacity,BatchesPerSecond=batchesPerSecond,Offered=offered,Issued=issued,Succeeded=succeeded,MissedSchedule=missed,QueueFull=full,
            Failed=issued-succeeded,FailureSamples=failures.ToArray(),ElapsedSeconds=elapsed,IssuedPerSecond=issued/elapsed,CompletedPerSecond=succeeded/elapsed,
            NewAuthenticatedSessions=newSessions,EndToEndLatency=Summarize(latency),RpcLatency=Summarize(rpcLatency),
            ExpectedRefreshCycles=expectedRefreshCycles,MembershipRefreshes=membershipRefreshes,IssuerMembershipChecks=issuerChecks,SharedIssuer=sharedIssuer,SessionsRefreshedTwice=sessionsRefreshedTwice,
            ServerBefore=serverBefore,ServerAfter=serverAfter,ClientBefore=clientBefore,ClientAfter=clientAfter,Samples=samples,
            ScheduleMisses=scheduleMisses,QueueSaturations=queueSaturations,Passed=passed});
        return passed;

        async Task ConsumeAsync(){
            await foreach(var (index,scheduled) in queue.Reader.ReadAllAsync(token)){
                Interlocked.Increment(ref issued);
                var sent=Stopwatch.GetTimestamp();
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                try{
                    await execute(index,deadline.Token);
                    var finished=Stopwatch.GetTimestamp();
                    latency[index]=Stopwatch.GetElapsedTime(scheduled,finished).TotalMilliseconds;
                    rpcLatency[index]=Stopwatch.GetElapsedTime(sent,finished).TotalMilliseconds;
                    Interlocked.Increment(ref succeeded);
                }catch(Exception error)when(!token.IsCancellationRequested){
                    Interlocked.Increment(ref failed);
                    if(failures.Count<64){failures.Enqueue(Failure(index,"rate-rpc",error));}
                }
            }
        }
        async Task SampleAsync(){
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
            try{
                while(await timer.WaitForNextTickAsync(monitor.Token)){
                    samples.Add(new{Seconds=Stopwatch.GetElapsedTime(started).TotalSeconds,Issued=Volatile.Read(ref issued),
                        Succeeded=Volatile.Read(ref succeeded),Server=await capture(),Client=MeasureClient()});
                }
            }catch(OperationCanceledException)when(monitor.IsCancellationRequested){}
        }
    }
}
