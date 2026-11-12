namespace Seamless.Transactions.Tests;

public sealed class DatabaseLockManagerTests{
    [Fact]
    public async Task UnrelatedDatabasesRunWhileSchemaChangeWaits(){
        using var manager=new DatabaseLockManager();var reader=Guid.NewGuid();var migration=Guid.NewGuid();
        var read=await manager.AcquireAsync(reader,1,DatabaseLockMode.Read);
        var changing=manager.AcquireAsync(migration,1,DatabaseLockMode.Schema);
        Assert.False(changing.IsCompleted);
        using(var unrelated=await manager.AcquireAsync(Guid.NewGuid(),2,DatabaseLockMode.Write).WaitAsync(TimeSpan.FromSeconds(5))){}
        read.Dispose();using var schema=await changing.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeadlockVictimReleasesOnlyWhenItsTransactionCleansUp(){
        using var manager=new DatabaseLockManager();var first=Guid.NewGuid();var second=Guid.NewGuid();
        using var firstDb=await manager.AcquireAsync(first,1,DatabaseLockMode.Write);
        var secondDb=await manager.AcquireAsync(second,2,DatabaseLockMode.Write);
        var waiting=manager.AcquireAsync(first,2,DatabaseLockMode.Write);
        var error=await Assert.ThrowsAsync<TransactionDeadlockException>(()=>manager.AcquireAsync(second,1,DatabaseLockMode.Write));Assert.Equal(second,error.TransactionId);
        Assert.False(waiting.IsCompleted);secondDb.Dispose();using var acquired=await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancellationDoesNotLeakWaitersAndReadersShare(){
        using var manager=new DatabaseLockManager();using var first=await manager.AcquireAsync(Guid.NewGuid(),1,DatabaseLockMode.Read);using var second=await manager.AcquireAsync(Guid.NewGuid(),1,DatabaseLockMode.Read);
        using var cancellation=new CancellationTokenSource();var waiting=manager.AcquireAsync(Guid.NewGuid(),1,DatabaseLockMode.Schema,cancellation.Token);cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiting);
        using var third=await manager.AcquireAsync(Guid.NewGuid(),1,DatabaseLockMode.Read).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
