using Seamless.Collections.Iteration;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions.Collections;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class VersionedTableTests{
    private static Task OutsideCurrent(Func<Task> action){using(ExecutionContext.SuppressFlow()){return Task.Run(action);}}
    [Fact]
    public async Task RealTsavoriteTablesShareASnapshotAndMergePrivateChangesIntoBothIterationPaths(){
        await using var fixture=new Fixture();await fixture.Seed();
        await using var tx=await fixture.Owner.BeginAsync();var left=fixture.Left.For(tx);var right=fixture.Right.For(tx);
        Assert.Equal(100,left[1].Balance);left[1]=(1,70);Assert.True(left.Remove(1));Assert.True(left.Add((1,70)));Assert.Equal(70,left[1].Balance);Assert.True(left.Remove(2));Assert.True(left.Add((100,500)));
        await OutsideCurrent(async()=>{await using var other=await fixture.Owner.BeginAsync();fixture.Right.For(other)[1]=(1,200);await other.CommitAsync();});
        Assert.Equal(100,right[1].Balance);
        var expected=left.Sum(static row=>row.Balance);Assert.Equal(6770,expected);
        var sum=new Sum();left.Rows.Iterate(ref sum);Assert.Equal(expected,sum.Value);Assert.Equal(64,sum.Count);
        var joined=new LookupSum(right);left.Rows.Iterate(ref joined);Assert.Equal(63,joined.Found);Assert.Equal(6300,joined.Value);
        Assert.Equal(TransactionAbortReason.Conflict,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await tx.CommitAsync())).Reason);
        await using(var fresh=await fixture.Owner.BeginAsync()){
            var update=fixture.Left.For(fresh);update[1]=(1,70);Assert.True(update.Remove(1));Assert.True(update.Add((1,70)));Assert.True(update.Remove(2));Assert.True(update.Add((100,500)));await fresh.CommitAsync();
        }
        await using var current=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(200,fixture.Right.For(current)[1].Balance);Assert.Equal(70,fixture.Left.For(current)[1].Balance);
    }

    [Fact]
    public async Task AnOpenEnumerationBlocksInstallationButDisposalLetsTheWriterComplete(){
        await using var fixture=new Fixture();await fixture.Seed();await using var read=await fixture.Owner.BeginReadOnlyAsync();
        var iterator=fixture.Left.For(read).GetEnumerator();Assert.True(iterator.MoveNext());
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer=OutsideCurrent(async()=>{await using var tx=await fixture.Owner.BeginAsync();fixture.Left.For(tx)[1]=(1,200);started.SetResult();await tx.CommitAsync();});
        try{await started.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(writer.IsCompleted);}
        finally{iterator.Dispose();await writer.WaitAsync(TimeSpan.FromSeconds(5));}
        Assert.Equal(100,fixture.Left.For(read)[1].Balance);
    }

    [Fact]
    public async Task HistoryPressureAbortsTheOldReaderReclaimsAndRequiresAnExplicitNewWriter(){
        await using var fixture=new Fixture(512);await fixture.Seed();
        await using var old=await fixture.Owner.BeginReadOnlyAsync();var retained=fixture.Left.For(old);Assert.Equal(100,retained[1].Balance);
        await OutsideCurrent(async()=>{await using var tx=await fixture.Owner.BeginAsync();fixture.Left.For(tx)[1]=(1,90);await tx.CommitAsync();});
        Assert.Equal(100,retained[1].Balance);
        await OutsideCurrent(async()=>{
            await using var tx=await fixture.Owner.BeginAsync();fixture.Left.For(tx)[1]=(1,80);
            Assert.Equal(TransactionAbortReason.HistoryPressure,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await tx.CommitAsync())).Reason);
        });
        await OutsideCurrent(async()=>{await using var verify=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(90,fixture.Left.For(verify)[1].Balance);});
        Assert.Equal(TransactionAbortReason.HistoryPressure,Assert.Throws<TransactionAbortedException>(()=>retained.ContainsKey(1)).Reason);
        Assert.Null(fixture.Owner.Failure.Error);Assert.Equal(0,fixture.Owner.GetDomainDiagnostics().HistoryMemoryBytes);
        await OutsideCurrent(async()=>{await using var retry=await fixture.Owner.BeginAsync();fixture.Left.For(retry)[1]=(1,80);await retry.CommitAsync();});
    }

    [Fact]
    public async Task CommitClosesItsOwnSuspendedEnumerationBeforeInstalling(){
        await using var fixture=new Fixture();await fixture.Seed();await using var tx=await fixture.Owner.BeginAsync();var rows=fixture.Left.For(tx);
        using var iterator=rows.GetEnumerator();Assert.True(iterator.MoveNext());rows[1]=(1,80);
        var commit=Task.Run(async()=>await tx.CommitAsync());var completed=false;
        try{completed=ReferenceEquals(commit,await Task.WhenAny(commit,Task.Delay(TimeSpan.FromSeconds(3))));}
        finally{iterator.Dispose();await commit.WaitAsync(TimeSpan.FromSeconds(5));}
        Assert.True(completed,"The commit waited for a structural lease retained by its own enumeration.");
        await using var verify=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(80,fixture.Left.For(verify)[1].Balance);
    }

    [Fact]
    public async Task ReclamationReleasesAbortedReadersBuffersBeforeTheCallerDisposesItsEnumerator(){
        await using var fixture=new Fixture();await fixture.Seed();await using var old=await fixture.Owner.BeginReadOnlyAsync();var rows=fixture.Left.For(old);
        Assert.Equal(100,rows[1].Balance);var baseline=fixture.MemoryBytes;
        using var iterator=rows.GetEnumerator();Assert.True(iterator.MoveNext());Assert.True(fixture.MemoryBytes>=baseline+(512<<10));
        await OutsideCurrent(async()=>await fixture.Owner.ReclaimHistoryAsync());
        Assert.True(fixture.MemoryBytes<baseline+(64<<10),"An aborted enumeration retained its private scan buffer reservation.");
        Assert.Equal(TransactionAbortReason.HistoryPressure,Assert.Throws<TransactionAbortedException>(()=>iterator.MoveNext()).Reason);
    }

    private struct Sum:IStrategy<Sum,(int Id,long Balance)>{
        internal long Value;internal int Count;
        public static bool Invoke(ref Sum self,(int Id,long Balance) row){self.Value=checked(self.Value+row.Balance);self.Count++;return true;}
    }
    [Fact]
    public async Task QueryOperationKeepsTheSnapshotProtectedThroughWorkerCompletionAndFinalMerging(){
        await using var fixture=new Fixture();await fixture.Seed();await using var read=await fixture.Owner.BeginReadOnlyAsync();var rows=fixture.Left.For(read).Rows;
        using var afterScan=new ManualResetEventSlim();using var finish=new ManualResetEventSlim();
        var query=Task.Run(()=>rows.ExecuteQuery(()=>{
            using var resources=rows.CreateQueryScope();using var memory=resources.Budget.ReserveMemory(64);
            var sum=new Sum();rows.Iterate(ref sum);afterScan.Set();if(!finish.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}resources.ThrowIfUnavailable();return sum.Count;
        }));
        Task? reclaim=null;var retained=false;
        try{
            Assert.True(await Task.Run(()=>afterScan.Wait(TimeSpan.FromSeconds(10))));
            // 一つのquery内のworker保持と、同じ取引への別の公開操作を区別する。
            Assert.Throws<InvalidOperationException>(()=>fixture.Right.For(read).ContainsKey(1));
            Assert.Throws<InvalidOperationException>(()=>rows.ExecuteQuery(static()=>0));
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await read.CommitAsync());
            reclaim=OutsideCurrent(async()=>await fixture.Owner.ReclaimHistoryAsync());var timer=System.Diagnostics.Stopwatch.StartNew();
            while(!fixture.Owner.GetDomainDiagnostics().Reclaiming&&timer.Elapsed<TimeSpan.FromSeconds(2)){await Task.Delay(1);}
            retained=fixture.Owner.GetDomainDiagnostics().ActiveOperations>0&&!reclaim.IsCompleted;
        }finally{
            finish.Set();try{await query.WaitAsync(TimeSpan.FromSeconds(10));}catch(TransactionAbortedException error){Assert.Equal(TransactionAbortReason.HistoryPressure,error.Reason);}
            if(reclaim is not null){await reclaim.WaitAsync(TimeSpan.FromSeconds(10));}
        }
        Assert.True(retained);Assert.Equal(0,fixture.Owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(fixture.Owner.Failure.Error);
    }
    [Fact]
    public async Task ParallelScanPartitionsTheSnapshotAndPrivateChangesUseTheSequentialOverlay(){
        await using var fixture=new Fixture();await fixture.Seed(128);
        await using(var read=await fixture.Owner.BeginReadOnlyAsync()){
            var rows=fixture.Left.For(read);var expected=rows.Sum(static row=>row.Balance);
            using var probe=new WorkerProbe();var result=new ParallelSum(probe);
            var query=Task.Run(()=>{rows.Rows.IterateParallel(ref result);});
            try{Assert.True(await Task.Run(()=>probe.Started.Wait(TimeSpan.FromSeconds(10))));Assert.Equal(probe.Workers,probe.Active);}
            finally{probe.Release.Set();await query.WaitAsync(TimeSpan.FromSeconds(10));}
            Assert.Equal(128,result.Count);Assert.Equal(expected,result.Value);Assert.Equal(0,probe.Active);
        }
        await using(var write=await fixture.Owner.BeginAsync()){
            var rows=fixture.Left.For(write);rows[1]=(1,200);rows.Remove(2);rows.Add((1000,7));
            var result=new ParallelSum(null);rows.Rows.IterateParallel(ref result);
            Assert.Equal(128,result.Count);Assert.Equal(12807,result.Value);await write.CommitAsync();
        }
        Assert.Equal(0,CpuScheduler.Shared.Usage.Active);Assert.Equal(0,CpuScheduler.Shared.Usage.Outstanding);
    }
    [Fact]
    public async Task ParallelFailureWaitsForAllWorkersAndReturnsResourcesBeforePropagatingTheOriginalError(){
        await using var fixture=new Fixture();await fixture.Seed(128);await using var read=await fixture.Owner.BeginReadOnlyAsync();var rows=fixture.Left.For(read);
        var warmup=new ParallelSum(null);rows.Rows.IterateParallel(ref warmup);var baseline=fixture.MemoryBytes;
        using var probe=new WorkerProbe{Fail=true};var result=new ParallelSum(probe);
        var query=Task.Run(()=>rows.Rows.IterateParallel(ref result));
        bool started;
        try{started=await Task.Run(()=>probe.Started.Wait(TimeSpan.FromSeconds(10)));}
        finally{probe.Release.Set();}
        var error=await Assert.ThrowsAsync<InvalidOperationException>(async()=>await query.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(started);
        Assert.Equal("worker failure",error.Message);Assert.Equal(0,probe.Active);Assert.Equal(baseline,fixture.MemoryBytes);
        Assert.Equal(0,CpuScheduler.Shared.Usage.Active);Assert.Equal(0,CpuScheduler.Shared.Usage.Outstanding);Assert.Null(fixture.Owner.Failure.Error);
        var retry=new ParallelSum(null);rows.Rows.IterateParallel(ref retry);Assert.Equal(128,retry.Count);
    }
    [Fact]
    public async Task ReclamationWaitsForEveryParallelCallbackBeforeReleasingTheSnapshot(){
        await using var fixture=new Fixture();await fixture.Seed(128);await using var read=await fixture.Owner.BeginReadOnlyAsync();var rows=fixture.Left.For(read);
        using var probe=new WorkerProbe();var result=new ParallelSum(probe);
        var query=Task.Run(()=>rows.Rows.IterateParallel(ref result));Task? reclaim=null;var blocked=false;
        try{
            Assert.True(await Task.Run(()=>probe.Started.Wait(TimeSpan.FromSeconds(10))));
            reclaim=OutsideCurrent(async()=>await fixture.Owner.ReclaimHistoryAsync());
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(!fixture.Owner.GetDomainDiagnostics().Reclaiming&&timer.Elapsed<TimeSpan.FromSeconds(2)){await Task.Delay(1);}
            blocked=fixture.Owner.GetDomainDiagnostics().Reclaiming&&!reclaim.IsCompleted;
        }finally{
            probe.Release.Set();
            try{await query.WaitAsync(TimeSpan.FromSeconds(10));}catch(TransactionAbortedException error){Assert.Equal(TransactionAbortReason.HistoryPressure,error.Reason);}
            if(reclaim is not null){await reclaim.WaitAsync(TimeSpan.FromSeconds(10));}
        }
        Assert.True(blocked);Assert.Equal(0,probe.Active);Assert.Equal(0,fixture.Owner.GetDomainDiagnostics().ActiveTransactions);
        Assert.Equal(TransactionAbortReason.HistoryPressure,Assert.Throws<TransactionAbortedException>(()=>rows.ContainsKey(1)).Reason);
        Assert.Equal(0,CpuScheduler.Shared.Usage.Outstanding);Assert.Null(fixture.Owner.Failure.Error);
        await OutsideCurrent(async()=>{await using var write=await fixture.Owner.BeginAsync();fixture.Left.For(write)[1]=(1,150);await write.CommitAsync();});
    }
    // 以下のprobeだけが同期/障害を注入する。製品のIParallelStrategyには副作用を許可しない。
    private sealed class WorkerProbe:IDisposable{
        internal readonly int Workers=Math.Min(2,Math.Max(1,Environment.ProcessorCount));
        internal readonly CountdownEvent Started=new(Math.Min(2,Math.Max(1,Environment.ProcessorCount)));
        internal readonly ManualResetEventSlim Release=new();
        internal int Active;
        internal bool Fail;
        internal void Enter(){
            Interlocked.Increment(ref this.Active);
            try{this.Started.Signal();if(!this.Release.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}if(this.Fail){throw new InvalidOperationException("worker failure");}}
            finally{Interlocked.Decrement(ref this.Active);}
        }
        public void Dispose(){this.Started.Dispose();this.Release.Dispose();}
    }
    private struct ParallelSum(WorkerProbe? probe):IParallelStrategy<ParallelSum,(int Id,long Balance)>{
        private readonly WorkerProbe? probe=probe;
        internal long Value;internal int Count;
        public static ParallelSum CreatePartial(in ParallelSum seed)=>new(seed.probe);
        public static void Merge(ref ParallelSum result,in ParallelSum part){result.Value=checked(result.Value+part.Value);result.Count=checked(result.Count+part.Count);}
        public static bool Invoke(ref ParallelSum self,(int Id,long Balance) row){if(self.Count==0){self.probe?.Enter();}self.Value=checked(self.Value+row.Balance);self.Count++;return true;}
    }
    private struct LookupSum(TransactionCollection<int,(int Id,long Balance)> other):IStrategy<LookupSum,(int Id,long Balance)>{
        private readonly TransactionCollection<int,(int Id,long Balance)> other=other;
        internal long Value;internal int Found;
        public static bool Invoke(ref LookupSum self,(int Id,long Balance) row){if(self.other.TryGetValue(row.Id,out var right)){self.Value=checked(self.Value+right.Balance);self.Found++;}return true;}
    }
    private sealed class Fixture:IAsyncDisposable{
        private readonly ExecutionScope resources;
        private readonly Store store;
        internal TransactionCoordinator Owner{get;}
        internal VersionedTransactionalTable<int,(int Id,long Balance)> Left{get;}
        internal VersionedTransactionalTable<int,(int Id,long Balance)> Right{get;}
        internal long MemoryBytes=>this.resources.Budget.MemoryBytes;
        internal long TemporaryBytes=>this.resources.Budget.TemporaryStorageBytes;
        internal Fixture(long historyBytes=512<<10,VersionedRecordEncoding encoding=VersionedRecordEncoding.Canonical,int queryWorkers=2){
            // nativeの固定poolは表ごとに約8MiB。行数・履歴圧迫条件を増やさず実費を予算へ含める。
            this.resources=new(new ExecutionOptions{MemoryLimitBytes=48<<20,TemporaryStorageLimitBytes=8<<20,MaximumQueryWorkers=queryWorkers,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
            this.store=Store.CreateMemory(new StoreLimits{MemoryBytes=32<<20,DiskBytes=0},parentBudget:this.resources.Budget);
            this.Owner=new(null,null,null,new TransactionCoordinatorOptions{Concurrency=TransactionConcurrencyMode.Optimistic,ParentBudget=this.resources.Budget,
                History=new(){MaxRetainedMemoryBytes=historyBytes,MaxRetainedStorageBytes=0,MaxMetadataBytes=256<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
            var tuning=new TsavoriteTuning{IndexSize=64,PageSize=4096,SegmentSize=1<<16,LogMemorySize=1<<16};
            this.Left=new(0,1,"left",this.store.CreateTsavoriteHistory("left",tuning,VersionedTransactionalTable<int,(int,long)>.StorageIdentity("left",encoding)),this.resources,1024,static row=>row.Id,encoding);
            this.Right=new(0,2,"right",this.store.CreateTsavoriteHistory("right",tuning,VersionedTransactionalTable<int,(int,long)>.StorageIdentity("right",encoding)),this.resources,1024,static row=>row.Id,encoding);
            this.Owner.Register(this.Left);this.Owner.Register(this.Right);
        }
        internal async Task Seed(int count=64){await using var tx=await this.Owner.BeginAsync();var left=this.Left.For(tx);var right=this.Right.For(tx);for(var key=1;key<=count;key++){left.Add((key,100));right.Add((key,100));}await tx.CommitAsync();}
        public async ValueTask DisposeAsync(){await this.Owner.DisposeAsync();this.Left.Dispose();this.Right.Dispose();this.store.Dispose();this.resources.Dispose();}
    }
}
