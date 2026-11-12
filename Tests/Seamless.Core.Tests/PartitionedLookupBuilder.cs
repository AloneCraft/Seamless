using Seamless.Execution;
using Scope=Seamless.Execution.ExecutionScope;
namespace Execution.Tests;

[Collection("HostCpuScheduler")]
public sealed class PartitionedLookupBuilder{
    [Theory]
    [InlineData(16,0)]
    [InlineData(16,1)]
    [InlineData(4,0)]
    public void SkewedWorkersShareRetentionWithoutChangingLookupResults(int memoryMiB,int shift){
        if(global::Seamless.Execution.CpuScheduler.Shared.Usage.Capacity<4){return;}
        using var scope=new Scope(new(){
            MemoryLimitBytes=memoryMiB<<20,TemporaryStorageLimitBytes=8<<20,MaximumQueryWorkers=4,
            TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!
        });
        using(var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,allowParallel:true)){
            Assert.Equal(3,lookup.WorkerCount);
            lookup.Build(output=>{for(var value=0;value<4096;value++){output.Add(value*3+shift,value);}});
            Assert.Equal(Enumerable.Range(0,4096).Select(value=>value*3+shift),lookup.Groups().Select(group=>group.Key));
            Assert.Equal(new[]{1024},lookup.Match(3072+shift));
            Assert.Empty(lookup.Match(-1));
            Assert.Equal(memoryMiB==4,scope.Budget.PeakTemporaryStorageBytes>0);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);
        Assert.Equal(0,scope.Budget.TemporaryStorageBytes);
        Assert.Equal(0,global::Seamless.Execution.CpuScheduler.Shared.Usage.Outstanding);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void InlineValuesThatFitTheBudgetDoNotSpillAndKeepTheirOrder(int maximumWorkers){
        using var scope=new Scope(new(){MemoryLimitBytes=8<<20,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=maximumWorkers,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        using(var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,(int Id,long Balance)>(scope,allowParallel:true)){
            lookup.Build(static output=>{for(var id=0;id<8192;id++){output.Add(id%64,(id,100+id%17));}});
            var groups=lookup.Groups().ToArray();Assert.Equal(64,groups.Length);
            for(var key=0;key<64;key++){
                Assert.Equal(key,groups[key].Key);
                Assert.Equal(Enumerable.Range(0,128).Select(index=>(key+index*64,100L+(key+index*64)%17)),groups[key]);
            }
            Assert.Equal(0,scope.Budget.PeakTemporaryStorageBytes);Assert.InRange(scope.Budget.PeakMemoryBytes,1,8<<20);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);Assert.Equal(0,global::Seamless.Execution.CpuScheduler.Shared.Usage.Outstanding);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void GroupOrderAndElementOrderMatchSequentialGroupingWithSmallSpillBudgets(int maximumWorkers){
        using var scope=new Scope(new(){MemoryLimitBytes=128<<10,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=maximumWorkers,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
        foreach(var skewed in new[]{false,true}){
            int Key(int value)=>skewed&&value<1000?0:(value*7)%17;
            var expected=Enumerable.Range(0,1024).GroupBy(Key).ToArray();
            using(var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,allowParallel:true)){
                lookup.Build(output=>{for(var value=0;value<1024;value++){output.Add(Key(value),value);}});
                var actual=lookup.Groups().ToArray();Assert.Equal(expected.Select(static group=>group.Key),actual.Select(static group=>group.Key));
                for(var index=0;index<expected.Length;index++){Assert.Equal(expected[index],actual[index]);}
                Assert.Equal(1024,lookup.LongCount);
                foreach(var group in expected){Assert.Equal(group,lookup.Match(group.Key));}
                Assert.Empty(lookup.Match(-1));
                Assert.True(scope.Budget.PeakTemporaryStorageBytes>0);
            }
            Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Equal(0,scope.Budget.TemporaryStorageBytes);Assert.Equal(0,global::Seamless.Execution.CpuScheduler.Shared.Usage.Outstanding);
        }
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
    [Theory]
    [InlineData(false,256)]
    [InlineData(true,256)]
    [InlineData(false,1024)]
    [InlineData(true,1024)]
    public async Task ConcurrentLookupsShareBudgetAndReleaseResources(bool cancelFirst,int memoryKiB){
        var parent=new global::Seamless.Execution.ResourceBudget(memoryKiB<<10,8<<20);
        using var started=new Barrier(2);
        await Task.WhenAll(Task.Run(()=>Run(cancelFirst)),Task.Run(()=>Run(false)));
        Assert.Equal(0,parent.MemoryBytes);
        Assert.Equal(0,parent.TemporaryStorageBytes);
        Assert.InRange(parent.PeakMemoryBytes,1,parent.MemoryLimitBytes);
        if(memoryKiB==256){
            Assert.True(parent.PeakTemporaryStorageBytes>0);
        }
        Assert.Equal(0,global::Seamless.Execution.CpuScheduler.Shared.Usage.Outstanding);

        void Run(bool cancel){
            using var cancellation=new CancellationTokenSource();
            using var scope=new Scope(new(){
                ParentBudget=parent,MemoryLimitBytes=memoryKiB<<9,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=3,
                TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,CancellationToken=cancellation.Token
            });
            using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,allowParallel:true);
            void Build()=>lookup.Build(output=>{
                Assert.True(started.SignalAndWait(TimeSpan.FromSeconds(10)));
                for(var value=0;value<1024;value++){
                    if(cancel&&value==768){cancellation.Cancel();}
                    output.Add(value%17,value);
                }
            });
            if(cancel){
                Assert.ThrowsAny<OperationCanceledException>(Build);
            }else{
                Build();
                Assert.Equal(Enumerable.Range(0,17),lookup.Groups().Select(static group=>group.Key));
                Assert.Equal(Enumerable.Range(0,1024).Where(static value=>value%17==3),lookup.Match(3));
            }
        }
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
