using Benchmark.Scalable.Core;
using Seamless.Execution;
using Xunit;

namespace Benchmark.Scalable.Tests.Requests;

public sealed class ScalableRequest{
    [Fact]public void Explain(){
        var request=new Core.ScalableRequest(Backend:"Persisted",Workload:Workload.Sort,Implementation:ScalableImplementation.Scoped,
            Profile:Profile.Optimized,ExplicitSettings:new(){InitialCapacity=32},Growth:"fixed");
        var explanation=request.Explain("unknown");
        Assert.Equal(Profile.Minimum,explanation.Profile.Applied);
        Assert.Equal(32,explanation.Profile.Settings.InitialCapacity);
        Assert.Equal("fixed",explanation.Profile.Growth.ID);Assert.Equal("Scoped",explanation.Implementation);
        Assert.NotEmpty(explanation.RuntimeDecisions);
        Assert.Equal("Memory",Assert.IsType<ProfileRequest>(explanation.Profile.Conditions).StorageKey);
        var fileRequest=request with{Kind=StoreKind.Temporary,TemporaryDirectory=Path.GetTempPath()};
        Assert.Equal(ProfileEnvironment.Storage(Path.GetTempPath()),Assert.IsType<ProfileRequest>(fileRequest.Explain("unknown").Profile.Conditions).StorageKey);
    }
}
