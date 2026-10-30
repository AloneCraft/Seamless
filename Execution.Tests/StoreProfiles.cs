using Seamless.Execution;
using Seamless.Execution.Growth;
using Seamless.Collections.Tsavorite;
using CommonStore=Seamless.Execution.Store;

namespace Execution.Tests;

public sealed class StoreProfiles{
    [Theory]
    [InlineData("fixed",512)]
    [InlineData("doubling",8192)]
    public void DiskEnumerationPreservesCollidingKeys(string growth,int count){
        Policy policy=new Policy<Doubling>();
        if(growth=="fixed"){policy=new Policy<Fixed>();}
        using var owner=CommonStore.CreateTemporary(limits:new(){MemoryBytes=8388608},options:new(){Growth=policy});
        using var data=owner.CreateTsavorite<int,int>("dictionary");
        using var values=data.Create();
        for(var index=0;index<count;index++){values[index]=index^42;}
        var actual=values.ToArray();
        Assert.Equal(count,actual.Length);
        Assert.Equal(count,actual.Select(pair=>pair.Key).Distinct().Count());
        Assert.All(actual,pair=>Assert.Equal(pair.Key^42,pair.Value));
        for(var index=0;index<count;index+=3){values[index]=-index;}
        for(var index=1;index<count;index+=3){Assert.True(values.Remove(index));}
        actual=values.ToArray();
        Assert.Equal(values.Count,actual.Length);
        Assert.Equal(actual.Length,actual.Select(pair=>pair.Key).Distinct().Count());
        Assert.All(actual,pair=>{
            Assert.NotEqual(1,pair.Key%3);
            var expected=pair.Key^42;
            if(pair.Key%3==0){expected=-pair.Key;}
            Assert.Equal(expected,pair.Value);
        });
        Assert.Equal(actual.Length,data.MeasureLogUsage().LiveRecords);
    }
    [Fact]
    public void IndexGrowthAccountsForBothIndexesDuringTransition(){
        using var owner=CommonStore.CreateTemporary(limits:new(){MemoryBytes=20000},options:new(){Growth=new Policy<Estimated>()});
        using var data=owner.CreateTsavorite<int,int>("dictionary",expectedCount:100000);
        using var values=data.Create();
        for(var index=0;index<200;index++){values.Add(index,index);}
        for(var index=0;index<200;index++){Assert.Equal(index,values[index]);}
        Assert.True(owner.Budget.PeakMemoryBytes>owner.Budget.MemoryBytes);
        Assert.True(owner.Budget.PeakMemoryBytes<=owner.Limits.MemoryBytes);
        Assert.Equal(data.Store.IndexSize*64,owner.GetCurrentSettings("dictionary").IndexBytes);
    }
    [Fact]
    public void MemoryLogGrowsWithoutLosingDataAndRespectsBudget(){
        using(var owner=global::Seamless.Execution.Store.CreateMemory()){
            using var data=owner.CreateTsavorite<int,int>("dictionary");
            Assert.Equal(8192,data.LogMemorySize);
            Assert.Equal(8192,data.Store.Log.MemorySizeBytes);
            using var items=data.Create();
            for(var index=0;index<4096;index++){items.Add(index,index*3);}
            Assert.True(data.LogMemorySize>8192);
            for(var index=0;index<4096;index++){Assert.Equal(index*3,items[index]);}
            Assert.Contains(owner.GrowthEvents,entry=>entry.Context.Resource==Seamless.Execution.Growth.Resource.LogMemory);
            Assert.All(owner.GrowthEvents,entry=>Assert.True(entry.ElapsedNanoseconds>=0));
            Assert.Equal(data.LogMemorySize,owner.GetCurrentSettings("dictionary").LogMemoryBytes);
            Assert.Equal(data.Store.IndexSize*64,owner.GetCurrentSettings("dictionary").IndexBytes);
            Assert.True(owner.Budget.MemoryBytes<=owner.Limits.MemoryBytes);
        }
        using(var owner=global::Seamless.Execution.Store.CreateMemory(limits:new(){MemoryBytes=32768})){
            using var data=owner.CreateTsavorite<int,int>("dictionary");using var items=data.Create();var inserted=0;
            Assert.Throws<OutOfMemoryException>(()=>{for(;inserted<4096;inserted++){items.Add(inserted,inserted);}});
            Assert.True(inserted>0);
            for(var index=0;index<inserted;index++){Assert.Equal(index,items[index]);}
            Assert.True(owner.Budget.MemoryBytes<=32768);
            Assert.Equal(data.LogMemorySize,owner.GetCurrentSettings("dictionary").LogMemoryBytes);
            Assert.Equal(data.Store.IndexSize*64,owner.GetCurrentSettings("dictionary").IndexBytes);
        }
    }
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
                if(policy.Descriptor.ID=="estimated"){Assert.Contains(store.GrowthEvents,entry=>entry.Context.ExpectedCount==1_000_000);}
            }
        }
    }
}
