using Benchmark.TpcE.Core;
using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Collections.Tsavorite;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Tests;

public sealed class VersionedTransactions{
    private static VersionedTpcEDatabase Open(Store store)=>new(store,new(){Customers=8,InitialTradeDays=0,MarketDays=1,ExpectedRunTrades=64},new(){Concurrency=TransactionConcurrencyMode.Optimistic,
        History=new(){MaxRetainedMemoryBytes=4<<20,MaxRetainedStorageBytes=4<<20,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=256<<10,MaintenanceReserveStorageBytes=256<<10,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}},
        new(){MemoryLimitBytes=64<<20,TemporaryStorageLimitBytes=16<<20,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,MaximumQueryWorkers=2},
        new(){IndexSize=64,PageSize=4096,SegmentSize=65536,LogMemorySize=65536});
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MainRowsAndAllIndexChunkElementsCommitRollbackAndRecoverTogether(bool persistent){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"tpc-"+Guid.NewGuid().ToString("N")[..6]);
        using(var store=persistent?TpcETestStores.Open(directory):TpcETestStores.Memory()){
            using var db=Open(store);db.BeginBulkLoad();for(var id=1;id<=40;id++){db.CustomerAccount.Load(new(id,1,10,"account",0,100));}db.EndBulkLoad();
            db.Log.BeginReadOnly();Assert.Equal(Enumerable.Range(1,40).Select(static id=>(long)id),db.CustomerAccounts.All(10));Assert.Equal(40,db.CustomerAccount.Count);db.Log.Commit();
            db.Log.Begin();var row=db.CustomerAccount.Get(new(1));db.CustomerAccount.Update(row with{CustomerId=20,Balance=125});Assert.Equal(new long[]{1},db.CustomerAccounts.All(20));db.Log.Rollback();
            db.Log.BeginReadOnly();Assert.Empty(db.CustomerAccounts.All(20));Assert.Equal(100,db.CustomerAccount.Get(new(1)).Balance);Assert.Equal(40,db.CustomerAccounts.CountOf(10));db.Log.Commit();
            db.Log.Begin();db.CustomerAccount.Update(row with{CustomerId=20,Balance=150});db.Log.Commit();
            await db.Domain.CheckpointAsync();
            db.Log.BeginReadOnly();Assert.Equal(150,db.CustomerAccount.Get(new(1)).Balance);Assert.Equal(new long[]{1},db.CustomerAccounts.All(20));Assert.Equal(39,db.CustomerAccounts.CountOf(10));db.Log.Commit();
            var observer=new Observer();db.Log.Observer=observer;db.Log.Begin();db.CustomerAccount.Update(row with{Balance=999});db.Log.Rollback();Assert.Equal(new[]{WriteKind.Update,WriteKind.UndoUpdate},observer.Kinds);db.Log.Observer=null;
            // 共通checkpointの後のcommitは、次回のWAL replayで主表/索引へ復元される。
            if(persistent){db.Log.Begin();db.CustomerAccount.Update(row with{CustomerId=30,Balance=175});db.Log.Commit();}
            Assert.Equal(0,db.Domain.Coordinator.GetDomainDiagnostics().ActiveTransactions);
        }
        if(persistent){
            using var store=TpcETestStores.Open(directory);using var db=Open(store);db.Log.BeginReadOnly();
            Assert.Equal(175,db.CustomerAccount.Get(new(1)).Balance);Assert.Empty(db.CustomerAccounts.All(20));Assert.Equal(new long[]{1},db.CustomerAccounts.All(30));Assert.Equal(Enumerable.Range(2,39).Select(static id=>(long)id),db.CustomerAccounts.All(10));db.Log.Commit();
        }
    }
    [Fact]
    public async Task DifferentRowsAndIndexesProceedTogetherAndConflictingUpdatesAbort(){
        using var store=TpcETestStores.Memory();using var db=Open(store);
        db.BeginBulkLoad();db.CustomerAccount.Load(new(1,1,10,"one",0,100));db.CustomerAccount.Load(new(2,1,11,"two",0,100));db.EndBulkLoad();
        using var ready=new CountdownEvent(2);using var release=new ManualResetEventSlim();
        var writers=new[]{1L,2L}.Select(id=>Task.Run(()=>{
            db.Log.Begin();try{var row=db.CustomerAccount.Get(new(id));db.CustomerAccount.Update(row with{CustomerId=20+id,Balance=200+id});ready.Signal();if(!release.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}db.Log.Commit();}
            finally{if(db.Log.IsRecording){db.Log.Rollback();}}
        })).ToArray();
        try{Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));Assert.Equal(2,db.Domain.Coordinator.GetDomainDiagnostics().ActiveTransactions);}finally{release.Set();}
        await Task.WhenAll(writers);
        using var both=new CountdownEvent(2);using var commit=new ManualResetEventSlim();var aborted=0;
        var conflicts=Enumerable.Range(0,2).Select(_=>Task.Run(()=>{
            db.Log.Begin();try{var row=db.CustomerAccount.Get(new(1));db.CustomerAccount.Update(row with{Balance=row.Balance+1});both.Signal();if(!commit.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}db.Log.Commit();}
            catch(TransactionAbortedException error){Assert.Equal(TransactionAbortReason.Conflict,error.Reason);Interlocked.Increment(ref aborted);}
            finally{if(db.Log.IsRecording){db.Log.Rollback();}}
        })).ToArray();
        try{Assert.True(both.Wait(TimeSpan.FromSeconds(10)));}finally{commit.Set();}await Task.WhenAll(conflicts);Assert.Equal(1,aborted);
        db.Log.BeginReadOnly();Assert.Equal(202,db.CustomerAccount.Get(new(1)).Balance);Assert.Equal(202,db.CustomerAccount.Get(new(2)).Balance);Assert.Equal(new long[]{1},db.CustomerAccounts.All(21));Assert.Equal(new long[]{2},db.CustomerAccounts.All(22));db.Log.Commit();
    }
    private sealed class Observer:IWriteObserver{
        internal readonly List<WriteKind> Kinds=[];
        public void OnWrite(int tableId,WriteKind kind,object key,object? row)=>this.Kinds.Add(kind);
    }
    [Fact]
    public void IndexHookFailureAbortsMainRowsAndBulkWorkWithoutLeakingIntoTheNextTransaction(){
        using var store=TpcETestStores.Memory();using var db=Open(store);
        db.CustomerAccount.AddHook(static(in CustomerAccountRow? before,in CustomerAccountRow? after)=>{if(after is{Id:99}){throw new IOException("index failure");}});
        db.BeginBulkLoad();db.CustomerAccount.Load(new(90,1,90,"before",0,100));
        Assert.Throws<IOException>(()=>db.CustomerAccount.Load(new(99,1,99,"failure",0,100)));Assert.False(db.Log.IsRecording);
        db.Log.BeginReadOnly();Assert.False(db.CustomerAccount.Contains(new(90)));Assert.False(db.CustomerAccount.Contains(new(99)));Assert.Empty(db.CustomerAccounts.All(90));Assert.Empty(db.CustomerAccounts.All(99));db.Log.Commit();
        db.BeginBulkLoad();db.CustomerAccount.Load(new(91,1,91,"next",0,100));db.EndBulkLoad();
        db.Log.BeginReadOnly();Assert.Equal(new long[]{91},db.CustomerAccounts.All(91));Assert.Empty(db.CustomerAccounts.All(90));Assert.Empty(db.CustomerAccounts.All(99));db.Log.Commit();
    }
}
