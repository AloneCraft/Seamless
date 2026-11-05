using Seamless.Execution;
using Scope=Seamless.Execution.ExecutionScope;
namespace Execution.Tests;

public sealed class QueryResultBuffer{
    [Fact]
    public void MaterializationAccountsForGrowthAndReturnsCallerOwnedResults(){
        using var scope=new Scope(new(){MemoryLimitBytes=8192,TemporaryStorageLimitBytes=0});
        int[] result;
        using(var buffer=new global::Seamless.Execution.QueryResultBuffer<int>(scope)){
            for(var index=0;index<16;index++){buffer.Add(index);}
            Assert.True(scope.Budget.MemoryBytes>16*sizeof(int));result=buffer.ToArray();
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(Enumerable.Range(0,16),result);
        using(var buffer=new global::Seamless.Execution.QueryResultBuffer<int>(scope)){buffer.Add(7);Assert.Equal(new[]{7},buffer.ToList());}
        Assert.Equal(0,scope.Budget.MemoryBytes);
    }
    [Fact]
    public void ResourceFailureAndValidationFailureReleaseEveryReservation(){
        using var scope=new Scope(new(){MemoryLimitBytes=512,TemporaryStorageLimitBytes=0});
        using(var buffer=new global::Seamless.Execution.QueryResultBuffer<int>(scope)){Assert.Throws<ResourceLimitException>(()=>{for(var index=0;index<100;index++){buffer.Add(index);}});}
        Assert.Equal(0,scope.Budget.MemoryBytes);
        var abort=false;
        using(var buffer=new global::Seamless.Execution.QueryResultBuffer<int>(scope,()=>{if(abort){throw new OperationCanceledException();}})){
            buffer.Add(1);abort=true;Assert.Throws<OperationCanceledException>(()=>buffer.ToArray());
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);
    }
    [Fact]
    public void DictionaryPreservesComparersAndDuplicateErrorsAndReturnsItsReservation(){
        using var scope=new Scope(new(){MemoryLimitBytes=8192,TemporaryStorageLimitBytes=0});Dictionary<string,int> result;
        using(var buffer=new QueryDictionaryBuffer<string,int>(scope,StringComparer.OrdinalIgnoreCase)){
            buffer.Add("A",1);Assert.Throws<ArgumentException>(()=>buffer.Add("a",2));result=buffer.ToDictionary();
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(1,result["a"]);
    }
}
