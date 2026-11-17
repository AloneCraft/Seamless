using Seamless.Execution;
using Seamless.Transactions.Collections;

namespace Seamless.Transactions.Tests;

public sealed class TransactionSavepoints{
    [Fact]
    public void HistoryPositionsSharePayloadAndRewindUnderExhaustedBudgets(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");
        using var resources=new ExecutionScope(new(){TemporaryDirectory=Path.Combine(root,"history-"+Guid.NewGuid().ToString("N")),MemoryLimitBytes=4<<20,TemporaryStorageLimitBytes=32<<20});
        using(var changes=new TransactionChangeSet<int,(int Id,long Value)>(resources,128<<10)){
            for(var i=0;i<2048;i++){changes.Set(i,new(false,(i,i)));}
            using var first=changes.CreateHistoryPoint();var bytes=resources.Budget.TemporaryStorageBytes;
            using(var same=changes.CreateHistoryPoint()){Assert.Equal(first.Position,same.Position);Assert.Equal(bytes,resources.Budget.TemporaryStorageBytes);}
            for(var i=0;i<2048;i++){changes.Set(i,new(false,(i,10000+i)));}
            using var second=changes.CreateHistoryPoint();Assert.True(second.Position>first.Position);
            changes.Set(4096,new(false,(4096,55)));changes.Set(0,new(true,default));
            changes.RollbackToHistoryPoint(second);Assert.False(changes.TryGetValue(4096,out _));Assert.Equal(10000,changes.Enumerate().Single(x=>x.Key==0).Value.Record.Value);
            // Undo only rewrites existing index slots and truncates files. It must not need extra space or buffers.
            using(var storage=resources.Budget.ReserveTemporaryStorage(resources.Budget.TemporaryStorageLimitBytes-resources.Budget.TemporaryStorageBytes))
            using(var memory=resources.Budget.ReserveMemory(resources.Budget.MemoryLimitBytes-resources.Budget.MemoryBytes)){
                changes.RollbackToHistoryPoint(first);
            }
            Assert.Equal(2048,changes.Enumerate().Count());Assert.Equal(0,changes.Enumerate().Single(x=>x.Key==0).Value.Record.Value);
            changes.Set(4096,new(false,(4096,88)));Assert.Equal(2049,changes.Enumerate().Count());
            changes.RollbackToHistoryPoint(first);Assert.False(changes.TryGetValue(4096,out _));
        }
        Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }

    [Fact]
    public async Task RestoreSpilledChangesAndReuseSavepoint(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");
        using var resources=new ExecutionScope(new(){TemporaryDirectory=Path.Combine(root,"savepoints-"+Guid.NewGuid().ToString("N")),MemoryLimitBytes=8<<20,TemporaryStorageLimitBytes=32<<20});
        await using var coordinator=new TransactionCoordinator();var data=new Dictionary<int,(int Id,long Value)>();
        var table=new TransactionalTable<int,(int Id,long Value)>(0,1,"savepoint-test",resources,1,key=>{var found=data.TryGetValue(key,out var value);return(found,value);},(key,value)=>data[key]=value,key=>data.Remove(key),static value=>value.Id,()=>data);
        coordinator.Register(table);
        await using(var tx=await coordinator.BeginAsync()){
            var rows=table.For(tx);for(var i=0;i<128;i++){Assert.True(rows.Add((i,i)));}
            tx.CreateSavepoint("same");rows[0]=(0,1000);tx.CreateSavepoint("same");rows[0]=(0,2000);Assert.True(rows.Remove(1));
            tx.RollbackToSavepoint("same");Assert.Equal(1000,rows[0].Value);Assert.True(rows.ContainsKey(1));
            rows[0]=(0,3000);tx.RollbackToSavepoint("same");Assert.Equal(1000,rows[0].Value);
            Assert.Throws<KeyNotFoundException>(()=>tx.RollbackToSavepoint("Same"));Assert.Equal(128,rows.Count());
            await tx.CommitAsync();
        }
        Assert.Equal(128,data.Count);Assert.Equal(1000,data[0].Value);Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }

    [Fact]
    public async Task RestoreRemovesParticipantsFirstUsedAfterSavepoint(){
        using var resources=new ExecutionScope();await using var coordinator=new TransactionCoordinator();var data=new Dictionary<int,(int Id,long Value)>();
        var table=new TransactionalTable<int,(int Id,long Value)>(0,1,"savepoint-test",resources,1024,key=>{var found=data.TryGetValue(key,out var value);return(found,value);},(key,value)=>data[key]=value,key=>data.Remove(key),static value=>value.Id,()=>data);coordinator.Register(table);
        await using(var tx=await coordinator.BeginAsync()){
            tx.CreateSavepoint("empty");Assert.True(table.For(tx).Add((1,10)));tx.RollbackToSavepoint("empty");Assert.False(table.For(tx).ContainsKey(1));
            await using(var child=tx.BeginNested()){Assert.True(table.For(child).Add((2,20)));await child.CommitAsync();}
            tx.RollbackToSavepoint("empty");await tx.CommitAsync();
        }
        Assert.Empty(data);
    }
}
