namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class BenchmarkRunProjectionService{
    [Fact]
    public void GetVisibleResults_KeepsUnscopedRowsAndSelectedElementCount(){
        var service = new global::UI.BenchmarkRunProjectionService();
        var results = new[] {
            Result("System.Array.FLinq","Aggregate",0,10),
            Result("System.Array.FLinq","Aggregate",16,20),
            Result("System.Array.FLinq","Aggregate",64,30),
        };

        var visible = service.GetVisibleResults(results,16);

        Assert.Equal([0,16],visible.Select(static item => item.AxisCount).ToArray());
    }

    [Fact]
    public void BuildComparisonRowsForElementCount_UsesCompletedResultsOnly(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var session = new global::UI.BenchmarkRunSession();
        var service = new global::UI.BenchmarkRunProjectionService();
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",64,100));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",64,50));
        session.UpsertResult(Failed(sourceTab.LeftProject,"Aggregate",128));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",128,25));

        var rows = service.BuildComparisonRowsForElementCount(session.GetResults(sourceTab.Tab),sourceTab,64,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal("Aggregate",row.MethodName);
        Assert.Equal(100,row.Left.TimeNanoseconds);
        Assert.Equal(50,row.Right.TimeNanoseconds);
        Assert.Equal(2.0/3.0,row.左右シェア.時間シェア.左シェア!.Value,6);
    }

    [Fact]
    public void BuildLatestComparisonRows_UsesLatestElementCountGroup(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var session = new global::UI.BenchmarkRunSession();
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",16,100,now.AddMinutes(-3)));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",16,50,now.AddMinutes(-3)));
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",64,80,now.AddMinutes(-1)));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",64,40,now));

        var rows = service.BuildLatestComparisonRows(session.GetResults(sourceTab.Tab),sourceTab,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(80,row.Left.TimeNanoseconds);
        Assert.Equal(40,row.Right.TimeNanoseconds);
        Assert.Equal(2.0/3.0,row.左右シェア.時間シェア.左シェア!.Value,6);
    }

    [Fact]
    public void BuildLatestComparisonRows_UsesEachProjectsOwnLatestElementCountIndependently(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var session = new global::UI.BenchmarkRunSession();
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        session.UpsertResult(Result(sourceTab.LeftProject,"Aggregate",64,80,now.AddMinutes(-2)));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",64,40,now.AddMinutes(-2)));
        session.UpsertResult(Result(sourceTab.RightProject,"Aggregate",128,25,now));

        var rows = service.BuildLatestComparisonRows(session.GetResults(sourceTab.Tab),sourceTab,CreateRows);

        // FLinq(Left)はN=64で打ち切り、ZLinq(Right)はN=128まで進んだ(Fast/Adaptiveでは
        // プロジェクトごとに終端Nが異なるのが通常運転)。軸が揃わなくても両側の値を独立に
        // 採用し、時間シェアが欠落しないことを確認する。
        var row = Assert.Single(rows);
        Assert.Equal(80,row.Left.TimeNanoseconds);
        Assert.Equal(25,row.Right.TimeNanoseconds);
        Assert.False(row.左右シェア.時間シェア.欠測か);
    }

    [Fact]
    public void BuildDisplayedRowsForSelectedElementCount_FallsBackToTabOwnElementCountWhenSharedNDoesNotMatch(){
        var arrayTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var rangeTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemRange);
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        // 複数タブの結果を1つのResultItemsにマージした状態を模す。SystemArrayは大きいN、
        // SystemRangeは小さいNで完了しており、全タブ共通で選ばれるNはSystemArray側の64になる。
        var results = new[] {
            Result(arrayTab.LeftProject,"Aggregate",64,100,now),
            Result(arrayTab.RightProject,"Aggregate",64,50,now),
            Result(rangeTab.LeftProject,"Aggregate",8,30,now.AddMinutes(-1)),
            Result(rangeTab.RightProject,"Aggregate",8,15,now.AddMinutes(-1)),
        };

        var rows = service.BuildDisplayedRowsForSelectedElementCount(results,rangeTab,64,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(30,row.Left.TimeNanoseconds);
        Assert.Equal(15,row.Right.TimeNanoseconds);
    }

    [Fact]
    public void BuildDisplayedRowsForSelectedElementCount_UsesElementCountFilterWhenTabMatches(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var service = new global::UI.BenchmarkRunProjectionService();
        var results = new[] {
            Result(sourceTab.LeftProject,"Aggregate",64,100),
            Result(sourceTab.RightProject,"Aggregate",64,50),
            Result(sourceTab.LeftProject,"Aggregate",128,80),
            Result(sourceTab.RightProject,"Aggregate",128,40),
        };

        var rows = service.BuildDisplayedRowsForSelectedElementCount(results,sourceTab,64,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(100,row.Left.TimeNanoseconds);
        Assert.Equal(50,row.Right.TimeNanoseconds);
    }

    [Fact]
    public void BuildDisplayedRowsForSelectedElementCount_KeepsRowsWithDifferentTerminalElementCounts(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        // 適応的ループ回数を模す: Aggregate は N=64 で確定、Any は N=8 で確定。
        // 選択N=64 でも Any の行は自身の終端N(8)の値で表示される。
        var results = new[] {
            Result(sourceTab.LeftProject,"Aggregate",64,100,now),
            Result(sourceTab.RightProject,"Aggregate",64,50,now),
            Result(sourceTab.LeftProject,"Any",8,30,now.AddMinutes(-1)),
            Result(sourceTab.RightProject,"Any",8,15,now.AddMinutes(-1)),
        };

        var rows = service.BuildDisplayedRowsForSelectedElementCount(results,sourceTab,64,CreateRows);

        Assert.Equal(2,rows.Count);
        var aggregate = Assert.Single(rows,static row => row.MethodName=="Aggregate");
        Assert.Equal(100,aggregate.Left.TimeNanoseconds);
        Assert.Equal(50,aggregate.Right.TimeNanoseconds);
        var any = Assert.Single(rows,static row => row.MethodName=="Any");
        Assert.Equal(30,any.Left.TimeNanoseconds);
        Assert.Equal(15,any.Right.TimeNanoseconds);
    }

    [Fact]
    public void BuildDisplayedRowsForSelectedElementCount_PrefersSelectedElementCountOverLaterMeasurement(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        // 選択Nの計測がある行はフォールバックせず選択Nの値を使う(より新しい別Nの計測があっても)。
        var results = new[] {
            Result(sourceTab.LeftProject,"Aggregate",64,100,now.AddMinutes(-1)),
            Result(sourceTab.RightProject,"Aggregate",64,50,now.AddMinutes(-1)),
            Result(sourceTab.LeftProject,"Aggregate",128,80,now),
            Result(sourceTab.RightProject,"Aggregate",128,40,now),
        };

        var rows = service.BuildDisplayedRowsForSelectedElementCount(results,sourceTab,64,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(100,row.Left.TimeNanoseconds);
        Assert.Equal(50,row.Right.TimeNanoseconds);
    }

    [Fact]
    public void BuildDisplayedRowsForSelectedElementCount_FallsBackPerProjectWhenOnlyOneProjectReachedSelectedElementCount(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        // 選択N=128にはLeftプロジェクトのみ到達。RightプロジェクトはN=64で打ち切り
        // (Fast/Adaptiveではプロジェクトごとに終端Nが異なるのが通常運転)。Rightは自身の
        // 最新(N=64)へフォールバックし、行・時間シェアが丸ごと欠落しないことを確認する。
        var results = new[] {
            Result(sourceTab.LeftProject,"Aggregate",64,100,now.AddMinutes(-1)),
            Result(sourceTab.RightProject,"Aggregate",64,50,now.AddMinutes(-1)),
            Result(sourceTab.LeftProject,"Aggregate",128,80,now),
        };

        var rows = service.BuildDisplayedRowsForSelectedElementCount(results,sourceTab,128,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(80,row.Left.TimeNanoseconds);
        Assert.Equal(50,row.Right.TimeNanoseconds);
        Assert.False(row.左右シェア.時間シェア.欠測か);
    }

    [Fact]
    public void BuildDisplayedRowsForSelectedElementCount_UsesLatestWhenNoElementCountSelected(){
        var sourceTab = global::UI.BenchmarkSourceTabs.Get(global::UI.BenchmarkSourceTab.SystemArray);
        var service = new global::UI.BenchmarkRunProjectionService();
        var now = DateTimeOffset.UtcNow;
        var results = new[] {
            Result(sourceTab.LeftProject,"Aggregate",64,80,now.AddMinutes(-1)),
            Result(sourceTab.RightProject,"Aggregate",64,40,now),
        };

        var rows = service.BuildDisplayedRowsForSelectedElementCount(results,sourceTab,null,CreateRows);

        var row = Assert.Single(rows);
        Assert.Equal(80,row.Left.TimeNanoseconds);
        Assert.Equal(40,row.Right.TimeNanoseconds);
    }

    private static global::UI.ReportRow[] CreateRows(
        global::UI.BenchmarkSourceTabDefinition sourceTab,
        IReadOnlyList<global::UI.BenchmarkResultItem> results) =>
        results.Select(static item => new global::UI.ReportRow(
            item.Project,
            item.Method,
            item.TypeFullName,
            item.Method,
            item.Status,
            item.Mean,
            item.ErrorNanoseconds?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.StdDevNanoseconds?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.AllocatedBytesValue?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.MeanNanoseconds??0,
            item.ErrorNanoseconds??0,
            item.StdDevNanoseconds??0,
            item.AllocatedBytesValue??0,
            0,
            0,
            item.AssignedLogicalCpu?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            item.StartedAt?.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.StartedAt?.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            item.ArtifactFolder,
            item.ExitCode,
            item.ArgumentKey,
            item.Arguments)).ToArray();

    private static global::UI.BenchmarkResultItem Result(
        string project,
        string method,
        int elementCount,
        double mean,
        DateTimeOffset? startedAt = null){
        var sourceTab = global::UI.BenchmarkSourceTabs.TryGetByProject(project)?.Tab??global::UI.BenchmarkSourceTab.SystemArray;
        var loopCount = global::UI.BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab,elementCount);
        return new(new global::UI.BenchmarkJob{
            Project=project,
            MethodName=method,
            TypeFullName="Sample.Type",
            SourceTab=sourceTab,
            ElementCount=global::UI.BenchmarkWorkloadAxis.ResolveElementCount(sourceTab,elementCount),
            LoopCount=loopCount,
            Status=global::UI.JobStatus.Completed,
            MeanNanoseconds=ToStoredTotal(sourceTab,loopCount,mean),
            AllocatedBytes=ToStoredTotal(sourceTab,loopCount,128),
            StartedAt=startedAt??DateTimeOffset.UtcNow,
            ExitCode=0,
        });
    }

    private static global::UI.BenchmarkResultItem Failed(string project,string method,int elementCount){
        var sourceTab = global::UI.BenchmarkSourceTabs.TryGetByProject(project)?.Tab??global::UI.BenchmarkSourceTab.SystemArray;
        return new(new global::UI.BenchmarkJob{
            Project=project,
            MethodName=method,
            TypeFullName="Sample.Type",
            SourceTab=sourceTab,
            ElementCount=global::UI.BenchmarkWorkloadAxis.ResolveElementCount(sourceTab,elementCount),
            LoopCount=global::UI.BenchmarkWorkloadAxis.ResolveLoopCount(sourceTab,elementCount),
            Status=global::UI.JobStatus.Failed,
            StartedAt=DateTimeOffset.UtcNow,
            ExitCode=1,
        });
    }

    private static double ToStoredTotal(global::UI.BenchmarkSourceTab sourceTab,int loopCount,double perOperation) =>
        global::UI.BenchmarkWorkloadAxis.UsesLoopCount(sourceTab)&&loopCount>1 ? perOperation*loopCount : perOperation;

    private static long ToStoredTotal(global::UI.BenchmarkSourceTab sourceTab,int loopCount,long perOperation) =>
        global::UI.BenchmarkWorkloadAxis.UsesLoopCount(sourceTab)&&loopCount>1 ? perOperation*loopCount : perOperation;
}

