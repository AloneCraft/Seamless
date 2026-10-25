using Seamless.Execution;
using Target=Seamless.Execution.ProfileResolver;

namespace Execution.Tests;

public sealed class ProfileResolver{
    [Fact]
    public void BundledEvidenceIsNarrowAndCarriesGrowth(){
        var profile=Assert.Single(ProfileCatalog.Bundled.Profiles,static value=>value.ID=="tsavorite-file-int32-filter-4096-v1");
        var result=Target.Resolve(Profile.Optimized,profile.Conditions,new());
        Assert.Equal(Profile.Optimized,result.Applied);Assert.Equal("fixed",result.Growth!.ID);
        Assert.Equal(4,result.EvidenceRunIDs.Length);
        Assert.Equal(Profile.Minimum,Target.Resolve(Profile.Optimized,profile.Conditions with{ElementType="other"},new()).Applied);
        var unknown=profile with{Growth=new("user-defined",1,"{}")};
        var catalog=new ProfileCatalog([unknown]);
        Assert.Equal(Profile.Minimum,Target.Resolve(Profile.Optimized,profile.Conditions,new(),supplied:catalog).Applied);
        Assert.Equal(Profile.Optimized,Target.Resolve(Profile.Optimized,profile.Conditions,new(),supplied:catalog,explicitGrowth:new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Doubling>()).Applied);
    }
    [Fact]
    public void Resolve(){
        var request=new ProfileRequest("Tsavorite",StoreKind.Temporary,"Int32",1_000_000);
        var minimum=Target.Resolve(Profile.Minimum,request,new());
        Assert.Equal(64,minimum.Settings.IndexBytes);
        Assert.Equal(8192,minimum.Settings.LogMemoryBytes);
        var fallback=Target.Resolve(Profile.Optimized,request,new());
        Assert.Equal(Profile.Minimum,fallback.Applied);
        Assert.NotEmpty(fallback.Reason);

        var measured=new MeasuredProfile("measured",1,request with{ElementCount=0},2_000_000,
            new(){IndexBytes=512,LogMemoryBytes=65536},["training","validation"],true);
        var catalog=new ProfileCatalog([measured]);
        var optimized=Target.Resolve(Profile.Optimized,request,new(),new(){IndexBytes=128},catalog);
        Assert.Equal(Profile.Optimized,optimized.Applied);
        Assert.Equal(128,optimized.Settings.IndexBytes);
        Assert.Equal(65536,optimized.Settings.LogMemoryBytes);
        Assert.Equal(Profile.Minimum,Target.Resolve(Profile.Optimized,request,new(){MemoryBytes=16384},supplied:catalog).Applied);
        Assert.Throws<ArgumentException>(()=>Target.Resolve(Profile.Minimum,request,new(),new(){IndexBytes=65}));
        Assert.Equal("measured",ProfileCatalog.FromJSON(catalog.ToJSON()).Profiles[0].ID);
    }
}
