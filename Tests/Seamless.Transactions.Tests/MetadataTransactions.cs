using System.Text;
using Seamless.Execution;

namespace Seamless.Transactions.Tests;

public sealed class MetadataTransactions{
    [Fact]
    public async Task MetadataOnlyCommitIsDurableAndTrackedAfterRestart(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");var path=Path.Combine(root,"metadata-"+Guid.NewGuid().ToString("N"));var metadata=Encoding.UTF8.GetBytes("generation replacement");Guid id;
        using(var resources=new ExecutionScope()){
            await using var journal=new TransactionJournal(path,budget:resources.Budget);await using var coordinator=new TransactionCoordinator(journal);await using var tx=await coordinator.BeginAsync();id=tx.Id;
            var receipt=await tx.CommitAsync(metadata);Assert.Equal(TransactionCommitKind.Committed,receipt.Kind);Assert.True(receipt.Lsn>0);Assert.Equal(TransactionOutcomeKind.Committed,(await coordinator.GetOutcomeAsync(id)).Kind);
        }
        await using(var journal=new TransactionJournal(path)){
            Assert.Equal(TransactionOutcomeKind.Committed,journal.GetOutcome(id).Kind);var transaction=Assert.Single(journal.ReadTransactions());Assert.Empty(transaction.Draft.Entries);Assert.Equal(metadata,transaction.Draft.Metadata);
        }
    }
}
