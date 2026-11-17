using System.Text;
using Seamless.Execution;
using Seamless.Transactions.Collections;

namespace Seamless.Transactions.Tests;

public sealed class PreparedTransactions{
    [Fact]
    public async Task AbortedPreparationAllowsAnotherStandaloneTransactionAndReplaysOnlyCommittedRows(){
        var path=PathForTest();var values=new Dictionary<int,(int Id,byte[] Payload)>();var injected=false;
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=4<<20,TemporaryStorageLimitBytes=16<<20,TemporaryDirectory=Path.Combine(path,"temporary")});
        TransactionalTable<int,(int Id,byte[] Payload)> Table()=>new(0,1,"prepared-table-v1",resources,1,key=>{var found=values.TryGetValue(key,out var row);return(found,row);},(key,row)=>values[key]=row,key=>values.Remove(key),row=>row.Id,()=>values);
        await using(var journal=new PreparedTransactionJournal(path,Options(),resources.Budget,phase=>{
            if(phase==PreparedTransactionPhase.PreparationDurable&&!injected){injected=true;throw new OperationCanceledException();}
        })){
            await using var coordinator=new TransactionCoordinator(preparedJournal:journal);var table=Table();coordinator.Register(table);
            await using(var transaction=await coordinator.BeginAsync()){
                for(var index=0;index<100;index++){table.For(transaction).Add((index,new byte[150]));}
                var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await transaction.CommitAsync());
                Assert.Equal(TransactionOutcomeKind.Aborted,error.Outcome);Assert.Empty(values);Assert.Null(coordinator.Failure.Error);
            }
            await using(var transaction=await coordinator.BeginAsync()){
                table.For(transaction).Add((999,new byte[]{9}));await transaction.CommitAsync();
            }
        }
        values.Clear();
        await using(var journal=new PreparedTransactionJournal(path,Options(),resources.Budget)){
            await using var coordinator=new TransactionCoordinator(preparedJournal:journal);coordinator.Register(Table());coordinator.Recover();
            var row=Assert.Single(values);Assert.Equal(999,row.Key);Assert.Equal(new byte[]{9},row.Value.Payload);
        }
        Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }

    [Fact]
    public async Task CoordinatorRecoversAfterCoveredWalSegmentsHaveBeenPruned(){
        var path=PathForTest();long covered=0;
        for(var generation=0;generation<4;generation++){
            await using var journal=new PreparedTransactionJournal(path,Options());
            journal.Replay((id,lsn,metadata,entries)=>{foreach(var entry in entries){}},covered);
            for(var index=0;index<24;index++){await journal.CommitAsync(Guid.NewGuid(),[],new byte[512]);}
            await journal.CheckpointAsync((id,lsn)=>{covered=lsn;return ValueTask.CompletedTask;});
        }
        await using var retained=new PreparedTransactionJournal(path,Options());
        await using var owner=new TransactionCoordinator(preparedJournal:retained);
        owner.Recover(covered);Assert.Equal(covered,owner.AppliedLsn);Assert.Null(owner.Failure.Error);
    }
    [Fact]
    public async Task CoveredCheckpointDoesNotRequireRetiredParticipantCodecs(){
        var path=PathForTest();long covered;
        await using(var journal=new PreparedTransactionJournal(path,Options())){covered=(await journal.CommitAsync(Guid.NewGuid(),Rows(2))).Lsn;}
        await using(var journal=new PreparedTransactionJournal(path,Options())){
            await using var owner=new TransactionCoordinator(preparedJournal:journal);
            // 保存済みの旧世代は所属も型も解放済み。保持WALのCRC・形式は引き続き検証する。
            owner.Recover(covered);Assert.Equal(covered,owner.AppliedLsn);Assert.Null(owner.Failure.Error);
        }
        await using(var journal=new PreparedTransactionJournal(path,Options())){
            await using var owner=new TransactionCoordinator(preparedJournal:journal);
            Assert.Throws<InvalidDataException>(()=>owner.Recover(0));
        }
    }
    private static string PathForTest()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root."),"prepared-"+Guid.NewGuid().ToString("N"));
    private static WalOptions Options()=>new(){MaxFrameBytes=1024,QueueBytes=4096,SegmentBytes=16384};
    private static IEnumerable<WalEntry> Rows(int count){
        var buffer=new byte[150];for(var index=0;index<count;index++){Array.Fill(buffer,(byte)index);yield return new(index%2,1,WalMutation.Upsert,"prepared-v1",BitConverter.GetBytes(index),buffer);}
    }
    [Fact]
    public async Task LargeTransactionReplaysAllPartitionsOnlyAfterItsFinalCommit(){
        var path=PathForTest();var id=Guid.NewGuid();var schema=Encoding.UTF8.GetBytes("two-database-generation-manifest");var phases=new List<PreparedTransactionPhase>();
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=1<<20,TemporaryStorageLimitBytes=8<<20});
        await using(var journal=new PreparedTransactionJournal(path,Options(),resources.Budget,phases.Add)){
            var committed=await journal.CommitAsync(id,Rows(80),schema);Assert.True(committed.Lsn>2);Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);Assert.True(phases.Count(phase=>phase==PreparedTransactionPhase.PreparationDurable)>1);
            var allocated=GC.GetAllocatedBytesForCurrentThread();
            for(var index=0;index<32;index++){Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);}
            Assert.InRange(GC.GetAllocatedBytesForCurrentThread()-allocated,0,128<<10);
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await journal.CommitAsync(id,Rows(1)));Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);
        }
        await using(var journal=new PreparedTransactionJournal(path,Options(),resources.Budget)){
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await journal.CommitAsync(Guid.NewGuid(),Rows(1)));
            var commits=0;journal.Replay((transaction,lsn,metadata,rows)=>{
                commits++;Assert.Equal(id,transaction);Assert.Equal(schema,metadata.ToArray());var count=0;
                foreach(var row in rows){var index=BitConverter.ToInt32(row.Key);Assert.Equal(count++,index);Assert.Equal(index%2,row.Partition);Assert.All(row.Row!,value=>Assert.Equal((byte)index,value));}Assert.Equal(80,count);
            });Assert.Equal(1,commits);Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);
        }
        Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }

    [Theory]
    [InlineData(PreparedTransactionPhase.PreparationDurable,false)]
    [InlineData(PreparedTransactionPhase.CommitDurable,true)]
    public async Task FailureBeforeOrAfterFinalCommitDoesNotReplayPartOfATransaction(PreparedTransactionPhase failure,bool committed){
        var path=PathForTest();var id=Guid.NewGuid();
        await using(var journal=new PreparedTransactionJournal(path,Options(),failureInjection:phase=>{if(phase==failure){throw new IOException("injected failure");}})){
            var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await journal.CommitAsync(id,Rows(80)));Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Aborted,error.Outcome);
        }
        await using(var journal=new PreparedTransactionJournal(path,Options())){
            var rows=0;var transactions=0;journal.Replay((transaction,lsn,metadata,entries)=>{transactions++;Assert.Equal(id,transaction);foreach(var row in entries){rows++;}});
            Assert.Equal(committed?80:0,rows);Assert.Equal(committed?1:0,transactions);Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Unknown,journal.GetOutcome(id).Kind);
            await journal.CommitAsync(Guid.NewGuid(),[],Encoding.UTF8.GetBytes("ddl-only"));
        }
    }

    [Fact]
    public async Task IncompleteReplayPreventsAcceptanceAndCanReleaseAllReaders(){
        var path=PathForTest();
        await using(var journal=new PreparedTransactionJournal(path,Options())){await journal.CommitAsync(Guid.NewGuid(),Rows(80));}
        await using(var journal=new PreparedTransactionJournal(path,Options())){
            Assert.Throws<InvalidOperationException>(()=>journal.Replay((id,lsn,metadata,entries)=>{_ =entries.First();}));
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await journal.CommitAsync(Guid.NewGuid(),[]));
            var count=0;journal.Replay((id,lsn,metadata,entries)=>{foreach(var entry in entries){count++;}});Assert.Equal(80,count);
        }
    }

    [Fact]
    public async Task CoordinatorStreamsSpilledChangesAndRecoversAllOfThem(){
        var path=PathForTest();var values=new Dictionary<int,(int Id,byte[] Payload)>();Guid id;
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=4<<20,TemporaryStorageLimitBytes=16<<20,TemporaryDirectory=Path.Combine(path,"temporary")});
        TransactionalTable<int,(int Id,byte[] Payload)> Table()=>new(0,1,"prepared-table-v1",resources,1,key=>{var found=values.TryGetValue(key,out var row);return(found,row);},(key,row)=>values[key]=row,key=>values.Remove(key),row=>row.Id,()=>values);
        await using(var journal=new PreparedTransactionJournal(path,Options(),resources.Budget)){
            await using var coordinator=new TransactionCoordinator(preparedJournal:journal);var table=Table();coordinator.Register(table);
            await using var transaction=await coordinator.BeginAsync();id=transaction.Id;
            for(var index=0;index<100;index++){Assert.True(table.For(transaction).Add((index,new byte[150])));}
            transaction.CreateSavepoint("user");transaction.CreateSavepoint("statement");table.For(transaction)[0]=(0,new byte[]{9});transaction.RollbackToSavepoint("statement");transaction.ReleaseSavepoint("statement");Assert.Throws<KeyNotFoundException>(()=>transaction.RollbackToSavepoint("statement"));
            await transaction.CommitAsync();Assert.Equal(100,values.Count);Assert.Equal(TransactionOutcomeKind.Committed,(await coordinator.GetOutcomeAsync(id)).Kind);
        }
        values.Clear();
        await using(var journal=new PreparedTransactionJournal(path,Options(),resources.Budget)){
            await using var coordinator=new TransactionCoordinator(preparedJournal:journal);coordinator.Register(Table());coordinator.Recover();Assert.Equal(100,values.Count);Assert.All(values.Values,row=>Assert.Equal(150,row.Payload.Length));Assert.Equal(TransactionOutcomeKind.Committed,(await coordinator.GetOutcomeAsync(id)).Kind);
        }
        Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }
}
