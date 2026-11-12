using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class QueryTraceCore{
    [Fact]public async Task InFlightSampleEnqueueIsSerializedWithStopAndTheTerminalFrame(){
        var clock=new Clock();var events=new List<QueryTraceFrame>();var id=Guid.NewGuid();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture=Capture(clock,id,new(),(frame,_,_)=>{
            if(frame.Kind=="Sample"){entered.SetResult();release.Task.GetAwaiter().GetResult();}
            events.Add(frame);return true;
        });
        clock.Advance(TimeSpan.FromMilliseconds(100));var sampling=Task.Run(capture.Sample);Task? ending=null;
        try{
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // sink内の一時停止中も停止境界と同じgateを保持することを、時間待ちに依存せず検査する。
            var gate=typeof(QueryTraceCapture).GetField("gate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(capture)!;
            var acquired=Monitor.TryEnter(gate);if(acquired){Monitor.Exit(gate);}Assert.False(acquired);
            ending=Task.Run(()=>{capture.StopSampling();capture.End(Completed(id,clock),CancellationToken.None);});
        }finally{release.TrySetResult();await sampling;if(ending is not null){await ending;}}
        capture.Sample();capture.End(Completed(id,clock),CancellationToken.None);
        Assert.Equal(new[]{"Sample","Completed"},events.Select(frame=>frame.Kind));
        Assert.Equal(new long[]{1,2},events.Select(frame=>frame.Sequence));
    }

    [Fact]public async Task EvictedTargetRefreshesEvenWhenItsPulseWasSkippedAtCapacity(){
        var budget=new ResourceBudget(1<<20,0);var hub=new QueryTraceNotifications(budget,maximumTargets:1);
        try{
            var old=await hub.Watch(new("definition","First")).WaitAsync(CancellationToken.None);
            var other=await hub.Watch(new("definition","Second")).WaitAsync(CancellationToken.None);
            using var waiting=hub.Watch(new("definition","Second",other.HubSession,other.Revision));
            hub.Pulse("definition","First");
            waiting.Dispose();
            // 失われた対象のrevisionが最新globalと同値でも、再作成は更新確認を促す。
            var changed=await hub.Watch(new("definition","First",old.HubSession,other.Revision)).WaitAsync(CancellationToken.None);
            Assert.True(changed.Changed);Assert.True(changed.Revision>other.Revision);
            var next=hub.Watch(new("definition","First",changed.HubSession,changed.Revision)).WaitAsync(CancellationToken.None);
            Assert.False(next.IsCompleted);hub.Pulse("definition","First");Assert.True((await next).Changed);
        }finally{hub.Close();}
        Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact]public void CompleteBoundaryRecordingRetainsTheCallingThread(){
        var clock=new Clock();var id=Guid.NewGuid();var owner=Environment.CurrentManagedThreadId;var calls=0;
        var capture=Capture(clock,id,new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),(_,reliability,_)=>{
            Assert.Equal(owner,Environment.CurrentManagedThreadId);Assert.Equal(QueryTraceReliability.Complete,reliability);calls++;return true;
        });
        capture.Started();capture.EnteredGate();capture.Bound(3,new('A',64));capture.Output();capture.StopSampling();capture.End(Completed(id,clock),CancellationToken.None);
        Assert.Equal(5,calls);
    }

    [Fact]public void SamplingCompleteIsExplicitlyUnsupportedAndFastSamplingUses100Milliseconds(){
        Assert.Equal(TimeSpan.FromMilliseconds(100),new QueryTraceOptions().EffectiveSampleInterval);
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>new QueryTraceOptions(QueryTraceRecordMode.TimeSampled,QueryTraceReliability.Complete).Validate()).Code);
        new QueryTraceOptions(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete).Validate();
        Assert.Equal("InvalidRequest",Assert.Throws<ManagementException>(()=>new QueryTraceOptions(SampleInterval:TimeSpan.Zero).Validate()).Code);
    }
    [Fact]public void BoundariesKeepActualIdentityAndDoNotInventSamplesOrNodeMeasurements(){
        var clock=new Clock();var events=new List<QueryTraceFrame>();var id=Guid.NewGuid();
        var capture=Capture(clock,id,new(QueryTraceRecordMode.BoundaryEvents),(frame,_,_)=>{events.Add(frame);return true;});
        capture.Started();clock.Advance(TimeSpan.FromMilliseconds(50));capture.EnteredGate();capture.Bound(3,new('A',64));capture.Invoking();capture.Output();
        clock.Advance(TimeSpan.FromMilliseconds(100));capture.StopSampling();capture.End(Completed(id,clock),CancellationToken.None);capture.Sample();
        Assert.Equal(new[]{"Started","GateAcquired","IdentityBound","FirstOutput","Completed"},events.Select(frame=>frame.Kind));
        Assert.Equal(Enumerable.Range(1,5).Select(value=>(long)value),events.Select(frame=>frame.Sequence));
        Assert.False(events[0].IdentityResolved);Assert.Null(events[0].Version);Assert.All(events.Skip(2),frame=>{Assert.True(frame.IdentityResolved);Assert.Equal(3,frame.Version);Assert.Equal(new string('A',64),frame.ArtifactHash);});
        Assert.Equal(1,events[^1].ObservedOutputCount);Assert.Equal(TimeSpan.FromMilliseconds(150),events[^1].Elapsed);
    }
    [Fact]public void TimeSamplesReportMissedIntervalsAndStopBeforeTheTerminalClock(){
        var clock=new Clock();var events=new List<QueryTraceFrame>();long late=0;var id=Guid.NewGuid();
        var capture=new QueryTraceCapture(id,"definition","Value",new(),clock.GetUtcNow(),clock.GetTimestamp(),(frame,_,_)=>{events.Add(frame);return true;},count=>late+=count,()=>throw new InvalidOperationException(),CancellationToken.None,clock);
        capture.Started();clock.Advance(TimeSpan.FromMilliseconds(50));capture.Sample();Assert.DoesNotContain(events,frame=>frame.Kind=="Sample");
        clock.Advance(TimeSpan.FromMilliseconds(50));capture.Sample();clock.Advance(TimeSpan.FromMilliseconds(500));capture.Sample();
        Assert.Equal(2,events.Count(frame=>frame.Kind=="Sample"));Assert.Equal(4,late);
        capture.StopSampling();clock.Advance(TimeSpan.FromSeconds(1));capture.Sample();capture.End(Completed(id,clock),CancellationToken.None);Assert.Equal(2,events.Count(frame=>frame.Kind=="Sample"));
    }
    [Fact]public async Task HostSamplerBoundsActiveTracesAndReleasesReservationsOnStop(){
        var clock=new Clock();var budget=new ResourceBudget(1<<20,0);var events=new List<QueryTraceFrame>();var failures=0;
        await using var sampler=new QueryTraceSampler(budget,()=>failures++,maximum:1,clock:clock);
        var capture=Capture(clock,Guid.NewGuid(),new(),(frame,_,_)=>{events.Add(frame);return true;});
        using var lease=sampler.Register(capture);Assert.NotNull(lease);Assert.True(budget.MemoryBytes>0);
        Assert.Null(sampler.Register(Capture(clock,Guid.NewGuid(),new(),(_,_,_)=>true)));Assert.Equal(1,failures);
        clock.Advance(TimeSpan.FromMilliseconds(100));Assert.Single(events);
        await sampler.DisposeAsync();Assert.Equal(0,budget.MemoryBytes);clock.Advance(TimeSpan.FromSeconds(1));Assert.Single(events);
    }
    [Fact]public async Task WatchCapacityCancellationPulseAndStopAreIndependentOfQueryExecutionSlots(){
        var budget=new ResourceBudget(1<<20,0);var hub=new QueryTraceNotifications(budget,maximumWatches:1);
        var initial=await hub.Watch(new("definition","Value")).WaitAsync(CancellationToken.None);
        using var cancellation=new CancellationTokenSource();
        var ticket=hub.Watch(new("definition","Value",initial.HubSession,initial.Revision));var pending=ticket.WaitAsync(cancellation.Token);Assert.False(pending.IsCompleted);
        Assert.Equal("Limit",Assert.Throws<ManagementException>(()=>hub.Watch(new("definition","Value",initial.HubSession,initial.Revision))).Code);
        cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        var next=hub.Watch(new("definition","Value",initial.HubSession,initial.Revision)).WaitAsync(CancellationToken.None);hub.Pulse("definition","Value");
        var changed=await next;Assert.True(changed.Changed);Assert.True(changed.Revision>initial.Revision);
        var stopping=hub.Watch(new("definition","Value",changed.HubSession,changed.Revision)).WaitAsync(CancellationToken.None);hub.Close();
        Assert.Equal("Stopped",(await Assert.ThrowsAsync<ManagementException>(()=>stopping)).Code);
        Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public void CancelledBoundaryWaitDoesNotCancelTheTerminalObservation(){
        var clock=new Clock();var id=Guid.NewGuid();var events=new List<QueryTraceFrame>();using var cancellation=new CancellationTokenSource();
        var capture=Capture(clock,id,new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete),(frame,_,token)=>{if(frame.Kind=="GateAcquired"){token.ThrowIfCancellationRequested();}events.Add(frame);return true;},cancellation.Token);
        capture.Started();cancellation.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>capture.EnteredGate());capture.StopSampling();
        capture.End(Completed(id,clock) with{Outcome="Cancelled",IdentityResolved=false,Version=null,ArtifactHash=null},CancellationToken.None);
        Assert.Equal("Completed",events[^1].Kind);Assert.Equal("Cancelled",events[^1].Outcome);
    }
    private static QueryTraceCapture Capture(Clock clock,Guid id,QueryTraceOptions options,Func<QueryTraceFrame,QueryTraceReliability,CancellationToken,bool> write,CancellationToken cancellation=default)=>new(id,"definition","Value",options,clock.GetUtcNow(),clock.GetTimestamp(),write,_=>{},()=>throw new InvalidOperationException("unexpected capture failure"),cancellation,clock);
    private static QueryExecutionObservation Completed(Guid id,Clock clock)=>new(id,"definition","Value",3,new('A',64),true,DateTimeOffset.UnixEpoch,clock.GetUtcNow(),clock.GetElapsedTime(0),TimeSpan.Zero,null,1,1,false,"Succeeded","Execution",null);
    private sealed class Clock:TimeProvider{
        private long ticks;
        private readonly List<Timer> timers=[];
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>this.ticks;
        public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UnixEpoch.AddTicks(this.ticks);
        public void Advance(TimeSpan value){this.ticks+=value.Ticks;foreach(var timer in this.timers.ToArray()){timer.Fire();}}
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period){var timer=new Timer(this,callback,state);this.timers.Add(timer);timer.Change(dueTime,period);return timer;}
        private sealed class Timer(Clock clock,TimerCallback callback,object? state):ITimer{
            private long due=long.MaxValue;private TimeSpan period;private bool disposed;
            public bool Change(TimeSpan dueTime,TimeSpan period){if(this.disposed){return false;}this.period=period;this.due=dueTime==Timeout.InfiniteTimeSpan?long.MaxValue:clock.ticks+dueTime.Ticks;return true;}
            internal void Fire(){if(this.disposed||clock.ticks<this.due){return;}this.due=this.period>TimeSpan.Zero?clock.ticks+this.period.Ticks:long.MaxValue;callback(state);}
            public void Dispose()=>this.disposed=true;
            public ValueTask DisposeAsync(){this.Dispose();return ValueTask.CompletedTask;}
        }
    }
}
