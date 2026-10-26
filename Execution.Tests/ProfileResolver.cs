using Seamless.Execution;
using Target=Seamless.Execution.ProfileResolver;

namespace Execution.Tests;

public sealed class ProfileResolver{
    [Fact]
    public void StoreMatchesTheActualVolumeAndRejectsUnknownDiskEvidence(){
        var root=Path.GetTempPath();
        var key=ProfileEnvironment.Storage(root);
        Assert.Equal(key,ProfileEnvironment.Storage(Path.Combine(root,"not-created-"+Guid.NewGuid().ToString("N"))));
        var request=new ProfileRequest("Tsavorite",StoreKind.Temporary,"Int32",1,StorageKey:key);
        var measured=new MeasuredProfile("measured-volume",1,request,1,new(){IndexBytes=4096},["training","validation"],true,Evidence:ProfileEvidence.Current);
        var catalog=new ProfileCatalog([measured with{ID="wrong-volume",Conditions=request with{StorageKey="other-volume"}},measured]);
        var options=new StoreOptions{Profile=Profile.Optimized,Profiles=catalog};
        using var store=Seamless.Execution.Store.CreateTemporary(temporaryDirectory:root,options:options);
        Assert.Equal("measured-volume",store.ResolveProfile("data","Tsavorite","Int32",1).ProfileID);
        Assert.Null(new ProfileCatalog([measured with{Conditions=request with{StorageKey="Unknown"}}]).Find(request));
        Assert.Null(new ProfileCatalog([measured with{Conditions=request with{StorageKey="Unknown"}}]).Find(request with{StorageKey="Unknown"}));
        var unknown=Target.Resolve(Profile.Optimized,request with{StorageKey="Unknown"},new(),supplied:catalog);
        Assert.Equal(Profile.Minimum,unknown.Applied);Assert.Contains("Storage volume is unknown",unknown.Reason);
        Assert.Equal("Memory",ProfileEnvironment.Storage(null));
    }
    [Fact]
    public void BundledEvidenceIsNarrowAndCarriesGrowth(){
        var profile=Assert.Single(ProfileCatalog.Bundled.Profiles,static value=>value.ID=="tsavorite-file-int32-filter-4096-doubling-v1");
        var result=Target.Resolve(Profile.Optimized,profile.Conditions,new());
        Assert.Equal(Profile.Minimum,result.Applied);Assert.Contains("Storage volume is unknown",result.Reason);
        Assert.Equal("doubling",profile.Growth!.ID);
        Assert.Equal(4,profile.EvidenceRunIDs.Length);
        Assert.Equal(Profile.Minimum,Target.Resolve(Profile.Optimized,profile.Conditions with{ElementType="other"},new()).Applied);
        Assert.Equal(Profile.Minimum,Target.Resolve(Profile.Optimized,profile.Conditions with{IO="other-io"},new()).Applied);
        var unknown=profile with{Conditions=profile.Conditions with{StorageKey="test-volume"},Growth=new("user-defined",1,"{}"),Evidence=ProfileEvidence.Current};
        var catalog=new ProfileCatalog([unknown]);
        var bundled=Target.Resolve(Profile.Optimized,unknown.Conditions,new(),supplied:catalog);
        Assert.Equal(Profile.Minimum,bundled.Applied);Assert.Contains("unavailable",bundled.Reason);
        Assert.Equal(Profile.Optimized,Target.Resolve(Profile.Optimized,unknown.Conditions,new(),supplied:catalog,explicitGrowth:new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Doubling>()).Applied);
    }
    [Fact]
    public void Resolve(){
        var request=new ProfileRequest("Tsavorite",StoreKind.Temporary,"Int32",1_000_000,StorageKey:"test-volume");
        var minimum=Target.Resolve(Profile.Minimum,request,new());
        Assert.Equal(64,minimum.Settings.IndexBytes);
        Assert.Equal("Minimum",minimum.ProfileID);Assert.Equal(Target.MinimumVersion,minimum.ProfileVersion);
        Assert.Equal(8192,minimum.Settings.LogMemoryBytes);
        var fallback=Target.Resolve(Profile.Optimized,request,new());
        Assert.Equal(Profile.Minimum,fallback.Applied);
        Assert.NotEmpty(fallback.Reason);

        var measured=new MeasuredProfile("measured",1,request with{ElementCount=0},2_000_000,
            new(){IndexBytes=512,LogMemoryBytes=65536},["training","validation"],true,Evidence:ProfileEvidence.Current);
        var catalog=new ProfileCatalog([measured]);
        var optimized=Target.Resolve(Profile.Optimized,request,new(),new(){IndexBytes=128},catalog);
        Assert.Equal(Profile.Optimized,optimized.Applied);
        Assert.Equal(128,optimized.Settings.IndexBytes);
        Assert.Equal(65536,optimized.Settings.LogMemoryBytes);
        Assert.Equal(Profile.Minimum,Target.Resolve(Profile.Optimized,request,new(){MemoryBytes=16384},supplied:catalog).Applied);
        Assert.Throws<ArgumentException>(()=>Target.Resolve(Profile.Minimum,request,new(),new(){IndexBytes=65}));
        var alternatives=new ProfileCatalog([
            measured with{ID="large",Version=2,Settings=new(){IndexBytes=1<<20,LogMemoryBytes=8192}},
            measured with{ID="small",Settings=new(){IndexBytes=512,LogMemoryBytes=8192}}
        ]);
        var withinBudget=Target.Resolve(Profile.Optimized,request,new(){MemoryBytes=16384},supplied:alternatives);
        Assert.Equal("small",withinBudget.ProfileID);Assert.Contains("resource budget",withinBudget.Reason);
        Assert.Equal("measured",ProfileCatalog.FromJSON(catalog.ToJSON()).Profiles[0].ID);
    }
}
