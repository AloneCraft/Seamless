using Benchmark;

namespace Test.BenchmarkWorker;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkRunPersistenceServiceTests{
    [Fact]
    public void SaveManagedRun_CreatesNumberedRunAndWritesProgressAndCsv(){
        var root = CreateTempRoot();
        var service = new BenchmarkRunPersistenceService();
        var state = CreateState(Path.Combine(root,".unsaved","run"));

        var option = service.SaveManagedRun(root,BenchmarkExecutionProfile.Fast,state);

        Assert.Equal(1,option.RunNumber);
        Assert.Equal(Path.Combine(root,"Fast","000001"),option.RunRoot);
        Assert.Contains("000001",option.DisplayName,StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(option.RunRoot,BenchmarkStateManager.ProgressFileName)));
        Assert.True(File.Exists(Path.Combine(option.RunRoot,"System.Array","N128.csv")));
        Assert.False(File.Exists(Path.Combine(option.RunRoot,"System.Array","N256.csv")));
        var loaded = service.ReadProgressState(option.RunRoot)!;
        Assert.Equal(BenchmarkExecutionProfile.Fast,loaded.ExecutionProfile);
        Assert.Equal(option.RunRoot,loaded.RunRoot);
        Assert.Equal(1,loaded.RunNumber);
        Assert.Equal(5,loaded.Jobs.Count);
        Assert.Contains(loaded.Jobs,static job => job.Status==JobStatus.Failed);
        Assert.Contains(loaded.Jobs,static job => job.Status==JobStatus.Pending&&job.MethodName=="Select");
        Assert.Equal(2,option.CompletedJobs);
        Assert.Equal(5,option.TotalJobs);
    }

    [Fact]
    public void SaveManagedRun_AlwaysCreatesNewRun(){
        var root = CreateTempRoot();
        var service = new BenchmarkRunPersistenceService();
        var state = CreateState(Path.Combine(root,".unsaved","run"));

        var first = service.SaveManagedRun(root,BenchmarkExecutionProfile.Fast,state);
        var second = service.SaveManagedRun(root,BenchmarkExecutionProfile.Fast,state);

        Assert.Equal(1,first.RunNumber);
        Assert.Equal(2,second.RunNumber);
        Assert.Equal(Path.Combine(root,"Fast","000001"),first.RunRoot);
        Assert.Equal(Path.Combine(root,"Fast","000002"),second.RunRoot);
        Assert.True(File.Exists(Path.Combine(first.RunRoot,BenchmarkStateManager.ProgressFileName)));
        Assert.True(File.Exists(Path.Combine(second.RunRoot,BenchmarkStateManager.ProgressFileName)));
    }

    [Fact]
    public void SaveManagedRun_WritesAllSourceTabsAndBenchmarkSelections(){
        var root = CreateTempRoot();
        var service = new BenchmarkRunPersistenceService();
        var state = CreateState(Path.Combine(root,".unsaved","run"));
        state.Jobs.Add(CompletedJob(BenchmarkSourceTab.SystemList,"System.List.FLinq","Where",128,90));
        state.Jobs.Add(CompletedJob(BenchmarkSourceTab.SystemList,"System.List.ZLinq","Where",128,110));
        state.BenchmarkSelection.ProjectCellSelections["Where|System.Array.FLinq"]=true;
        state.BenchmarkSelection.ProjectCellSelections["Where|System.List.ZLinq"]=false;
        state.BenchmarkSelection.RowSelections["System.Array|Where"]=true;
        state.BenchmarkSelection.RowSelections["System.List|Where"]=false;

        var option = service.SaveManagedRun(root,BenchmarkExecutionProfile.Fast,state);

        Assert.True(File.Exists(Path.Combine(option.RunRoot,"System.Array","N128.csv")));
        Assert.True(File.Exists(Path.Combine(option.RunRoot,"System.List","N128.csv")));
        var loaded = service.ReadProgressState(option.RunRoot)!;
        Assert.Equal(7,loaded.Jobs.Count);
        Assert.True(loaded.BenchmarkSelection.ProjectCellSelections["Where|System.Array.FLinq"]);
        Assert.False(loaded.BenchmarkSelection.ProjectCellSelections["Where|System.List.ZLinq"]);
        Assert.True(loaded.BenchmarkSelection.RowSelections["System.Array|Where"]);
        Assert.False(loaded.BenchmarkSelection.RowSelections["System.List|Where"]);
    }

    [Fact]
    public void CreateSummary_UsesGeometricMeanAndExcludesMissingRatios(){
        var rows = BenchmarkComparisonFormatter.BuildComparisonRows([
            CompletedJob("System.Array.FLinq","Half",128,50,10).ToReportRow(),
            CompletedJob("System.Array.ZLinq","Half",128,100,10).ToReportRow(),
            CompletedJob("System.Array.FLinq","Double",128,200,40).ToReportRow(),
            CompletedJob("System.Array.ZLinq","Double",128,100,10).ToReportRow(),
            CompletedJob("System.Array.FLinq","MissingRight",128,100,10).ToReportRow(),
        ],BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray));

        var summary = BenchmarkImprovementPromptBuilder.CreateSummary(rows);

        Assert.Equal(1.0,summary.TimeGeometricMean!.Value,precision: 12);
        Assert.Equal(2,summary.TimeIncludedCount);
        Assert.Equal(1,summary.TimeExcludedCount);
        Assert.Equal(2.0,summary.AllocatedGeometricMean!.Value,precision: 12);
        Assert.Equal(2,summary.AllocatedIncludedCount);
        Assert.Equal(1,summary.AllocatedExcludedCount);
    }

    [Fact]
    public void SaveManagedRun_WritesImprovementPrompt(){
        var root = CreateTempRoot();
        var service = new BenchmarkRunPersistenceService();
        var state = CreateState(Path.Combine(root,".unsaved","run"));

        var option = service.SaveManagedRun(root,BenchmarkExecutionProfile.Fast,state);

        var promptPath = Path.Combine(option.RunRoot,BenchmarkImprovementPromptBuilder.FileName);
        Assert.True(File.Exists(promptPath));
        var prompt = File.ReadAllText(promptPath);
        Assert.Contains("タブ別幾何平均",prompt,StringComparison.Ordinal);
        Assert.Contains("時間比ランキング",prompt,StringComparison.Ordinal);
        Assert.Contains("メモリ比ランキング",prompt,StringComparison.Ordinal);
    }

    [Fact]
    public void ExportRun_WritesProgressAndCsv(){
        var root = CreateTempRoot();
        var folder = Path.Combine(root,"export");
        var service = new BenchmarkRunPersistenceService();
        var state = CreateState(Path.Combine(root,".unsaved","run"));

        service.ExportRun(folder,state,false);

        Assert.True(File.Exists(Path.Combine(folder,BenchmarkStateManager.ProgressFileName)));
        Assert.True(File.Exists(Path.Combine(folder,"System.Array","N128.csv")));
        var loaded = service.ReadProgressState(folder)!;
        Assert.Equal(folder,loaded.RunRoot);
        Assert.Equal(0,loaded.RunNumber);
        Assert.Equal(state.ExecutionProfile,loaded.ExecutionProfile);
        Assert.Equal(5,loaded.Jobs.Count);
    }

    [Fact]
    public void ExportRun_WithoutOverwriteThrowsWhenOutputExists(){
        var root = CreateTempRoot();
        var folder = Path.Combine(root,"export");
        var service = new BenchmarkRunPersistenceService();
        var state = CreateState(Path.Combine(root,".unsaved","run"));
        service.ExportRun(folder,state,false);

        Assert.Throws<IOException>(() => service.ExportRun(folder,state,false));

        service.ExportRun(folder,state,true);
        Assert.True(File.Exists(Path.Combine(folder,BenchmarkStateManager.ProgressFileName)));
    }

    private static ProgressState CreateState(string runRoot){
        var now = DateTimeOffset.UtcNow;
        return new ProgressState{
            RunId="source",
            Mode=BenchmarkRunMode.Adaptive,
            ExecutionProfile=BenchmarkExecutionProfile.Accurate,
            RunRoot=runRoot,
            RunNumber=0,
            SourceTab=BenchmarkSourceTab.SystemArray,
            ElementCounts=[128,256,512],
            AdaptiveThresholdNanoseconds=1_000_000,
            CreatedAt=now.AddMinutes(-1),
            UpdatedAt=now,
            AdaptiveStates=[
                new AdaptiveBenchmarkState{
                    MethodName="Where",
                    IsCompleted=true,
                },
                new AdaptiveBenchmarkState{
                    MethodName="Select",
                },
            ],
            Jobs=[
                CompletedJob("System.Array.FLinq","Where",128,100),
                CompletedJob("System.Array.ZLinq","Where",128,120),
                new BenchmarkJob{
                    JobId="failed",
                    Project="System.Array.FLinq",
                    AssemblyName="Assembly",
                    TypeFullName="Type",
                    MethodName="Select",
                    SourceTab=BenchmarkSourceTab.SystemArray,
                    ElementCount=256,
                    PreparedElementCounts=[128,256,512],
                    Status=JobStatus.Failed,
                    FailureReason="boom",
                    ExitCode=1,
                    StartedAt=now,
                    FinishedAt=now,
                },
                new BenchmarkJob{
                    JobId="pending",
                    Project="System.Array.ZLinq",
                    AssemblyName="Assembly",
                    TypeFullName="Type",
                    MethodName="Select",
                    SourceTab=BenchmarkSourceTab.SystemArray,
                    ElementCount=256,
                    PreparedElementCounts=[128,256,512],
                    Status=JobStatus.Pending,
                },
                new BenchmarkJob{
                    JobId="running",
                    Project="System.Array.FLinq",
                    AssemblyName="Assembly",
                    TypeFullName="Type",
                    MethodName="Select",
                    SourceTab=BenchmarkSourceTab.SystemArray,
                    ElementCount=512,
                    PreparedElementCounts=[128,256,512],
                    Status=JobStatus.Running,
                    AssignedLogicalCpu=0,
                    StartedAt=now,
                },
            ],
        };
    }

    private static BenchmarkJob CompletedJob(string project,string method,int elementCount,double meanNanoseconds,long allocatedBytes = 0){
        return CompletedJob(BenchmarkSourceTab.SystemArray,project,method,elementCount,meanNanoseconds,allocatedBytes);
    }

    private static BenchmarkJob CompletedJob(BenchmarkSourceTab sourceTab,string project,string method,int elementCount,double meanNanoseconds,long allocatedBytes = 0){
        var now = DateTimeOffset.UtcNow;
        return new BenchmarkJob{
            JobId=project+"|"+method+"|"+elementCount,
            Project=project,
            AssemblyName="Assembly",
            TypeFullName="Type",
            MethodName=method,
            SourceTab=sourceTab,
            ElementCount=elementCount,
            PreparedElementCounts=[128,256,512],
            Status=JobStatus.Completed,
            Mean=meanNanoseconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            MeanNanoseconds=meanNanoseconds,
            ErrorNanoseconds=0,
            StdDevNanoseconds=0,
            AllocatedBytes=allocatedBytes,
            ExitCode=0,
            ArtifactFolder=string.Empty,
            StartedAt=now,
            FinishedAt=now,
        };
    }

    private static string CreateTempRoot(){
        var root = Path.Combine(AppContext.BaseDirectory,"BenchmarkRunPersistenceServiceTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
