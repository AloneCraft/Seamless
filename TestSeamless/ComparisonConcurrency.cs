namespace TestSeamless;

public static class ComparisonConcurrency{
    public static async Task<double[]> RunAsync(int concurrency,Func<Task<double>> measure){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);ArgumentNullException.ThrowIfNull(measure);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var arrived=0;var workers=new Task<double>[concurrency];
        try{
            for(var i=0;i<concurrency;i++){
                // 同期で完了するネイティブクエリも、全ワーカーの準備後に各スレッドで開始する。
                workers[i]=Task.Factory.StartNew(()=>{
                    if(Interlocked.Increment(ref arrived)==concurrency){ready.TrySetResult();}
                    start.Task.GetAwaiter().GetResult();return measure().GetAwaiter().GetResult();
                },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            }
            await ready.Task.ConfigureAwait(false);start.SetResult();return await Task.WhenAll(workers).ConfigureAwait(false);
        }catch{start.TrySetCanceled();throw;}
    }
}
