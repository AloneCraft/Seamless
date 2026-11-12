using Seamless.Execution;

namespace Execution.Tests;

public sealed class ProfileExplanation{
    [Fact]public void Explain(){
        var request=new ProfileRequest("Tsavorite",StoreKind.Memory,"Int32",10);
        var evidence=new[]{"training","validation"};
        var profile=new MeasuredProfile("known",1,request,10,new(){IndexBytes=128},evidence,true,Evidence:ProfileEvidence.Current);
        var explanation=Seamless.Execution.ProfileResolver.Explain(Profile.Optimized,request,new(),new(){IndexBytes=256},new([profile]));
        Assert.Equal(Profile.Optimized,explanation.Applied);Assert.Equal(256,explanation.Settings.IndexBytes);
        Assert.Equal("doubling",explanation.Growth.ID);Assert.NotEmpty(explanation.UnresolvedConditions);
        evidence[0]="changed";Assert.Equal("training",explanation.EvidenceRunIDs[0]);
        var legacy=profile with{Evidence=null};
        var fallback=Seamless.Execution.ProfileResolver.Explain(Profile.Optimized,request,new(),supplied:new([legacy]));
        Assert.Equal(Profile.Minimum,fallback.Applied);Assert.Contains("versions unknown",fallback.Reason);
        var stale=profile with{Evidence=ProfileEvidence.Current with{CodecVersion="old"}};
        Assert.Equal(Profile.Minimum,Seamless.Execution.ProfileResolver.Explain(Profile.Optimized,request,new(),supplied:new([stale])).Applied);
    }
}
