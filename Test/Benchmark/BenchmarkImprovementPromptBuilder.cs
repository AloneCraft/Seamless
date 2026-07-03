namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkImprovementPromptBuilder{
    [Fact]
    public void BuildCandidates_OrdersTimeRegressionByLargestRatio(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var state = State(
            Job(sourceTab.LeftProject,"Aggregate_sum",64,300,100),
            Job(sourceTab.RightProject,"Aggregate_sum",64,100,100),
            Job(sourceTab.LeftProject,"Any_predicate",64,120,100),
            Job(sourceTab.RightProject,"Any_predicate",64,100,100));

        var candidates = global::UI.BenchmarkImprovementCandidateBuilder.BuildCandidates(state);

        Assert.Equal("Aggregate_sum",candidates[0].MethodName);
        Assert.Equal(3,candidates[0].TimeRatio.NumericValue);
        Assert.Equal("Any_predicate",candidates[1].MethodName);
    }

    [Fact]
    public void BuildCandidates_KeepsAllocatedRegressionSeparateFromTimeWin(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var state = State(
            Job(sourceTab.LeftProject,"Where_predicate",64,80,500),
            Job(sourceTab.RightProject,"Where_predicate",64,100,100));

        var candidate = Assert.Single(global::UI.BenchmarkImprovementCandidateBuilder.BuildCandidates(state));

        Assert.False(candidate.HasTimeRegression);
        Assert.True(candidate.HasAllocatedRegression);
        Assert.Equal(5,candidate.AllocatedRatio.NumericValue);
    }

    [Fact]
    public void BuildCandidates_OrdersByWorstLeftRatioAcrossTimeAndAllocated(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var state = State(
            Job(sourceTab.LeftProject,"Aggregate_time",64,250,100),
            Job(sourceTab.RightProject,"Aggregate_time",64,100,100),
            Job(sourceTab.LeftProject,"Where_allocated",64,100,600),
            Job(sourceTab.RightProject,"Where_allocated",64,100,100));

        var candidates = global::UI.BenchmarkImprovementCandidateBuilder.BuildCandidates(state);

        Assert.Equal("Where_allocated",candidates[0].MethodName);
        Assert.Equal(6,candidates[0].AllocatedRatio.NumericValue);
        Assert.Equal("Aggregate_time",candidates[1].MethodName);
        Assert.Equal(2.5,candidates[1].TimeRatio.NumericValue);
    }

    [Fact]
    public void CreateDefaultSelectedKeys_SelectsTopTenForEachBucketAndMergesDuplicates(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var jobs = new List<global::UI.BenchmarkJob>();
        for(var index=0;index<12;index++){
            var method = "Aggregate_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            jobs.Add(Job(sourceTab.LeftProject,method,64,200+index,200+index));
            jobs.Add(Job(sourceTab.RightProject,method,64,100,100));
        }
        var candidates = global::UI.BenchmarkImprovementCandidateBuilder.BuildCandidates(State(jobs.ToArray()));

        var selected = global::UI.BenchmarkImprovementCandidateBuilder.CreateDefaultSelectedKeys(candidates,10);

        Assert.Equal(10,selected.Count);
        Assert.Contains(candidates.First(static item => item.MethodName=="Aggregate_11").Key,selected);
        Assert.DoesNotContain(candidates.First(static item => item.MethodName=="Aggregate_0").Key,selected);
    }

    [Fact]
    public void BuildPrompt_UsesSelectedCandidatesAndCategorySections(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var state = State(
            Job(sourceTab.LeftProject,"Aggregate_sum",64,300,100),
            Job(sourceTab.RightProject,"Aggregate_sum",64,100,100),
            Job(sourceTab.LeftProject,"Any_predicate",64,200,100),
            Job(sourceTab.RightProject,"Any_predicate",64,100,100));
        var candidates = global::UI.BenchmarkImprovementCandidateBuilder.BuildCandidates(state);
        var selected = candidates.Where(static item => item.MethodName=="Aggregate_sum");

        var prompt = global::UI.BenchmarkImprovementPromptBuilder.BuildPrompt(selected);

        Assert.Contains("## Aggregate",prompt);
        Assert.Contains("Aggregate_sum",prompt);
        Assert.Contains("検索ヒント",prompt);
        Assert.Contains("単体テスト",prompt);
        Assert.Contains("ベンチマーク",prompt);
        Assert.DoesNotContain("Any_predicate",prompt);
    }

    private static global::UI.ProgressState State(params global::UI.BenchmarkJob[] jobs) =>
        new(){
            RunId="run",
            SourceTab=global::UI.BenchmarkSourceTab.SystemArray,
            Jobs=jobs.ToList(),
        };

    private static global::UI.BenchmarkJob Job(
        string project,
        string method,
        int elementCount,
        double meanNanoseconds,
        long allocatedBytes) =>
        new(){
            Project=project,
            AssemblyName="Benchmarks",
            TypeFullName="Sample.Type",
            MethodName=method,
            SourceTab=global::UI.BenchmarkSourceTabs.TryGetByProject(project)?.Tab??global::UI.BenchmarkSourceTab.SystemArray,
            ElementCount=elementCount,
            Status=global::UI.JobStatus.Completed,
            MeanNanoseconds=meanNanoseconds,
            AllocatedBytes=allocatedBytes,
            ExitCode=0,
        };
}
