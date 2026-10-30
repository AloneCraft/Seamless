namespace Benchmark.Infrastructure.Tests;

public sealed class ComparisonPolicy{
    [Fact]public void Compare(){
        var conditions=new Conditions("sum","input",100,"Int32","Tsavorite","Memory","FLinq","All","{}","{}","environment");
        Measurement Make(params double[] times)=>Measurement.Create("a","run",conditions,"digest",times.Select(time=>new Sample(time,1,0)).ToArray());
        var stable=Make(100,100,100,100,100,100,100);
        var noisy=Make(1,10,50,100,200,1000,10000);
        Assert.Equal(ComparisonStatus.Inconclusive,Infrastructure.Comparison.Compare(stable,noisy).Status);
        Assert.Equal(ComparisonStatus.CorrectnessFailure,Infrastructure.Comparison.Compare(stable,noisy with{Digest="wrong"}).Status);
        Assert.Equal(ComparisonStatus.Incomplete,Infrastructure.Comparison.Compare(stable,Make(100,100,100,100,100,100,double.NaN)).Status);
        Assert.Equal(ComparisonStatus.Inconclusive,Infrastructure.Comparison.Compare(Make(0,0,0,0,0,0,0),stable).Status);
        Assert.Equal(ComparisonStatus.Passed,Infrastructure.Comparison.Compare(stable,Make(101,101,101,101,101,101,101)).Status);
        Assert.Equal(ComparisonStatus.Regressed,Infrastructure.Comparison.Compare(stable,Make(120,120,120,120,120,120,120)).Status);
        Assert.Equal("noise-v1",Infrastructure.Comparison.Compare(stable,stable).PolicyVersion);
        var current=System.Text.Json.JsonSerializer.Serialize(new{Evidence=Seamless.Execution.ProfileEvidence.Current});
        Assert.Equal(ComparisonStatus.Incompatible,Infrastructure.Comparison.Compare(stable,stable with{Conditions=conditions with{ParametersJSON=current}},ComparisonMode.Experiment).Status);
    }
}
