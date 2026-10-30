namespace TestSeamless.Tests;

public sealed class ComparisonConcurrency{
    [Theory][InlineData(1)][InlineData(4)][InlineData(16)]public async Task RunAsync(int concurrency){
        using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var active=0;
        var operation=Task.Run(()=>global::TestSeamless.ComparisonConcurrency.RunAsync(concurrency,()=>{
            if(Interlocked.Increment(ref active)==concurrency){entered.TrySetResult();}
            if(!release.Wait(TimeSpan.FromSeconds(15))){throw new TimeoutException("Synchronous queries did not overlap.");}
            return Task.FromResult(1.0);
        }));
        try{await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(concurrency,Volatile.Read(ref active));}
        finally{release.Set();}
        var samples=await operation.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(concurrency,samples.Length);Assert.All(samples,value=>Assert.Equal(1.0,value));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>global::TestSeamless.ComparisonConcurrency.RunAsync(concurrency,()=>throw new InvalidOperationException("measurement failed")));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>global::TestSeamless.ComparisonConcurrency.RunAsync(0,()=>Task.FromResult(0.0)));
    }
}
