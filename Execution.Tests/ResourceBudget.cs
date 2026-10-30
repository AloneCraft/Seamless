using Budget=Seamless.Execution.ResourceBudget;
using Seamless.Execution;

namespace Execution.Tests;

public sealed class ResourceBudget{
    [Fact]public void CreateChild(){
        var parent=new Budget(100,100);
        var first=parent.CreateChild(80,80);
        var second=parent.CreateChild(80,80);
        using var a=first.ReserveMemory(60);
        using var b=second.ReserveMemory(30);
        Assert.Throws<ResourceLimitException>(()=>b.Resize(50));
        Assert.Equal(30,b.Bytes);Assert.Equal(90,parent.MemoryBytes);Assert.Equal(30,second.MemoryBytes);
        Assert.Throws<ResourceLimitException>(()=>first.ReserveMemory(21));
        a.Resize(10);b.Resize(70);
        Assert.Equal(80,parent.MemoryBytes);Assert.Equal(90,parent.PeakMemoryBytes);
        using var disk=first.CreateChild(20,20).ReserveTemporaryStorage(20);
        Assert.Equal(20,parent.TemporaryStorageBytes);
        using var siblingDisk=second.ReserveTemporaryStorage(80);
        Assert.Throws<ResourceLimitException>(()=>disk.Resize(21));
        Assert.Equal(100,parent.TemporaryStorageBytes);Assert.Equal(20,disk.Bytes);
        siblingDisk.Dispose();disk.Dispose();Assert.Equal(0,parent.TemporaryStorageBytes);
    }
    [Fact]public void ReservationIsAtomicAcrossThreads(){
        var parent=new Budget(128,128);
        var children=Enumerable.Range(0,8).Select(_=>parent.CreateChild(128,128)).ToArray();
        Parallel.ForEach(children,child=>{
            for(var index=0;index<1000;index++){
                using var reservation=child.ReserveMemory(8);
                reservation.Resize(16);
                Parallel.Invoke(reservation.Dispose,reservation.Dispose);
            }
        });
        Assert.Equal(0,parent.MemoryBytes);Assert.InRange(parent.PeakMemoryBytes,1,128);
        Assert.All(children,child=>Assert.Equal(0,child.MemoryBytes));
    }
    [Fact]public void StoreAndScopeShareTheParent(){
        var parent=new Budget(100,100);
        using var store=Seamless.Execution.Store.CreateMemory(new(){MemoryBytes=100,DiskBytes=100},parentBudget:parent);
        using var scope=new Seamless.Execution.ExecutionScope(new(){MemoryLimitBytes=100,TemporaryStorageLimitBytes=100,ParentBudget=parent});
        using var first=store.Budget.ReserveMemory(60);
        using var second=scope.Budget.ReserveMemory(40);
        Assert.Throws<ResourceLimitException>(()=>scope.Budget.ReserveMemory(1));
        Assert.Equal(100,parent.MemoryBytes);
    }
}
