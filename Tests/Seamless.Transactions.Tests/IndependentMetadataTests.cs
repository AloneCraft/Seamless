using System.Text;
using Seamless.Execution;
namespace Seamless.Transactions.Tests;

public sealed class IndependentMetadataTests{
    [Fact]
    public async Task OversizedOwnedPublicationMetadataAbortsWithoutFaultingFollowingTransactions(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"owned-metadata-bound-"+Guid.NewGuid().ToString("N"));await using var fixture=new Fixture(path,TransactionConcurrencyMode.Optimistic);await using(var scope=await fixture.Owner.BeginOwnedAsync(Guid.NewGuid(),false)){var error=await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await scope.CommitOwnedAsync(new byte[4096],static _=>throw new InvalidOperationException("Must not install")));Assert.Equal(TransactionAbortReason.ResourceLimit,error.Reason);Assert.Equal(TransactionOutcomeKind.Aborted,(await fixture.Owner.GetOutcomeAsync(scope.Id)).Kind);}Assert.Null(fixture.Owner.Failure.Error);await using var next=await fixture.Owner.BeginOwnedAsync(Guid.NewGuid(),false);Assert.Equal(TransactionCommitKind.Committed,(await next.CommitOwnedAsync(new byte[]{1},static _=>{})).Kind);
    }
    [Fact]
    public async Task InstallationFailureAfterDurabilityKeepsTheCommittedOutcomeAndStopsTheDomain(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"independent-install-"+Guid.NewGuid().ToString("N"));await using var fixture=new Fixture(path,TransactionConcurrencyMode.Optimistic);await using var parent=await fixture.Owner.BeginAsync();var id=Guid.NewGuid();
        var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(parent,id,new byte[]{1},installed:static _=>throw new IOException("publication failed")));Assert.Equal(TransactionOutcomeKind.Committed,error.Outcome);Assert.Equal(TransactionOutcomeKind.Committed,(await fixture.Owner.GetOutcomeAsync(id)).Kind);Assert.NotNull(fixture.Owner.Failure.Error);await parent.RollbackAsync();
    }
    private static Task OutsideCurrent(Func<Task> action){using(ExecutionContext.SuppressFlow()){return Task.Run(action);}}
    [Theory]
    [InlineData(TransactionConcurrencyMode.Exclusive)]
    [InlineData(TransactionConcurrencyMode.Optimistic)]
    public async Task ReservationSurvivesParentSavepointAndRollbackAndIsDurable(TransactionConcurrencyMode mode){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"independent-"+Guid.NewGuid().ToString("N"));var id=Guid.NewGuid();var metadata=Encoding.UTF8.GetBytes("identity=42");
        await using(var fixture=new Fixture(path,mode)){
            await using(var parent=await fixture.Owner.BeginAsync()){
                parent.CreateSavepoint("before");var receipt=await fixture.Owner.CommitIndependentMetadataAsync(parent,id,metadata);Assert.Equal(TransactionCommitKind.Committed,receipt.Kind);Assert.True(receipt.Lsn>0);Assert.Equal(id,receipt.Id);
                parent.RollbackToSavepoint("before");Assert.Equal(TransactionOutcomeKind.Committed,(await fixture.Owner.GetOutcomeAsync(id)).Kind);await parent.RollbackAsync();
            }
            var found=0;fixture.Journal.Replay((transaction,lsn,bytes,entries)=>{Assert.Equal(id,transaction);Assert.Equal(metadata,bytes.ToArray());Assert.Empty(entries);found++;});Assert.Equal(1,found);Assert.Null(fixture.Owner.Failure.Error);
        }
        await using(var journal=new PreparedTransactionJournal(path,new(){MaxFrameBytes=4096,QueueBytes=16384,SegmentBytes=65536,CatalogIdentity="independent-tests"})){Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);var replayed=0;journal.Replay((transaction,lsn,bytes,entries)=>{Assert.Equal(id,transaction);Assert.Equal(metadata,bytes.ToArray());Assert.Empty(entries);replayed++;});Assert.Equal(1,replayed);}
    }
    [Theory]
    [InlineData(TransactionConcurrencyMode.Exclusive)]
    [InlineData(TransactionConcurrencyMode.Optimistic)]
    public async Task ReservationBypassesReclaimAdmissionWhileRetainingAndSerializingItsParent(TransactionConcurrencyMode mode){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"independent-reclaim-"+Guid.NewGuid().ToString("N"));using var durable=new ManualResetEventSlim();using var finish=new ManualResetEventSlim();
        await using var fixture=new Fixture(path,mode,()=>{durable.Set();if(!finish.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}});await using var parent=await fixture.Owner.BeginAsync();
        var reclaim=OutsideCurrent(async()=>await fixture.Owner.CheckpointVersionedAsync());var timer=global::System.Diagnostics.Stopwatch.StartNew();while(!fixture.Owner.GetDomainDiagnostics().Reclaiming&&timer.Elapsed<TimeSpan.FromSeconds(3)){await Task.Delay(10);}Assert.True(fixture.Owner.GetDomainDiagnostics().Reclaiming);
        using var attempted=new ManualResetEventSlim();var next=OutsideCurrent(async()=>{attempted.Set();await using var tx=await fixture.Owner.BeginReadOnlyAsync();await tx.CommitAsync();});Assert.True(attempted.Wait(TimeSpan.FromSeconds(3)));
        using var cancellation=new CancellationTokenSource();var id=Guid.NewGuid();var reservation=fixture.Owner.CommitIndependentMetadataAsync(parent,id,new byte[]{1},cancellation.Token).AsTask();
        try{
            Assert.True(await Task.Run(()=>durable.Wait(TimeSpan.FromSeconds(5))));Assert.False(reclaim.IsCompleted);Assert.False(next.IsCompleted);Assert.Equal(2,fixture.Owner.GetDomainDiagnostics().ActiveTransactions);
            Assert.Throws<InvalidOperationException>(()=>parent.BeginNested());await Assert.ThrowsAsync<InvalidOperationException>(async()=>await parent.RollbackAsync());await Assert.ThrowsAsync<InvalidOperationException>(async()=>await parent.CommitAsync());
            cancellation.Cancel();
        }finally{finish.Set();}
        Assert.Equal(TransactionCommitKind.Committed,(await reservation.WaitAsync(TimeSpan.FromSeconds(5))).Kind);await parent.RollbackAsync();await reclaim.WaitAsync(TimeSpan.FromSeconds(5));await next.WaitAsync(TimeSpan.FromSeconds(5));Assert.True(fixture.CheckpointLsn>0);Assert.Equal(TransactionOutcomeKind.Committed,(await fixture.Owner.GetOutcomeAsync(id)).Kind);Assert.Equal(0,fixture.Owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(fixture.Owner.Failure.Error);
    }
    [Fact]
    public async Task InvalidReadOnlyForeignCancelledAndCompletedRequestsLeaveTheDomainUsable(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"independent-guards-"+Guid.NewGuid().ToString("N"));await using var fixture=new Fixture(path,TransactionConcurrencyMode.Optimistic);var metadata=new byte[]{1};
        await using(var read=await fixture.Owner.BeginReadOnlyAsync()){await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(read,Guid.NewGuid(),metadata));await read.CommitAsync();}
        await using(var foreignOwner=new TransactionCoordinator()){await using var foreign=await foreignOwner.BeginAsync();await Assert.ThrowsAsync<ArgumentException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(foreign,Guid.NewGuid(),metadata));await foreign.RollbackAsync();}
        await using(var parent=await fixture.Owner.BeginAsync()){
            await Assert.ThrowsAsync<ArgumentException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(parent,parent.Id,metadata));await Assert.ThrowsAsync<ArgumentException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(parent,Guid.NewGuid(),new byte[4096]));
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(parent,Guid.NewGuid(),metadata,cancellation.Token));
            var id=Guid.NewGuid();await fixture.Owner.CommitIndependentMetadataAsync(parent,id,metadata);await Assert.ThrowsAsync<InvalidOperationException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(parent,id,metadata));await parent.CommitAsync();await Assert.ThrowsAsync<ObjectDisposedException>(async()=>await fixture.Owner.CommitIndependentMetadataAsync(parent,Guid.NewGuid(),metadata));
        }
        Assert.Null(fixture.Owner.Failure.Error);await using var good=await fixture.Owner.BeginReadOnlyAsync();await good.CommitAsync();
    }
    private sealed class Fixture:IAsyncDisposable{
        private readonly ExecutionScope resources;
        internal readonly PreparedTransactionJournal Journal;
        internal readonly TransactionCoordinator Owner;
        internal long CheckpointLsn;
        internal Fixture(string path,TransactionConcurrencyMode mode,Action? durable=null){
            this.resources=new(new(){MemoryLimitBytes=2<<20,TemporaryStorageLimitBytes=8<<20,TemporaryDirectory=Path.Combine(path,"work")});this.Journal=new(path,new(){MaxFrameBytes=4096,QueueBytes=16384,SegmentBytes=65536,CatalogIdentity="independent-tests"},this.resources.Budget,phase=>{if(phase==PreparedTransactionPhase.CommitDurable){durable?.Invoke();}});this.Owner=new(null,null,this.Journal,new(){Concurrency=mode,ParentBudget=this.resources.Budget,History=new(){MaxRetainedMemoryBytes=64<<10,MaxRetainedStorageBytes=0,MaxMetadataBytes=128<<10,MaintenanceReserveMemoryBytes=32<<10,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(8)}});this.Owner.ConfigureMaintenance(()=>this.Journal.CheckpointAsync((wal,lsn)=>{this.CheckpointLsn=lsn;return ValueTask.CompletedTask;}));
        }
        public async ValueTask DisposeAsync(){await this.Owner.DisposeAsync();await this.Journal.DisposeAsync();this.resources.Dispose();}
    }
}
