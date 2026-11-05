using Seamless.Execution;
using Seamless.Collections.Iteration;
namespace Seamless.Transactions.Tests;

public sealed partial class VersionedTableTests{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PrivateSpilledLookupWaitsForEveryWorkerOnHistoryCancellationAndFailure(bool historyCancellation){
        await using var fixture=new Fixture(queryWorkers:4);await fixture.Seed();
        await using var read=await fixture.Owner.BeginReadOnlyAsync();var rows=fixture.Left.For(read).Rows;
        // 取引自身の登録費を基準に含め、query終了時の私有作業領域だけを照合する。
        Assert.Equal(64,rows.LongCount());var baseline=fixture.MemoryBytes;var storage=fixture.TemporaryBytes;
        using var probe=new PrivateWorkerBarrier();
        var query=Task.Run(()=>rows.ExecuteQuery(()=>{
            using var root=rows.CreateQueryScope();using var scope=root.CreateChild(memoryLimitBytes:256<<10);
            using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,probe,allowParallel:true);
            Assert.True(lookup.IsParallel);probe.Configure(lookup.WorkerCount);
            lookup.Build(builder=>{
                probe.ProducerThread=Environment.CurrentManagedThreadId;
                var producer=new PrivateLookupProducer(builder);
                try{rows.Iterate(ref producer);}finally{probe.ProducerExited.Set();}
            });
            return lookup.LongCount;
        }));
        Task? reclaim=null;
        try{
            Assert.True(await Task.Run(()=>probe.Ready.Wait(TimeSpan.FromSeconds(10))));
            Assert.True(fixture.TemporaryBytes>storage,"The worker lookups must really spill before the barrier.");
            if(historyCancellation){
                reclaim=OutsideCurrent(async()=>await fixture.Owner.ReclaimHistoryAsync());
                var timer=System.Diagnostics.Stopwatch.StartNew();
                while(!fixture.Owner.GetDomainDiagnostics().Reclaiming&&timer.Elapsed<TimeSpan.FromSeconds(2)){await Task.Delay(1);}
                Assert.True(fixture.Owner.GetDomainDiagnostics().Reclaiming);Assert.False(reclaim.IsCompleted);
            }else{probe.FailFirst=true;}
            probe.Release(0);
            Assert.True(await Task.Run(()=>probe.Exited[0].Wait(TimeSpan.FromSeconds(10))));
            Assert.True(await Task.Run(()=>probe.ProducerExited.Wait(TimeSpan.FromSeconds(10))));
            if(probe.Workers>1){
                Assert.False(query.IsCompleted);Assert.True(fixture.TemporaryBytes>storage);
                if(reclaim is not null){Assert.False(reclaim.IsCompleted);Assert.True(fixture.Owner.GetDomainDiagnostics().ActiveOperations>0);}
            }
        }finally{
            probe.ReleaseAll();
            if(historyCancellation){
                var error=await Assert.ThrowsAsync<global::Seamless.Transactions.TransactionAbortedException>(async()=>await query.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal(global::Seamless.Transactions.TransactionAbortReason.HistoryPressure,error.Reason);
            }else{
                var error=await Assert.ThrowsAsync<InvalidOperationException>(async()=>await query.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal("private worker failure",error.Message);
            }
            if(reclaim is not null){await reclaim.WaitAsync(TimeSpan.FromSeconds(10));}
        }
        Assert.Equal(storage,fixture.TemporaryBytes);
        Assert.Equal(0,CpuScheduler.Shared.Usage.Active);Assert.Equal(0,CpuScheduler.Shared.Usage.Outstanding);
        Assert.Null(fixture.Owner.Failure.Error);
        if(historyCancellation){Assert.Equal(0,fixture.Owner.GetDomainDiagnostics().ActiveOperations);}
        else{Assert.Equal(baseline,fixture.MemoryBytes);Assert.Equal(64,rows.LongCount());}
        await OutsideCurrent(async()=>{await using var tx=await fixture.Owner.BeginAsync();fixture.Left.For(tx)[1]=(1,150);await tx.CommitAsync();});
    }
    private struct PrivateLookupProducer(global::Seamless.Linq.PartitionedLookupBuilder<int,int> lookup):IStrategy<PrivateLookupProducer,(int Id,long Balance)>{
        private readonly global::Seamless.Linq.PartitionedLookupBuilder<int,int> lookup=lookup;
        private int next;
        public static bool Invoke(ref PrivateLookupProducer self,(int Id,long Balance) row){
            for(var index=0;index<16;index++){self.lookup.Add(self.next++,checked((int)row.Balance));}
            return true;
        }
    }
    // 比較器の副作用は障壁/障害の注入専用。製品の並列化可否の判断には用いない。
    private sealed class PrivateWorkerBarrier:IEqualityComparer<int>,IDisposable{
        internal readonly ManualResetEventSlim Ready=new();
        internal readonly ManualResetEventSlim ProducerExited=new();
        internal ManualResetEventSlim[] Exited=[];
        private ManualResetEventSlim[] releases=[];
        private int[] entered=[];
        private int arrivals;
        internal int Workers;
        internal int ProducerThread;
        internal volatile bool FailFirst;
        internal void Configure(int workers){
            this.Workers=workers;this.entered=new int[workers];
            this.releases=Enumerable.Range(0,workers).Select(static _=>new ManualResetEventSlim()).ToArray();
            this.Exited=Enumerable.Range(0,workers).Select(static _=>new ManualResetEventSlim()).ToArray();
        }
        public bool Equals(int x,int y)=>x==y;
        public int GetHashCode(int key){
            if(Environment.CurrentManagedThreadId!=Volatile.Read(ref this.ProducerThread)&&key>=384){
                var worker=(int)((uint)key%(uint)this.Workers);
                if(Interlocked.CompareExchange(ref this.entered[worker],1,0)==0){
                    if(Interlocked.Increment(ref this.arrivals)==this.Workers){this.Ready.Set();}
                    try{
                        if(!this.releases[worker].Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException("Private worker barrier was not released.");}
                        if(worker==0&&this.FailFirst){throw new InvalidOperationException("private worker failure");}
                    }finally{this.Exited[worker].Set();}
                }
            }
            return key;
        }
        internal void Release(int worker)=>this.releases[worker].Set();
        internal void ReleaseAll(){foreach(var release in this.releases){release.Set();}}
        public void Dispose(){this.ReleaseAll();this.Ready.Dispose();this.ProducerExited.Dispose();foreach(var release in this.releases){release.Dispose();}foreach(var exited in this.Exited){exited.Dispose();}}
    }
}
