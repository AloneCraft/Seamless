using Seamless.Execution;
using Scope=Seamless.Execution.ExecutionScope;
namespace Execution.Tests;

[Collection("HostCpuScheduler")]
public sealed class PartitionedLookupBuilder{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void GroupOrderAndElementOrderMatchSequentialGroupingWithSmallSpillBudgets(int maximumWorkers){
        using var scope=new Scope(new(){MemoryLimitBytes=128<<10,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=maximumWorkers,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        var expected=Enumerable.Range(0,1024).GroupBy(static value=>(value*7)%17).ToArray();
        using(var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,allowParallel:true)){
            lookup.Build(static output=>{for(var value=0;value<1024;value++){output.Add((value*7)%17,value);}});
            var actual=lookup.Groups().ToArray();Assert.Equal(expected.Select(static group=>group.Key),actual.Select(static group=>group.Key));
            for(var index=0;index<expected.Length;index++){Assert.Equal(expected[index],actual[index]);}
            Assert.Equal(1024,lookup.LongCount);
            foreach(var group in expected){Assert.Equal(group,lookup.Match(group.Key));}
            Assert.Empty(lookup.Match(-1));
            Assert.True(scope.Budget.PeakTemporaryStorageBytes>0);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);Assert.Equal(0,global::Seamless.Execution.CpuScheduler.Shared.Usage.Outstanding);
    }
    [Fact]
    public void ProducerFailureCancelsQueuedWorkAndReturnsEveryResource(){
        using var scope=new Scope(new(){MemoryLimitBytes=128<<10,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=3,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        using(var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,allowParallel:true)){
            var error=Assert.Throws<IOException>(()=>lookup.Build(static output=>{for(var value=0;value<64;value++){output.Add(value%2,value);}throw new IOException("producer failure");}));
            Assert.Equal("producer failure",error.Message);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);Assert.Equal(0,global::Seamless.Execution.CpuScheduler.Shared.Usage.Outstanding);
    }
    [Fact]
    public void NullKeysAndStringComparisonKeepTheirGroupingMeaning(){
        using var scope=new Scope(new(){MemoryLimitBytes=128<<10,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=3,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<string?,int>(scope,StringComparer.OrdinalIgnoreCase,allowParallel:true);
        lookup.Build(static output=>{output.Add("A",1);output.Add(null,2);output.Add("a",3);output.Add("B",4);});
        var groups=lookup.Groups().ToArray();Assert.Equal(new string?[]{"A",null,"B"},groups.Select(static group=>group.Key));Assert.Equal(new[]{1,3},groups[0]);Assert.Equal(new[]{2},groups[1]);
    }
    [Fact]
    public void UnknownReferenceGraphsStayOnTheProducerAndPreserveTheirObjects(){
        using var scope=new Scope(new(){MemoryLimitBytes=128<<10,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=3,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        var key=new object();var value=new[]{1,2};
        using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<object,int[]>(scope,allowParallel:true);Assert.False(lookup.IsParallel);
        lookup.Build(output=>{output.Add(key,value);output.Add(key,value);});var group=Assert.Single(lookup.Groups());
        Assert.Same(key,group.Key);Assert.All(group,item=>Assert.Same(value,item));Assert.Equal(0,scope.Budget.PeakTemporaryStorageBytes);
    }
    [Fact]
    public void SpilledStringGroupsKeepTheFirstKeyObject(){
        using var scope=new Scope(new(){MemoryLimitBytes=128<<10,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=3,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        var keys=Enumerable.Range(0,17).Select(static index=>"key"+index).ToArray();
        using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<string,int>(scope,allowParallel:true);
        lookup.Build(output=>{for(var index=0;index<1024;index++){var key=keys[index%keys.Length];if(index>=keys.Length){key=new string(key.AsSpan());}output.Add(key,index);}});
        var groups=lookup.Groups().ToArray();Assert.Equal(keys.Length,groups.Length);Assert.True(scope.Budget.PeakTemporaryStorageBytes>0);
        for(var index=0;index<groups.Length;index++){Assert.Same(keys[index],groups[index].Key);}
    }
}
