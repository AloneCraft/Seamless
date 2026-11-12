using Seamless.Transactions;

namespace Seamless.Database.Transactions.Tests;

public sealed class CommitMetadataTests{
    private sealed class DeferredSchema(DatabaseContainerOptions options):DatabaseContainer(options){
        internal int SchemaCalls;
        protected override void EnsureTransactionSchema(){this.SchemaCalls++;throw new InvalidOperationException("The schema getter must not run during metadata inspection.");}
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated root."),"db-audit-"+Guid.NewGuid().ToString("N"));
    private static DatabaseContainerOptions Options(string root)=>DatabaseContainerOptions.OnStorage(root) with{TransactionMetadataConsumer="audit-v1",IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(root,"temporary")};
    [Fact]public void SynchronousCommitStoresRowsAndAnImmutableMetadataSnapshotTogether(){
        var root=Root();Guid id;using(var database=new BankDatabase(Options(root))){using(var transaction=database.BeginTransaction()){id=transaction.Id;transaction.Accounts.Add(new(1,123));byte[] payload=[7,8,9];transaction.Commit(payload);payload[0]=99;}var journal=database.MetadataJournal!;using var page=journal.ReadMetadataBatch();var row=Assert.Single(page.Records);Assert.Equal(id,row.TransactionId);Assert.Equal(new byte[]{7,8,9},row.Payload.ToArray());Assert.Equal(123,database.Accounts[1].Balance);Assert.Single(journal.ReadTransactions().Single().Draft.Entries);}
        using var reopened=new BankDatabase(Options(root));Assert.Equal(123,reopened.Accounts[1].Balance);using var restored=reopened.MetadataJournal!.ReadMetadataBatch();Assert.Equal(id,Assert.Single(restored.Records).TransactionId);
    }
    [Fact]public void RollbackDoesNotCommitRowsOrMetadata(){using var database=new BankDatabase(Options(Root()));using(var transaction=database.BeginTransaction()){transaction.Accounts.Add(new(1,123));transaction.Rollback();}Assert.Empty(database.Accounts);using var page=database.MetadataJournal!.ReadMetadataBatch();Assert.Empty(page.Records);Assert.Equal(0,page.DurableCeiling);}
    [Fact]public void InnerScopeCannotChooseTheOuterCommitMetadata(){using var database=new BankDatabase(Options(Root()));using(var outer=database.BeginTransaction()){using(var inner=outer.BeginTransaction()){Assert.Throws<InvalidOperationException>(()=>inner.Commit(new byte[]{1}));inner.Commit();}outer.Accounts.Add(new(1,123));outer.Commit(new byte[]{2});}using var page=database.MetadataJournal!.ReadMetadataBatch();Assert.Equal(2,Assert.Single(page.Records).Payload.Span[0]);}
    [Fact]public void MetadataRetentionRequiresPersistentStandaloneSharedTransactions(){var root=Root();Assert.Throws<ArgumentException>(()=>new BankDatabase(Options(root) with{StorageMode=DatabaseStorageMode.Memory}));}
    [Fact]public void ExistingWalIsNotConvertedByAddingMetadataRetention(){var root=Root();using(var original=new BankDatabase(Options(root) with{TransactionMetadataConsumer=null})){using var transaction=original.BeginTransaction();transaction.Accounts.Add(new(1,123));transaction.Commit();}using(var upgraded=new BankDatabase(Options(root))){Assert.Throws<InvalidDataException>(()=>upgraded.MetadataJournal);}using var restored=new BankDatabase(Options(root) with{TransactionMetadataConsumer=null});Assert.Equal(123,restored.Accounts[1].Balance);Assert.Null(restored.MetadataJournal);}
    [Fact]public void InvalidMetadataIsRejectedBeforeVotingForCommit(){using var database=new BankDatabase(Options(Root()));using(var transaction=database.BeginTransaction()){transaction.Accounts.Add(new(1,123));Assert.ThrowsAny<Exception>(()=>transaction.Commit(new byte[4<<20]));}Assert.Empty(database.Accounts);using var page=database.MetadataJournal!.ReadMetadataBatch();Assert.Empty(page.Records);}
    [Fact]public void MetadataInspectionDoesNotExecuteTheDeferredSchema(){using var database=new DeferredSchema(Options(Root()));Assert.NotNull(database.MetadataJournal);Assert.Equal(0,database.SchemaCalls);Assert.Throws<InvalidOperationException>(()=>database.BeginTransaction());Assert.Equal(1,database.SchemaCalls);}
}
