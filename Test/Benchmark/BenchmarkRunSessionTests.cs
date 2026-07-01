namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkRunSessionTests{
    [Fact]
    public void UpsertResult_ReplacesSameResultKey(){
        var session = new global::UI.BenchmarkRunSession();

        session.UpsertResult(Result("System.Array.FLinq","Aggregate",64,100));
        session.UpsertResult(Result("System.Array.FLinq","Aggregate",64,250));

        var result = Assert.Single(session.GetResults(global::UI.BenchmarkSourceTab.SystemArray));
        Assert.Equal(1,session.Count);
        Assert.Equal(250,result.MeanNanoseconds);
    }

    [Fact]
    public void GetResults_SeparatesSourceTabs(){
        var session = new global::UI.BenchmarkRunSession();

        session.UpsertResult(Result("System.Array.FLinq","Aggregate",64,100));
        session.UpsertResult(Result("System.Range.FLinq","Aggregate",64,200));

        var arrayResult = Assert.Single(session.GetResults(global::UI.BenchmarkSourceTab.SystemArray));
        var rangeResult = Assert.Single(session.GetResults(global::UI.BenchmarkSourceTab.SystemRange));
        Assert.Equal("System.Array.FLinq",arrayResult.Project);
        Assert.Equal("System.Range.FLinq",rangeResult.Project);
    }

    [Fact]
    public void GetElementCounts_ReturnsDistinctSortedCountsForSourceTab(){
        var session = new global::UI.BenchmarkRunSession();

        session.UpsertResult(Result("System.Array.FLinq","Aggregate",64,100));
        session.UpsertResult(Result("System.Array.ZLinq","Aggregate",16,200));
        session.UpsertResult(Result("System.Range.FLinq","Aggregate",4,300));

        Assert.Equal([16,64],session.GetElementCounts(global::UI.BenchmarkSourceTab.SystemArray));
    }

    [Fact]
    public void ToProgressState_CreatesJobsAndSelectionSnapshot(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var session = new global::UI.BenchmarkRunSession();
        session.UpsertResult(Result("System.Array.FLinq","Aggregate",64,100,"Array.Type","Count=64",[new global::UI.BenchmarkArgument("Count","64")]));

        var state = session.ToProgressState(
            "run-42",
            global::UI.BenchmarkExecutionProfile.Fast,
            sourceTab,
            TimeSpan.FromMilliseconds(1),
            new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase){
                ["SystemArray|row|left"]=true,
            },
            new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase){
                ["SystemArray|row"]=true,
            },
            result => new global::UI.BenchmarkCase(result.Project,"Array.Assembly",result.TypeFullName,result.Method,result.ArgumentKey,result.Arguments));

        var job = Assert.Single(state.Jobs);
        Assert.Equal(global::UI.BenchmarkExecutionProfile.Fast,state.ExecutionProfile);
        Assert.Equal(global::UI.BenchmarkSourceTab.SystemArray,state.SourceTab);
        Assert.Equal([64],state.ElementCounts);
        Assert.Equal("Array.Assembly",job.AssemblyName);
        Assert.Equal("Array.Type",job.TypeFullName);
        Assert.Equal("Count=64",job.ArgumentKey);
        Assert.True(state.BenchmarkSelection.ProjectCellSelections["SystemArray|row|left"]);
        Assert.True(state.BenchmarkSelection.RowSelections["SystemArray|row"]);
    }

    private static global::UI.BenchmarkResultItem Result(
        string project,
        string method,
        int elementCount,
        double mean,
        string typeFullName = "Sample.Type",
        string argumentKey = "",
        IReadOnlyList<global::UI.BenchmarkArgument>? arguments = null) =>
        new(
            project,
            method,
            global::UI.ProjectMetrics.FromMeasurement(mean,128),
            elementCount,
            typeFullName,
            argumentKey,
            arguments){
        };
}
