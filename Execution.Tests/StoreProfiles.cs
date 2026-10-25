using Seamless.Execution;
using Seamless.Execution.Growth;
using Seamless.Collections.Tsavorite;
using CommonStore=Seamless.Execution.Store;

namespace Execution.Tests;

public sealed class StoreProfiles{
    [Fact]
    public void MinimumStartsSmallAndGrowthIsIndependent(){
        foreach(var policy in new Policy[]{new Policy<Fixed>(),new Policy<Doubling>(),new Policy<Estimated>()}){
            using var store=CommonStore.CreateMemory(options:new(){Growth=policy});
            using var data=store.CreateTsavoriteHashSet<int>("items",expectedCount:1_000_000);
            Assert.Equal(64,data.Store.IndexSize*64);
            Assert.Equal(8192,data.LogMemorySize);
            using var items=data.Create();
            for(var i=0;i<200;i++){Assert.True(items.Add(i));}
            for(var i=0;i<200;i++){Assert.True(items.Contains(i));}
            Assert.Equal(Profile.Minimum,store.ResolvedProfiles["items"].Applied);
            if(policy.Descriptor.ID=="fixed"){
                Assert.Equal(64,data.Store.IndexSize*64);
            }else{
                Assert.True(data.Store.IndexSize*64>64);
                Assert.NotEmpty(store.GrowthEvents);
            }
        }
    }
}
