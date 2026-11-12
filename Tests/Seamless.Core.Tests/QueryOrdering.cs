using Seamless.Execution;
using Scope=Seamless.Execution.ExecutionScope;
using Budget=Seamless.Execution.ResourceBudget;
namespace Execution.Tests;

public sealed class QueryOrdering{
    [Fact]
    public void RejectsOversizedKeysBeforeTheSortAllocatesThem(){
        using var scope=new Scope(new(){MemoryLimitBytes=1024,TemporaryStorageLimitBytes=0});
        Assert.Throws<ResourceLimitException>(()=>{using var keys=new global::Seamless.Linq.QueryOrdering.KeyBudget<int>(scope,4096);});
        Assert.Equal(0,scope.Budget.MemoryBytes);
    }
    [Fact]
    public void RejectsAnOversizedElementBeforeYieldingItToTheSort(){
        using var scope=new Scope(new(){MemoryLimitBytes=1024,TemporaryStorageLimitBytes=0});
        using(var budget=new global::Seamless.Linq.QueryOrdering.InputBudget<OversizedElement>(scope,[])){
            using var iterator=budget.Read(Enumerable.Repeat(default(OversizedElement),1)).GetEnumerator();Assert.Throws<ResourceLimitException>(()=>iterator.MoveNext());
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);
    }
    [Fact]
    public void ReferenceKeysAreChargedWhileLinqComputesThemAndAllReservationsAreReturned(){
        var parent=new Budget(8192,0);var visited=0;
        using(var scope=new Scope(new(){MemoryLimitBytes=8192,TemporaryStorageLimitBytes=0,ParentBudget=parent})){
            using var keys=new global::Seamless.Linq.QueryOrdering.KeyBudget<string>(scope,0);
            using var budget=new global::Seamless.Linq.QueryOrdering.InputBudget<int>(scope,[keys]);
            var query=budget.Read(Enumerable.Range(0,16)).OrderBy(value=>{visited++;return keys.Capture(new string((char)('a'+value),1024));});
            Assert.Throws<ResourceLimitException>(()=>query.ToArray());Assert.InRange(visited,1,15);Assert.True(parent.MemoryBytes>0);
        }
        Assert.Equal(0,parent.MemoryBytes);
    }
    [Fact]
    public void ASubsequentSortReservesItsWorkspaceBeforeTheFirstSortReceivesAnElement(){
        var parent=new Budget(8192,0);var visited=0;var delivered=0;
        IEnumerable<int> Input(){visited++;yield return 1;}
        using(var scope=new Scope(new(){MemoryLimitBytes=8192,TemporaryStorageLimitBytes=0,ParentBudget=parent})){
            using var later=new global::Seamless.Linq.QueryOrdering.InputBudget<OversizedElement>(scope,[]);
            using var first=new global::Seamless.Linq.QueryOrdering.InputBudget<int>(scope,[later]);
            Assert.Throws<ResourceLimitException>(()=>{foreach(var value in first.Read(Input())){delivered++;}});
            Assert.Equal(1,visited);Assert.Equal(0,delivered);
        }
        Assert.Equal(0,parent.MemoryBytes);
    }
    [global::System.Runtime.CompilerServices.InlineArray(1024)]private struct OversizedElement{private byte first;}
}
