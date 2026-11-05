using Seamless.Transactions;
namespace Seamless.Transactions.Tests;

public sealed class ExternalCommitTests{
    private static string NewPath()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"external-"+Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task MetadataOnlyCommitUsesTheRequestedIdentityAndAppliesOnlyAfterDurability(){
        await using var journal=new PreparedTransactionJournal(NewPath());await using var coordinator=new TransactionCoordinator(null,new(),journal);
        var id=Guid.NewGuid();long applied=0;
        using var changes=new Changes(lsn=>{Assert.True(lsn>0);Assert.True(journal.DurableLsn>=lsn);Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);applied=lsn;});
        var result=await coordinator.CommitExternalAsync(id,changes,new byte[]{1,2,3});Assert.Equal(id,result.Id);Assert.Equal(applied,result.Lsn);Assert.Equal(applied,coordinator.AppliedLsn);
    }
    [Fact]
    public async Task ApplicationFailureKeepsTheDurableOutcomeAndDoesNotAdvanceAppliedLsn(){
        await using var journal=new PreparedTransactionJournal(NewPath());await using var coordinator=new TransactionCoordinator(null,new(),journal);var id=Guid.NewGuid();
        using var changes=new Changes(_=>throw new IOException("application failed"));
        var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await coordinator.CommitExternalAsync(id,changes,new byte[]{4}));
        Assert.Equal(TransactionOutcomeKind.Committed,error.Outcome);Assert.Equal(TransactionOutcomeKind.Committed,(await coordinator.GetOutcomeAsync(id)).Kind);Assert.Equal(0,coordinator.AppliedLsn);
    }
    [Fact]
    public async Task EmptyExternalCommitStillHasADurableIdentity(){
        await using var journal=new PreparedTransactionJournal(NewPath());await using var coordinator=new TransactionCoordinator(null,new(),journal);var id=Guid.NewGuid();long applied=0;
        using var changes=new Changes(lsn=>applied=lsn);var result=await coordinator.CommitExternalAsync(id,changes);
        Assert.Equal(TransactionCommitKind.Committed,result.Kind);Assert.True(applied>0);Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FailedPreparationAbortsOnlyThatRequestAndNextCommitCanProceed(int failure){
        using var cancellation=new CancellationTokenSource();var prepared=false;
        await using var journal=new PreparedTransactionJournal(NewPath(),new(){MaxFrameBytes=1024,QueueBytes=4096,SegmentBytes=16384},failureInjection:phase=>{
            if(phase!=PreparedTransactionPhase.PreparationDurable){return;}prepared=true;if(failure==1){throw new IOException("prepared chunk failed");}if(failure==2){cancellation.Cancel();}
        });await using var coordinator=new TransactionCoordinator(null,new(),journal);var id=Guid.NewGuid();
        IEnumerable<WalEntry> Rows(){if(failure==0){throw new IOException("row preparation failed");}return Enumerable.Range(0,16).Select(index=>new WalEntry(1,1,WalMutation.Upsert,"test",BitConverter.GetBytes(index),new byte[150]));}
        using var rejected=new Changes(_=>throw new InvalidOperationException("Must not apply."),Rows);
        var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await coordinator.CommitExternalAsync(id,rejected,cancellationToken:cancellation.Token));Assert.Equal(TransactionOutcomeKind.Aborted,error.Outcome);Assert.Null(coordinator.Failure.Error);Assert.Equal(TransactionOutcomeKind.Aborted,journal.GetOutcome(id).Kind);Assert.Equal(failure!=0,prepared);
        using var good=new Changes(_=>{});var committed=await coordinator.CommitExternalAsync(Guid.NewGuid(),good,new byte[]{8});Assert.Equal(TransactionCommitKind.Committed,committed.Kind);
    }
    private sealed class Changes(Action<long> apply,Func<IEnumerable<WalEntry>>? rows=null):ICommittedTransactionChanges{
        public IReadOnlyList<WalEntry> Encode()=>throw new InvalidOperationException("Must stream.");
        public IEnumerable<WalEntry> EnumerateEncoded(){if(rows is not null){return rows();}return [];}
        public void Apply()=>throw new InvalidOperationException("Durable LSN required.");
        public void Apply(long lsn)=>apply(lsn);
        public void Dispose(){}
    }
}
