namespace Benchmark.Infrastructure.Tests;

public sealed class Comparison{
    [Fact]
    public void DifferentStorageIsNotAPerformanceComparison(){
        Conditions Read(string storage){
            var original=new Conditions("sum","input",100,"Int32","Tsavorite","Temporary","Native","Native","{}","{}","host");
            var json=System.Text.Json.JsonSerializer.SerializeToNode(original)!;
            json["StorageKey"]=storage;
            return System.Text.Json.JsonSerializer.Deserialize<Conditions>(json.ToJsonString())!;
        }
        var before=Measurement.Create("before","run",Read("volume-ssd"),"same",Enumerable.Repeat(new Sample(100,1,0),7).ToArray());
        var after=before with{ID="after",Conditions=Read("volume-hdd")};
        Assert.Equal(ComparisonStatus.Incompatible,Infrastructure.Comparison.Compare(before,after,ComparisonMode.Experiment).Status);
        var legacy=before with{Conditions=Read("Unknown")};
        Assert.Equal(ComparisonStatus.Incompatible,Infrastructure.Comparison.Compare(legacy,legacy).Status);
    }
    [Fact]
    public void Compare(){
        var conditions=new Conditions("sum","input",100,"Int32","Tsavorite","Memory","FLinq","All","{}","{}","environment");
        Assert.Equal("9920D3FA72013C2393FA67D929CB95273F576F9D34D6D62591207B1F42FE9E2C",conditions.ScopeKey);
        var baseline=Measurement.Create("a","run",conditions,"digest",Enumerable.Repeat(new Sample(100,1,0),7).ToArray());
        var candidate=baseline with{ID="b",Samples=Enumerable.Repeat(new Sample(110,1,0),7).ToArray()};
        Assert.Equal(ComparisonStatus.Regressed,Infrastructure.Comparison.Compare(baseline,candidate).Status);
        Assert.Equal(ComparisonStatus.Incompatible,Infrastructure.Comparison.Compare(baseline,candidate with{Conditions=conditions with{ParametersJSON="{\"Index\":128}"}}).Status);
        Assert.Equal(ComparisonStatus.Regressed,Infrastructure.Comparison.Compare(baseline,candidate with{Conditions=conditions with{ParametersJSON="{\"Index\":128}"}},ComparisonMode.Experiment).Status);
        Assert.Equal(ComparisonStatus.CorrectnessFailure,Infrastructure.Comparison.Compare(baseline,candidate with{Digest="wrong"}).Status);
        Assert.Equal(ComparisonStatus.Incomplete,Infrastructure.Comparison.Compare(baseline,candidate with{Samples=[]}).Status);
        Assert.Equal(ComparisonStatus.Incompatible,Infrastructure.Comparison.Compare(baseline,candidate with{Conditions=conditions with{IO="alternate-io"}}).Status);
        Assert.Equal(ComparisonStatus.Incompatible,Infrastructure.Comparison.Compare(baseline,candidate with{Conditions=conditions with{InputHash="other"}},ComparisonMode.Experiment).Status);
        Assert.Equal(ComparisonStatus.Regressed,Infrastructure.Comparison.Compare(baseline,candidate with{Conditions=conditions with{Implementation="Ideal"}},ComparisonMode.Experiment).Status);
    }
    [Fact]
    public void CapacityAndFunctionalOutcomes(){
        var conditions=new Conditions("capacity","input",128,"Int32","Tsavorite","Memory","Capacity","Native","{}","{}","environment","capacity-sweep-v1");
        var baseline=Measurement.Create("a","run",conditions,"same",[]) with{Capacity=new(100,"MemoryFull")};
        Assert.Equal(ComparisonStatus.Passed,Infrastructure.Comparison.Compare(baseline,baseline with{Capacity=new(110,"MemoryFull")}).Status);
        Assert.Equal(ComparisonStatus.Regressed,Infrastructure.Comparison.Compare(baseline,baseline with{Capacity=new(90,"MemoryFull")}).Status);
        Assert.Equal(ComparisonStatus.Inconclusive,Infrastructure.Comparison.Compare(baseline,baseline with{Capacity=new(100,"LogFull")}).Status);
        Assert.Equal(ComparisonStatus.Incomplete,Infrastructure.Comparison.Compare(baseline,baseline with{Capacity=null}).Status);
        var functional=baseline with{Conditions=conditions with{MeasurementVersion="test-outcome-v1"},Capacity=null};
        Assert.Equal(ComparisonStatus.Passed,Infrastructure.Comparison.Compare(functional,functional).Status);
    }
}
