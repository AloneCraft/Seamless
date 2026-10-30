using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
namespace Seamless.Transactions.Tests;
public sealed class TransactionTests{
    private static TransactionalTable<int,(int Id,long Balance)> Accounts(TransactionCoordinator coordinator,ExecutionScope resources,Dictionary<int,(int Id,long Balance)> data){
        var table=new TransactionalTable<int,(int Id,long Balance)>(0,1,"account-tuple-v1",resources,1024,
            key=>{var found=data.TryGetValue(key,out var value);return(found,value);},(key,row)=>data[key]=row,key=>data.Remove(key),static row=>row.Id,()=>data);
        coordinator.Register(table);return table;
    }
    [Fact]public async Task TransferStagesAndCommitsAtomically(){
        using var resources=new ExecutionScope();await using var coordinator=new TransactionCoordinator();
        var data=new Dictionary<int,(int,long)>{{1,(1,100)},{2,(2,0)}};var table=Accounts(coordinator,resources,data);
        await using(var transaction=await coordinator.BeginAsync()){
            var accounts=table.For(transaction);accounts[1]=(1,60);accounts[2]=(2,40);
            Assert.Equal(100,data[1].Item2);
            Assert.Equal(60,accounts[1].Balance);
            Assert.Throws<InvalidOperationException>(()=>coordinator.EnterRead());
            Assert.Equal(TransactionCommitKind.Committed,(await transaction.CommitAsync()).Kind);
        }
        Assert.Equal(100,data[1].Item2+data[2].Item2);
        Assert.Equal(60,data[1].Item2);
    }
    [Fact]public async Task JoinedVoteIsNotDurableAndRollbackAbortsParent(){
        using var resources=new ExecutionScope();await using var coordinator=new TransactionCoordinator();
        var data=new Dictionary<int,(int,long)>{{1,(1,100)}};var table=Accounts(coordinator,resources,data);
        await using(var parent=await coordinator.BeginAsync()){
            await using(var child=parent.BeginNested()){
                Assert.Equal(parent.Id,child.Id);table.For(child)[1]=(1,10);
                Assert.Equal(TransactionCommitKind.Voted,(await child.CommitAsync()).Kind);
            }
            Assert.Equal(100,data[1].Item2);
            await parent.RollbackAsync();
        }
        await using(var parent=await coordinator.BeginAsync()){
            await using(var child=parent.BeginNested()){table.For(child)[1]=(1,50);await child.RollbackAsync();}
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await parent.CommitAsync());
        }
        Assert.Equal(100,data[1].Item2);
    }
    [Fact]public async Task ParentCannotCommitWithAnOpenChild(){
        await using var coordinator=new TransactionCoordinator();await using var parent=await coordinator.BeginAsync();
        await using var child=parent.BeginNested();
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await parent.CommitAsync());
        await child.RollbackAsync();await parent.RollbackAsync();
    }
    [Fact]public async Task OutcomesSurviveRestartAndExplicitForget(){
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.C12",Guid.NewGuid().ToString("N"));Guid id;
        try{
            using(var resources=new ExecutionScope()){
                await using var journal=new TransactionJournal(directory);await using var coordinator=new TransactionCoordinator(journal);
                var data=new Dictionary<int,(int,long)>();var table=Accounts(coordinator,resources,data);
                await using var transaction=await coordinator.BeginAsync();id=transaction.Id;table.For(transaction)[1]=(1,100);await transaction.CommitAsync();
                Assert.Equal(TransactionOutcomeKind.Committed,(await coordinator.GetOutcomeAsync(id)).Kind);
            }
            await using(var journal=new TransactionJournal(directory)){
                Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);
                await journal.ForgetThroughAsync(journal.Writer.DurableLsn);
                Assert.Equal(TransactionOutcomeKind.Unknown,journal.GetOutcome(id).Kind);
            }
            await using(var journal=new TransactionJournal(directory)){Assert.Equal(TransactionOutcomeKind.Unknown,journal.GetOutcome(id).Kind);}
        }finally{if(Directory.Exists(directory)){Directory.Delete(directory,true);}}
    }
    [Theory][InlineData(true,false)][InlineData(false,true)]public async Task DurableCommitRemainsKnownWhenApplyOrCleanupFails(bool applyFails,bool cleanupFails){
        var directory=Path.Combine(Path.GetTempPath(),"Seamless.C12.CommitFailure",Guid.NewGuid().ToString("N"));
        try{
            Guid id;
            await using(var journal=new TransactionJournal(directory)){
                await using var coordinator=new TransactionCoordinator(journal);await using var transaction=await coordinator.BeginAsync();id=transaction.Id;
                transaction.Enlist(new FailingChanges(applyFails,cleanupFails));
                var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await transaction.CommitAsync());Assert.Equal(id,error.TransactionId);Assert.Equal(TransactionOutcomeKind.Committed,error.Outcome);
                Assert.Equal(TransactionOutcomeKind.Committed,(await coordinator.GetOutcomeAsync(id)).Kind);
                await Assert.ThrowsAnyAsync<Exception>(async()=>await coordinator.BeginAsync());
            }
            await using(var reopened=new TransactionJournal(directory)){Assert.Equal(TransactionOutcomeKind.Committed,reopened.GetOutcome(id).Kind);}
        }finally{if(Directory.Exists(directory)){Directory.Delete(directory,true);}}
    }
    private sealed class FailingChanges(bool applyFails,bool cleanupFails):ITransactionChanges{
        public IReadOnlyList<WalEntry> Encode()=>[new(0,1,WalMutation.Upsert,"test",[1],[2])];
        public void Apply(){if(applyFails){throw new IOException("injected apply failure");}}
        public void Dispose(){if(cleanupFails){throw new IOException("injected cleanup failure");}}
    }
    [Fact]public async Task StoppingRejectsReadersAlreadyWaitingForOwnership(){
        await using var coordinator=new TransactionCoordinator();using var first=coordinator.EnterRead();using var entered=new ManualResetEventSlim();
        var waiting=Task.Run(()=>{entered.Set();using var read=coordinator.EnterRead();});Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));Assert.False(waiting.IsCompleted);
        coordinator.StopAccepting();first.Dispose();await Assert.ThrowsAsync<ObjectDisposedException>(()=>waiting);
    }
    [Fact]public async Task OversizedStagedTransactionFailsBeforeWalAcceptanceOrLiveMutation(){
        var path=Path.Combine(Path.GetTempPath(),"Seamless.C12.SizeBound",Guid.NewGuid().ToString("N"));
        try{
            using var resources=new ExecutionScope();await using var journal=new TransactionJournal(path,new WalOptions{MaxFrameBytes=512,QueueBytes=512});await using var coordinator=new TransactionCoordinator(journal);
            var data=new Dictionary<int,(int,long)>();var table=Accounts(coordinator,resources,data);await using var transaction=await coordinator.BeginAsync();
            for(var index=0;index<20;index++){table.For(transaction)[index]=(index,100);}
            var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await transaction.CommitAsync());Assert.IsType<InvalidDataException>(error.InnerException);
            Assert.Equal(0,journal.Writer.LastAcceptedLsn);Assert.Empty(data);
        }finally{if(Directory.Exists(path)){Directory.Delete(path,true);}}
    }
}
