using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions.Collections;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class VersionedTableTests{
    [Theory]
    [InlineData(VersionedRecordEncoding.Canonical)]
    [InlineData(VersionedRecordEncoding.Auto)]
    public async Task QuiescentSnapshotHashMatchesCanonicalRowsAndIgnoresSupersededVersions(VersionedRecordEncoding encoding){
        await using var fixture=new Fixture(encoding:encoding);await fixture.Seed();
        await using(var update=await fixture.Owner.BeginAsync()){var table=fixture.Left.For(update);table.Remove(1);table[2]=(2,250);table.Add((65,300));await update.CommitAsync();}
        Assert.Throws<InvalidOperationException>(()=>((ITransactionParticipant)fixture.Left).GetCommittedSnapshot());
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=1<<20,TemporaryStorageLimitBytes=0,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        var expected=new TransactionalTable<int,(int Id,long Balance)>(0,1,"left",resources,1024,static _=>throw new NotSupportedException(),static(_,_)=>throw new NotSupportedException(),static _=>throw new NotSupportedException(),static row=>row.Id,Rows);
        fixture.Left.CloseForMaintenance();
        try{var snapshot=((ITransactionParticipant)fixture.Left).GetCommittedSnapshot();Assert.Equal(64,snapshot.RowCount);Assert.Equal(expected.GetCommittedSnapshot(),snapshot);Assert.Equal(snapshot,((ITransactionParticipant)fixture.Left).GetCommittedSnapshot());}
        finally{fixture.Left.ReopenAfterMaintenance();}
        await using var read=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(250,fixture.Left.For(read)[2].Balance);await read.CommitAsync();
        static IEnumerable<KeyValuePair<int,(int Id,long Balance)>> Rows(){yield return new(65,(65,300));yield return new(2,(2,250));for(var key=64;key>=3;key--){yield return new(key,(key,100));}}
    }
    [Theory]
    [InlineData(VersionedRecordEncoding.Canonical)]
    [InlineData(VersionedRecordEncoding.Auto)]
    public async Task InitialSnapshotUsesOneBaseVersionAndCannotBeReopenedAfterNormalUse(VersionedRecordEncoding encoding){
        await using var fixture=new Fixture(encoding:encoding);
        Assert.Equal(0,fixture.Left.RecoveredVersion);
        using(var initial=fixture.Left.BeginUnpublishedSnapshot()){
            Assert.True(initial.Add((1,100)));Assert.False(initial.Add((1,999)));Assert.Equal(1,initial.RowCount);
            Assert.Throws<InvalidOperationException>(()=>_=fixture.Left.RecoveredVersion);Assert.Throws<InvalidOperationException>(()=>fixture.Left.CloseForMaintenance());
            await Task.Run(()=>Assert.True(initial.Add((2,200))));initial.Complete();Assert.Equal(2,initial.RowCount);Assert.Throws<ObjectDisposedException>(()=>initial.Add((3,300)));
        }
        await using(var read=await fixture.Owner.BeginReadOnlyAsync()){Assert.Equal(new long[]{100,200},fixture.Left.For(read).OrderBy(static row=>row.Id).Select(static row=>row.Balance));Assert.Equal(0,(await read.CommitAsync()).Lsn);}
        Assert.Equal(1,fixture.Left.RecoveredVersion);Assert.Throws<InvalidOperationException>(()=>fixture.Left.BeginUnpublishedSnapshot());
        await using(var update=await fixture.Owner.BeginAsync()){fixture.Left.For(update)[1]=(1,50);await update.CommitAsync();}
        await fixture.Owner.ReclaimHistoryAsync();await using var current=await fixture.Owner.BeginReadOnlyAsync();Assert.Equal(50,fixture.Left.For(current)[1].Balance);Assert.Equal(200,fixture.Left.For(current)[2].Balance);await current.CommitAsync();
    }
    [Fact]
    public async Task EmptyTableIsSealedWhenTheDomainStartsEvenWithoutReadingThatTable(){
        await using var fixture=new Fixture();await using(var read=await fixture.Owner.BeginReadOnlyAsync()){await read.CommitAsync();}
        Assert.Equal(0,fixture.Left.RecoveredVersion);Assert.Throws<InvalidOperationException>(()=>fixture.Left.BeginUnpublishedSnapshot());
    }
    [Fact]
    public async Task AbandonedInitialSnapshotRemainsUnavailableAndCanBeDisposed(){
        var fixture=new Fixture();
        try{
            using(var initial=fixture.Left.BeginUnpublishedSnapshot()){Assert.True(initial.Add((1,100)));}
            Assert.Throws<InvalidOperationException>(()=>_=fixture.Left.RecoveredVersion);Assert.Throws<InvalidOperationException>(()=>fixture.Left.BeginUnpublishedSnapshot());
        }finally{await fixture.DisposeAsync();}
        // 投入済みnativeページも所有者の破棄で返る。writer終了だけで行の保存領域が消える契約にはしない。
        Assert.Equal(0,fixture.MemoryBytes);
    }
    [Fact]
    public void EmptyRecoveredTableCannotAcceptAnInitialSnapshot(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"empty-snapshot-"+Guid.NewGuid().ToString("N"));
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=32<<20,TemporaryStorageLimitBytes=1<<20,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        var tuning=new TsavoriteTuning{IndexSize=64,PageSize=4096,SegmentSize=65536,LogMemorySize=65536};var identity=VersionedTransactionalTable<int,(int,long)>.StorageIdentity("empty",VersionedRecordEncoding.Canonical);
        using(var store=Store.Open(path,parentBudget:resources.Budget)){var data=store.CreateTsavoriteHistory("empty",tuning,identity);using var table=new VersionedTransactionalTable<int,(int Id,long Balance)>(0,1,"empty",data,resources,1024,static row=>row.Id);table.CloseForMaintenance();store.Save();}
        using(var store=Store.Open(path,parentBudget:resources.Budget)){var data=store.CreateTsavoriteHistory("empty",tuning,identity);Assert.True(data.HasRecoveredCheckpoint);Assert.Equal(0,data.MaximumCommittedVersion);using var table=new VersionedTransactionalTable<int,(int Id,long Balance)>(0,1,"empty",data,resources,1024,static row=>row.Id);Assert.Throws<InvalidOperationException>(()=>table.BeginUnpublishedSnapshot());}
    }
}
