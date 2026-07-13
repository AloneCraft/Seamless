namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkImprovementPromptBuilder{
    [Fact]
    public void 概要を作成_左右シェアの算術平均と件数を計算する(){
        var rows = new[]{
            Row("A",global::UI.シェア値.左シェアから作成(0.2),global::UI.シェア値.左シェアから作成(0.8)),
            Row("B",global::UI.シェア値.左シェアから作成(0.8),global::UI.シェア値.欠測),
            Row("C",global::UI.シェア値.欠測,global::UI.シェア値.左シェアから作成(0.6)),
            Row("D",global::UI.シェア値.欠測,global::UI.シェア値.欠測),
        };

        var summary=global::UI.BenchmarkImprovementPromptBuilder.概要を作成(rows);

        Assert.Equal(0.5,summary.時間左平均!.Value,6);
        Assert.Equal(0.7,summary.メモリ左平均!.Value,6);
        Assert.Equal(2,summary.時間対象数);
        Assert.Equal(2,summary.時間除外数);
        Assert.Equal(2,summary.メモリ対象数);
        Assert.Equal(2,summary.メモリ除外数);
        Assert.Equal("時間シェア 0.50:0.50 (対象 2 / 除外 2) / メモリシェア 0.70:0.30 (対象 2 / 除外 2)",summary.DisplayText);
    }

    [Fact]
    public void CreateSummary_HandlesEmptyRows(){
        var summary=global::UI.BenchmarkImprovementPromptBuilder.概要を作成([]);

        Assert.Null(summary.時間左平均);
        Assert.Null(summary.メモリ左平均);
        Assert.Equal(0,summary.時間対象数);
        Assert.Equal(0,summary.時間除外数);
        Assert.Equal(0,summary.メモリ対象数);
        Assert.Equal(0,summary.メモリ除外数);
        Assert.Equal("全体 時間シェア - (対象 0 / 除外 0) / メモリシェア - (対象 0 / 除外 0)",summary.OverallDisplayText);
    }

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
        Assert.Equal(0.75,candidates[0].時間シェア.左シェア!.Value,6);
        Assert.Equal("Any_predicate",candidates[1].MethodName);
    }

    [Fact]
    public void BuildCandidates_KeepsAllocatedRegressionSeparateFromTimeWin(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var state = State(
            Job(sourceTab.LeftProject,"Where_predicate",64,80,500),
            Job(sourceTab.RightProject,"Where_predicate",64,100,100));

        var candidate = Assert.Single(global::UI.BenchmarkImprovementCandidateBuilder.BuildCandidates(state));

        Assert.False(candidate.時間回帰か);
        Assert.True(candidate.メモリ回帰か);
        Assert.Equal(5.0/6.0,candidate.メモリシェア.左シェア!.Value,6);
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
        Assert.Equal(6.0/7.0,candidates[0].メモリシェア.左シェア!.Value,6);
        Assert.Equal("Aggregate_time",candidates[1].MethodName);
        Assert.Equal(5.0/7.0,candidates[1].時間シェア.左シェア!.Value,6);
    }

    [Fact]
    public void BuildPrompt_IncludesAllRegressionCandidatesWithoutTopTenLimit(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var jobs = new List<global::UI.BenchmarkJob>();
        for(var index=0;index<12;index++){
            var method = "Aggregate_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            jobs.Add(Job(sourceTab.LeftProject,method,64,200+index,200+index));
            jobs.Add(Job(sourceTab.RightProject,method,64,100,100));
        }
        var prompt=global::UI.BenchmarkImprovementPromptBuilder.BuildPrompt(State(jobs.ToArray()));

        for(var index=0;index<12;index++){
            Assert.Contains("Aggregate_"+index.ToString(global::System.Globalization.CultureInfo.InvariantCulture),prompt);
        }
    }

    [Fact]
    public void BuildPrompt_UsesAllCandidatesAndCategorySections(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var state = State(
            Job(sourceTab.LeftProject,"Aggregate_sum",64,300,100),
            Job(sourceTab.RightProject,"Aggregate_sum",64,100,100),
            Job(sourceTab.LeftProject,"Any_predicate",64,200,100),
            Job(sourceTab.RightProject,"Any_predicate",64,100,100));
        var prompt = global::UI.BenchmarkImprovementPromptBuilder.BuildPrompt(state);

        Assert.Contains("## Aggregate",prompt);
        Assert.Contains("Aggregate_sum",prompt);
        Assert.Contains("検索ヒント",prompt);
        Assert.Contains("単体テスト",prompt);
        Assert.Contains("ベンチマーク",prompt);
        Assert.Contains("Any_predicate",prompt);
        Assert.Contains("一般化できない特殊化",prompt);
        Assert.Contains("Benchmark実行の検出",prompt);
        Assert.Contains("列挙順、遅延実行、例外、破棄、デリゲートの副作用",prompt);
        Assert.Contains("MeanNanosecondsとAllocatedBytesを同時に比較",prompt);
        Assert.Contains("異なるデータ分布",prompt);
        Assert.Contains("反復測定",prompt);
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

    private static global::UI.BenchmarkComparisonRow Row(string benchmark,global::UI.シェア値 timeShare,global::UI.シェア値 memoryShare){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var definition = new global::UI.ComparisonPairDefinition(
            sourceTab.Id,
            sourceTab.DisplayName,
            sourceTab.DisplayName,
            sourceTab.LeftProject,
            sourceTab.RightProject);
        return new global::UI.BenchmarkComparisonRow(
            benchmark,
            sourceTab,
            global::UI.ProjectMetrics.Missing,
            global::UI.ProjectMetrics.Missing){
            左右シェア=new global::UI.シェア比較(
                definition,
                global::UI.ProjectMetrics.Missing,
                global::UI.ProjectMetrics.Missing,
                timeShare,
                memoryShare),
        };
    }
}
