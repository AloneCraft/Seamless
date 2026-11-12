using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

public sealed class RegressionLifetimeTests{
    [Fact]
    public void 独自型をassertionで比較しても回帰アセンブリを解放できる(){
        const string source="""
            public sealed class LifetimeCase{
                private sealed record Row(int Value);
                [Fact]public void Compare(){Assert.Equal(new[]{new Row(1)},new[]{new Row(1)});}
            }
            """;
        using var suite=RegressionSuite.Compile(new("lifetime",[new("Lifetime.cs",source)]));
        var result=suite.Run(Assert.Single(suite.Cases).ID);
        Assert.Equal("Passed",result.Status);Assert.True(suite.AssertionsAreCollectible);
    }
}
