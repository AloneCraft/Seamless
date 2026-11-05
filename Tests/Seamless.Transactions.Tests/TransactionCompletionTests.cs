using Seamless.Execution;
namespace Seamless.Transactions.Tests;

public sealed class TransactionCompletionTests{
    [Fact]
    public async Task CompletionHooksRunOnceAtTheRootAndCanBeUnregisteredEarly(){
        await using var owner=new TransactionCoordinator();var completed=0;
        await using(var root=await owner.BeginAsync()){
            using(var removed=root.OnCompletion(()=>completed+=100)){}
            using var registration=root.OnCompletion(()=>completed++);await using(var nested=root.BeginNested()){await nested.CommitAsync();Assert.Equal(0,completed);}await root.CommitAsync();Assert.Equal(1,completed);
        }
        await using(var root=await owner.BeginAsync()){using var registration=root.OnCompletion(()=>completed++);await root.RollbackAsync();Assert.Equal(2,completed);}
        Assert.Equal(2,completed);Assert.Null(owner.Failure.Error);
    }
    [Fact]
    public async Task AFailingCompletionStillReleasesOtherResourcesAndFaultsTheDomain(){
        await using var owner=new TransactionCoordinator();var released=false;await using var root=await owner.BeginReadOnlyAsync();using var first=root.OnCompletion(static()=>throw new IOException("cleanup failed"));using var second=root.OnCompletion(()=>released=true);
        var error=await Assert.ThrowsAsync<TransactionCommitException>(async()=>await root.CommitAsync());Assert.Equal(root.Id,error.TransactionId);Assert.True(released);Assert.NotNull(owner.Failure.Error);
    }
}
