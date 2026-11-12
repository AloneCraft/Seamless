using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions.Collections;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed class ParticipantGroupTests{
    [Fact]
    public async Task OneSnapshotSpansDatabasesAndDifferentPhysicalGenerations(){
        await using var fixture=new Fixture();await fixture.Seed();await using var read=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(100,fixture.First.For(read)[1].Balance);
        Task writer;using(ExecutionContext.SuppressFlow()){writer=Task.Run(async()=>{await using var tx=await fixture.Owner.BeginAsync();fixture.First.For(tx)[1]=(1,200);fixture.Next.For(tx)[1]=(1,300);fixture.Other.For(tx)[1]=(1,400);await tx.CommitAsync();});}await writer;
        Assert.Equal(100,fixture.First.For(read)[1].Balance);Assert.Equal(100,fixture.Next.For(read)[1].Balance);Assert.Equal(100,fixture.Other.For(read)[1].Balance);await read.CommitAsync();
    }
    [Fact]
    public async Task LateGenerationSelectionUsesTheOriginalCatalogAndNativePlanSnapshot(){
        await using var fixture=new Fixture();await fixture.Seed();
        // 安定したカタログの行0が物理世代、行-1がNative計画の識別子を表す。
        await using(var seed=await fixture.Owner.BeginAsync()){fixture.Next.For(seed).Add((0,0));fixture.Next.For(seed).Add((-1,10));await seed.CommitAsync();}
        using var candidate=fixture.Create(fixture.One,7,"dynamic-generation",[3]);
        await using var read=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(100,fixture.First.For(read)[1].Balance);
        // 旧rootが存在する間にも、新しい型付き世代を論理DBへ追加できる。登録は公開ではない。
        fixture.One.Register(candidate);
        Task writer;using(ExecutionContext.SuppressFlow()){writer=Task.Run(async()=>{
            await using var tx=await fixture.Owner.BeginAsync();candidate.For(tx).Add((1,200));fixture.Other.For(tx)[1]=(1,300);fixture.Next.For(tx)[0]=(0,1);fixture.Next.For(tx)[-1]=(-1,20);await tx.CommitAsync();
        });}await writer;
        // 最初にDB Aを読み、更新完了後に初めてDB B/カタログ/Nativeを見る順序でも同じ開始版。
        Assert.Equal(100,fixture.Other.For(read)[1].Balance);Assert.Equal(0,fixture.Next.For(read)[0].Balance);Assert.Equal(10,fixture.Next.For(read)[-1].Balance);await read.CommitAsync();
        await using(var current=await fixture.Owner.BeginReadOnlyAsync()){Assert.Equal(1,fixture.Next.For(current)[0].Balance);Assert.Equal(200,candidate.For(current)[1].Balance);Assert.Equal(300,fixture.Other.For(current)[1].Balance);Assert.Equal(20,fixture.Next.For(current)[-1].Balance);await current.CommitAsync();}
        await fixture.Owner.RetireVersionedParticipantsAsync([candidate]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamespaceSeparatesEqualKeysButSameChildUpdatesConflict(bool sameChild){
        await using var fixture=new Fixture();await fixture.Seed();using var ready=new CountdownEvent(2);using var go=new ManualResetEventSlim();var aborted=0;
        var tasks=Enumerable.Range(0,2).Select(index=>Task.Run(async()=>{
            await using var tx=await fixture.Owner.BeginAsync();var table=fixture.First;if(index==1&&!sameChild){table=fixture.Next;}
            var row=table.For(tx)[1];table.For(tx)[1]=row with{Balance=row.Balance+1};ready.Signal();if(!go.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}
            try{await tx.CommitAsync();}catch(TransactionAbortedException error){Assert.Equal(TransactionAbortReason.Conflict,error.Reason);Interlocked.Increment(ref aborted);}
        })).ToArray();
        try{Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));}finally{go.Set();}await Task.WhenAll(tasks);
        if(sameChild){Assert.Equal(1,aborted);}else{Assert.Equal(0,aborted);}
        await using var verify=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(101,fixture.First.For(verify)[1].Balance);await verify.CommitAsync();
    }
    [Fact]
    public async Task ScanDependencyCoversTheLogicalDatabaseAndReclamationReopensEveryChild(){
        await using var fixture=new Fixture();await fixture.Seed();await using(var scan=await fixture.Owner.BeginAsync()){
            Assert.Single(fixture.First.For(scan).Rows);
            Task writer;using(ExecutionContext.SuppressFlow()){writer=Task.Run(async()=>{await using var tx=await fixture.Owner.BeginAsync();fixture.Next.For(tx)[1]=(1,250);await tx.CommitAsync();});}await writer;
            var error=await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await scan.CommitAsync());Assert.Equal(TransactionAbortReason.Conflict,error.Reason);
        }
        await fixture.Owner.ReclaimHistoryAsync();await using var verify=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(100,fixture.First.For(verify)[1].Balance);Assert.Equal(250,fixture.Next.For(verify)[1].Balance);Assert.Equal(100,fixture.Other.For(verify)[1].Balance);await verify.CommitAsync();
    }
    [Fact]
    public async Task WalReplayResolvesTheGenerationCodecWhenTableIdsAreEqual(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"groups-"+Guid.NewGuid().ToString("N"));
        await using(var fixture=new Fixture(path)){await fixture.Seed();await using var tx=await fixture.Owner.BeginAsync();fixture.First.For(tx)[1]=(1,111);fixture.Next.For(tx)[1]=(1,222);fixture.Other.For(tx)[1]=(1,333);await tx.CommitAsync();}
        await using(var fixture=new Fixture(path)){await using var tx=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(111,fixture.First.For(tx)[1].Balance);Assert.Equal(222,fixture.Next.For(tx)[1].Balance);Assert.Equal(333,fixture.Other.For(tx)[1].Balance);await tx.CommitAsync();}
    }
    [Fact]
    public async Task UnregisteredChildrenAndDuplicateDependencyNamespacesAreRejected(){
        await using var fixture=new Fixture();using var rogue=fixture.Create(fixture.One,8,"rogue",[9]);
        await using var tx=await fixture.Owner.BeginAsync();Assert.Throws<InvalidOperationException>(()=>rogue.For(tx).TryGetValue(1,out _));await tx.RollbackAsync();
        using var duplicate=fixture.Create(fixture.One,9,"duplicate",[1]);Assert.Throws<InvalidOperationException>(()=>fixture.One.Register(duplicate));
        Assert.Throws<InvalidOperationException>(()=>fixture.Owner.Register(rogue));
    }
    [Fact]
    public async Task BatchRegistrationLeavesNoPartialGenerationWhenValidationOrPreparationFails(){
        await using var fixture=new Fixture();await fixture.Seed();using var first=fixture.Create(fixture.One,8,"batch-first",[8]);using var duplicate=fixture.Create(fixture.One,9,"batch-duplicate",[1]);
        Assert.Throws<InvalidOperationException>(()=>fixture.One.RegisterBatch([first,duplicate]));Assert.False(fixture.One.Owns(first));
        using var loading=fixture.Create(fixture.One,9,"batch-loading",[9]);using(var snapshot=loading.BeginUnpublishedSnapshot()){
            Assert.True(snapshot.Add((1,200)));Assert.Throws<InvalidOperationException>(()=>fixture.One.RegisterBatch([first,loading]));Assert.False(fixture.One.Owns(first));Assert.False(fixture.One.Owns(loading));snapshot.Complete();
        }
        fixture.One.RegisterBatch([first,loading]);await using(var tx=await fixture.Owner.BeginAsync()){first.For(tx).Add((1,100));Assert.Equal(200,loading.For(tx)[1].Balance);await tx.CommitAsync();}
        await fixture.Owner.RetireVersionedParticipantsAsync([first,loading]);
    }
    [Fact]
    public async Task MembershipCannotChangeBetweenReclamationAndCommonCheckpointReopen(){
        await using var fixture=new Fixture();await fixture.Seed();using var candidate=fixture.Create(fixture.One,8,"candidate",[8]);
        await fixture.One.ReclaimAsync();try{Assert.Throws<InvalidOperationException>(()=>fixture.One.Register(candidate));}finally{fixture.One.ReopenAfterMaintenance();}
        await using var read=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(100,fixture.First.For(read)[1].Balance);Assert.Equal(100,fixture.Next.For(read)[1].Balance);await read.CommitAsync();
    }
    [Fact]
    public async Task ANewGenerationMustFinishInitialLoadingBeforeJoiningAStartedGroup(){
        await using var fixture=new Fixture();await fixture.Seed();using var candidate=fixture.Create(fixture.One,7,"loaded-generation",[4]);
        using(var snapshot=candidate.BeginUnpublishedSnapshot()){Assert.True(snapshot.Add((1,200)));Assert.Throws<InvalidOperationException>(()=>fixture.One.Register(candidate));Assert.False(fixture.One.Owns(candidate));snapshot.Complete();}
        fixture.One.Register(candidate);await using(var read=await fixture.Owner.BeginReadOnlyAsync()){Assert.Equal(200,candidate.For(read)[1].Balance);await read.CommitAsync();}
        await fixture.Owner.RetireVersionedParticipantsAsync([candidate]);
    }
    [Fact]
    public async Task RetirementWaitsForWritersAndCheckpointAndBlocksNewRoots(){
        var checkpoint=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture=new Fixture(checkpoint:async()=>{checkpoint.SetResult();await finish.Task;});await fixture.Seed();
        using var cancellation=new CancellationTokenSource();
        var writer=await fixture.Owner.BeginAsync();fixture.First.For(writer)[1]=(1,150);
        Task retire;using(ExecutionContext.SuppressFlow()){retire=Task.Run(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First],cancellation.Token));}
        Assert.True(SpinWait.SpinUntil(()=>fixture.Owner.GetDomainDiagnostics().Reclaiming,TimeSpan.FromSeconds(3)));Assert.False(retire.IsCompleted);Assert.True(fixture.One.Owns(fixture.First));
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task newcomer;using(ExecutionContext.SuppressFlow()){newcomer=Task.Run(async()=>{await using var tx=await fixture.Owner.BeginReadOnlyAsync();entered.SetResult();Assert.Throws<InvalidOperationException>(()=>fixture.First.For(tx).TryGetValue(1,out _));Assert.Equal(100,fixture.Next.For(tx)[1].Balance);await tx.CommitAsync();});}
        try{
            await writer.CommitAsync();await writer.DisposeAsync();await checkpoint.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(entered.Task.IsCompleted);Assert.True(fixture.One.Owns(fixture.First));
            cancellation.Cancel();Assert.False(retire.IsCompleted);
            var duplicate=fixture.Owner.RetireVersionedParticipantsAsync([fixture.First]).AsTask();
            // checkpointの途中で受け付けた同じ世代の要求も、一度の回収結果を共有する。
            finish.TrySetResult();await duplicate.WaitAsync(TimeSpan.FromSeconds(3));
        }finally{finish.TrySetResult();await writer.DisposeAsync();}
        await retire.WaitAsync(TimeSpan.FromSeconds(3));await newcomer.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(fixture.One.Owns(fixture.First));
    }
    [Fact]
    public async Task OrdinaryRetirementWaitsForReadersWithoutDeclaringHistoryPressure(){
        await using var fixture=new Fixture(checkpoint:()=>ValueTask.CompletedTask);await fixture.Seed();var read=await fixture.Owner.BeginReadOnlyAsync();
        Task retirement;using(ExecutionContext.SuppressFlow()){retirement=Task.Run(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First]));}
        try{
            Assert.True(SpinWait.SpinUntil(()=>fixture.Owner.GetDomainDiagnostics().Reclaiming,TimeSpan.FromSeconds(3)));
            Assert.False(retirement.IsCompleted);Assert.Equal(100,fixture.First.For(read)[1].Balance);await read.CommitAsync();
        }finally{await read.DisposeAsync();}
        await retirement.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(fixture.One.Owns(fixture.First));
    }
    [Fact]
    public async Task FailedCheckpointKeepsTheGenerationRegistered(){
        await using var fixture=new Fixture(checkpoint:()=>throw new IOException("checkpoint failed"));await fixture.Seed();
        await Assert.ThrowsAsync<IOException>(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First]));Assert.True(fixture.One.Owns(fixture.First));Assert.NotNull(fixture.Owner.Failure.Error);
    }
    [Fact]
    public async Task DisposedFailedGroupReleasesChildReferences(){
        await using var fixture=new Fixture();var (group,child)=CreateFailedGroup(fixture);
        for(var attempt=0;attempt<10&&child.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(10);}
        Assert.False(child.IsAlive);GC.KeepAlive(group);
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (VersionedTransactionParticipantGroup Group,WeakReference Child) CreateFailedGroup(Fixture fixture){
        var budget=new ResourceBudget(4096,0);var group=new VersionedTransactionParticipantGroup(3,0,"failed-database",budget);var child=fixture.Create(group,7,"failed-child",[1]);
        group.Register(child);group.ReclaimAsync().GetAwaiter().GetResult();group.FaultMaintenance(new IOException("common checkpoint failed"));child.Dispose();group.Dispose();Assert.Equal(0,budget.MemoryBytes);return(group,new(child));
    }
    [Fact]
    public async Task RetirementValidatesBeforeAdmissionAndRefusesAmbientWaits(){
        await using var fixture=new Fixture();await fixture.Seed();using var rogue=fixture.Create(fixture.One,8,"rogue-retire",[9]);
        await Assert.ThrowsAsync<ArgumentException>(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First,rogue]));Assert.True(fixture.One.Owns(fixture.First));Assert.False(fixture.Owner.GetDomainDiagnostics().Reclaiming);
        using var canceled=new CancellationTokenSource();canceled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First],canceled.Token));Assert.True(fixture.One.Owns(fixture.First));
        var before=fixture.Budget.MemoryBytes;
        // 残予算を予約するだけで配列は確保しない。回収要求の予約が失敗しても所属や受付を変更しない。
        using(var pressure=fixture.Budget.ReserveMemory(fixture.Budget.AvailableMemoryBytes)){
            await Assert.ThrowsAsync<ResourceLimitException>(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First]));
        }
        Assert.Equal(before,fixture.Budget.MemoryBytes);Assert.True(fixture.One.Owns(fixture.First));Assert.False(fixture.Owner.GetDomainDiagnostics().Reclaiming);Assert.Null(fixture.Owner.Failure.Error);
        await using var tx=await fixture.Owner.BeginAsync();Assert.Equal(100,fixture.First.For(tx)[1].Balance);Assert.Equal(100,fixture.Other.For(tx)[1].Balance);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First]));
    }
    [Fact]
    public async Task PersistentRetirementRequiresTheCommonCheckpointOwner(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"retirement-"+Guid.NewGuid().ToString("N"));await using var fixture=new Fixture(path);await fixture.Seed();
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.Owner.RetireVersionedParticipantsAsync([fixture.First]));Assert.True(fixture.One.Owns(fixture.First));Assert.False(fixture.Owner.GetDomainDiagnostics().Reclaiming);
    }
    private sealed class Fixture:IAsyncDisposable{
        private readonly ExecutionScope resources;
        private readonly Store store;
        private readonly PreparedTransactionJournal? journal;
        internal ResourceBudget Budget=>this.resources.Budget;
        internal readonly TransactionCoordinator Owner;
        internal readonly VersionedTransactionParticipantGroup One,Two;
        internal readonly VersionedTransactionalTable<int,(int Id,long Balance)> First,Next,Other;
        internal Fixture(string? journalPath=null,Func<ValueTask>? checkpoint=null){
            // 世代候補を含む最大6表の既存native poolを計上する。行数とmetadata/履歴の上限は維持する。
            this.resources=new(new(){MemoryLimitBytes=64<<20,TemporaryStorageLimitBytes=16<<20,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});this.store=Store.CreateMemory(parentBudget:this.resources.Budget);
            if(journalPath is not null){this.journal=new(journalPath,budget:this.resources.Budget);}
            this.Owner=new(null,null,this.journal,new(){Concurrency=TransactionConcurrencyMode.Optimistic,ParentBudget=this.resources.Budget,History=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=0,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
            this.One=new(1,0,"database-a",this.resources.Budget);this.Two=new(2,0,"database-b",this.resources.Budget);
            this.First=this.Create(this.One,7,"first",[1]);this.Next=this.Create(this.One,7,"next",[2]);this.Other=this.Create(this.Two,7,"other",[1]);
            this.One.Register(this.First);this.One.Register(this.Next);this.Two.Register(this.Other);this.Owner.Register(this.One);this.Owner.Register(this.Two);
            if(checkpoint is not null){this.Owner.ConfigureMaintenance(checkpoint);}
        }
        internal VersionedTransactionalTable<int,(int Id,long Balance)> Create(VersionedTransactionParticipantGroup group,int tableId,string codec,byte[] prefix){
            var data=this.store.CreateTsavoriteHistory(codec,new(){IndexSize=64,PageSize=4096,SegmentSize=65536,LogMemorySize=65536},VersionedTransactionalTable<int,(int,long)>.StorageIdentity(codec,VersionedRecordEncoding.Canonical));
            return new(group.Partition,tableId,codec,data,this.resources,1024,static row=>row.Id,VersionedRecordEncoding.Canonical,new(group,prefix));
        }
        internal async Task Seed(){await using var tx=await this.Owner.BeginAsync();this.First.For(tx).Add((1,100));this.Next.For(tx).Add((1,100));this.Other.For(tx).Add((1,100));await tx.CommitAsync();}
        public async ValueTask DisposeAsync(){await this.Owner.DisposeAsync();this.First.Dispose();this.Next.Dispose();this.Other.Dispose();this.One.Dispose();this.Two.Dispose();this.store.Dispose();if(this.journal is not null){await this.journal.DisposeAsync();}this.resources.Dispose();}
    }
}
