using Seamless.Execution;
using Scheduler=Seamless.Execution.CpuScheduler;
namespace Execution.Tests;

public sealed class CpuScheduler{
    [Fact]
    public async Task HostSlotsBoundConcurrentChunksAndAreReturnedAfterCancellationAndExceptions(){
        var scheduler=Scheduler.Shared;var capacity=scheduler.Usage.Capacity;
        using var entered=new CountdownEvent(capacity);using var finish=new ManualResetEventSlim();using var cancelled=new CancellationTokenSource();
        var workers=Enumerable.Range(0,capacity).Select(_=>Task.Run(async()=>await scheduler.RunAsync(()=>{entered.Signal();if(!finish.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}return 1;}))).ToArray();
        Task<int>? waiting=null;
        try{
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));Assert.Equal(capacity,scheduler.Usage.Active);
            waiting=Task.Run(async()=>await scheduler.RunAsync<int>(()=>throw new InvalidOperationException("A cancelled waiter ran."),cancelled.Token));
            cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await waiting);Assert.Equal(capacity,scheduler.Usage.Active);
        }finally{finish.Set();await Task.WhenAll(workers);}
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await scheduler.RunAsync<int>(()=>throw new InvalidOperationException()));
        Assert.Equal(0,scheduler.Usage.Active);Assert.Equal(0,scheduler.Usage.Outstanding);Assert.True(scheduler.Usage.PeakActive<=capacity);
    }
    [Fact]
    public async Task IoWaitReturnsSlotsAndCallbacksReacquireThemWithoutCreatingNestedWorkers(){
        var scheduler=Scheduler.Shared;var capacity=scheduler.Usage.Capacity;
        using var waiting=new CountdownEvent(capacity);using var finish=new ManualResetEventSlim();
        var workers=Enumerable.Range(0,capacity).Select(_=>Task.Run(async()=>await scheduler.RunAsync(()=>{
            using(var io=Scheduler.SuspendForIo()){
                waiting.Signal();if(!finish.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}
                using var callback=Scheduler.EnterCallback();Assert.True(scheduler.IsExecuting);
            }
            return scheduler.RunAsync(()=>1).GetAwaiter().GetResult();
        }))).ToArray();
        try{Assert.True(waiting.Wait(TimeSpan.FromSeconds(10)));Assert.Equal(0,scheduler.Usage.Active);Assert.Equal(7,await scheduler.RunAsync(()=>7));}
        finally{finish.Set();await Task.WhenAll(workers);}
        Assert.Equal(0,scheduler.Usage.Active);Assert.Equal(0,scheduler.Usage.Outstanding);Assert.True(scheduler.Usage.PeakActive<=capacity);
    }
}
