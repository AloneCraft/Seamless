using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

public sealed class FixtureInputIdentityTests{
    [Theory]
    [InlineData(QueryElementType.Int)]
    [InlineData(QueryElementType.String)]
    [InlineData(QueryElementType.Double)]
    public void IdentityUsesTypedInputAcrossContainers(QueryElementType type){
        using var array=new SourceFixtureFactory().Create(SourceTarget.SystemArray,65,Seed256.Parse("42"),type,true);
        using var list=new SourceFixtureFactory().Create(SourceTarget.SystemList,65,Seed256.Parse("42"),type,true);
        Assert.Equal(Worker.FixtureInputIdentity.Compute(array,type),Worker.FixtureInputIdentity.Compute(list,type));
        using var changed=new SourceFixtureFactory().Create(SourceTarget.SystemArray,66,Seed256.Parse("42"),type,true);
        Assert.NotEqual(Worker.FixtureInputIdentity.Compute(array,type),Worker.FixtureInputIdentity.Compute(changed,type));
    }
}
