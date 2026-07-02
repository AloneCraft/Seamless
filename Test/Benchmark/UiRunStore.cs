using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UiRunStore{
    [Fact]
    public void CreateRun_InitializesSchemaAndListsRun(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);

        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark run",
            "Fast",
            "SystemArray",
            DateTimeOffset.UtcNow));

        var runs = store.ListRuns();

        Assert.Equal(UiRunStatus.Queued,run.Status);
        var loaded = Assert.Single(runs);
        Assert.Equal(run.RunId,loaded.RunId);
        Assert.Equal(UiRunAppKind.Benchmark,loaded.AppKind);
        Assert.Equal("Benchmark run",loaded.DisplayName);
    }

    [Fact]
    public void UpsertCaseJobAndMeasurement_RoundTripsNormalizedMetrics(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.ScalableBenchmark,
            "Scalable",
            "Accurate",
            "ScalableSet",
            DateTimeOffset.UtcNow));
        var testCase = store.UpsertCase(
            run.RunId,
            "Aggregate|N=64",
            "Aggregate",
            "Left",
            "Aggregate",
            "Sample.Type",
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
                ["Count"]="64",
            });
        var job = store.UpsertJob(
            run.RunId,
            testCase.CaseId,
            "Aggregate|Left|64|0",
            UiRunStatus.Completed,
            64,
            1,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "Completed",
            string.Empty);

        store.SaveMeasurement(job.JobId,[
            new("MeanNanoseconds",123.5,"ns"),
            new("AllocatedBytes",4096,"bytes"),
        ]);
        store.SetRunStatus(run.RunId,UiRunStatus.Completed,"done");

        var metrics = store.ListMetrics(run.RunId);
        var jobs = store.ListJobs(run.RunId);
        var loadedRun = Assert.Single(store.ListRuns(UiRunAppKind.ScalableBenchmark));

        Assert.Equal(UiRunStatus.Completed,loadedRun.Status);
        Assert.Equal(1,Assert.Single(jobs).LoopCount);
        Assert.Contains(metrics,item => item.MetricName=="MeanNanoseconds"&&Math.Abs(item.NumericValue-123.5)<0.001);
        Assert.Contains(metrics,item => item.MetricName=="AllocatedBytes"&&Math.Abs(item.NumericValue-4096)<0.001);
    }

    [Fact]
    public void AnalysisViews_ReturnExpectedRows(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "SystemArray",
            DateTimeOffset.UtcNow));
        var leftCase = store.UpsertCase(run.RunId,"left","Aggregate","System.Array.FLinq","Aggregate","FLinq.Sample.Type",
            sourceTabId:"System.Array",argumentKey:"Count=4096",side:"Left");
        var rightCase = store.UpsertCase(run.RunId,"right","Aggregate","System.Array.ZLinq","Aggregate","ZLinq.Sample.Type",
            sourceTabId:"System.Array",argumentKey:"Count=4096",side:"Right");
        var leftJob = store.UpsertJob(run.RunId,leftCase.CaseId,"left-job",UiRunStatus.Completed,4096,8,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"",string.Empty);
        var rightJob = store.UpsertJob(run.RunId,rightCase.CaseId,"right-job",UiRunStatus.Completed,4096,8,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"",string.Empty);
        store.SaveMeasurement(leftJob.JobId,[new("MeanNanoseconds",200,"ns"),new("StdDevNanoseconds",10,"ns"),new("AllocatedBytes",2048,"bytes")]);
        store.SaveMeasurement(rightJob.JobId,[new("MeanNanoseconds",100,"ns"),new("StdDevNanoseconds",5,"ns"),new("AllocatedBytes",1024,"bytes")]);
        store.SetRunStatus(run.RunId,UiRunStatus.Completed,"done");

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{
            DataSource=path,
        }.ToString());
        connection.Open();

        {
            //TypeFullName が Left/Right で違ってもペアが成立する
            Assert.Equal(2,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkFlatResults WHERE LoopCount=8;"));
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkPairComparisons WHERE LoopCount=8 AND MeanRatio=2;"));
            Assert.Equal(2,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkTrendResults WHERE ElementCount=4096 AND LoopCount=8;"));
        }
        {
            //Completed Run の履歴集計 View 群
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkImprovementCandidates WHERE Method='Aggregate' AND AvgMeanRatio=2;"));
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkTimeRatioWorst WHERE Method='Aggregate';"));
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkAllocationRatioWorst WHERE Method='Aggregate' AND AvgAllocationRatio=2;"));
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkCategorySummary WHERE Category='Aggregate';"));
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkHistoryTrend WHERE Method='Aggregate';"));
            Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkScaleTrend WHERE ElementCount=4096;"));
            Assert.Equal(0,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkMissingFailureMatrix;"));
            Assert.Equal(2,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkMeasurementQuality WHERE StdDevRatio=0.05;"));
        }
    }

    [Fact]
    public void QueryAnalysisView(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "SystemArray",
            DateTimeOffset.UtcNow));
        var testCase = store.UpsertCase(run.RunId,"case","Sum","System.Array.FLinq","Sum","FLinq.Sample.Type",
            sourceTabId:"System.Array",argumentKey:"",side:"Left");
        store.UpsertJob(run.RunId,testCase.CaseId,"job",UiRunStatus.Completed,64,1,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"",string.Empty);
        {
            //ホワイトリスト内の View は汎用行で読める
            var rows = store.QueryAnalysisView("BenchmarkFlatResults");
            var row = Assert.Single(rows);
            Assert.Equal("Sum",row["Method"]);
            Assert.Equal("Left",row["Side"]);
        }
        {
            //全分析 View が定義済みで読み出せる
            foreach(var viewName in global::Seamless.UI.Storage.UiRunStore.AnalysisViewNames){
                _=store.QueryAnalysisView(viewName,limit:10);
            }
        }
        {
            //未知の View 名は拒否する
            Assert.Throws<ArgumentOutOfRangeException>(() => store.QueryAnalysisView("Runs; DROP TABLE Runs;"));
        }
    }

    [Fact]
    public void MissingFailureMatrix_ReportsFailedAndMissingSide(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "SystemArray",
            DateTimeOffset.UtcNow));
        var leftCase = store.UpsertCase(run.RunId,"left","Sum","System.Array.FLinq","Sum","FLinq.Sample.Type",
            sourceTabId:"System.Array",argumentKey:"",side:"Left");
        store.UpsertJob(run.RunId,leftCase.CaseId,"left-job",UiRunStatus.Failed,4096,8,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"Timeout","timeout");

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{
            DataSource=path,
        }.ToString());
        connection.Open();

        //右側欠測かつ失敗として 1 行報告される
        Assert.Equal(1,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkMissingFailureMatrix WHERE Side='Left' AND OppositeMissing=1 AND JobStatus='Failed';"));
    }

    [Fact]
    public void FallbackDiagnostics_RoundTripsAndAggregates(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "SystemArray",
            DateTimeOffset.UtcNow));

        store.SaveFallbackDiagnostics(run.RunId,[
            new("System.Array.FLinq","Aggregate","EnumerableFallback",3),
            new("System.Array.FLinq","Sum","EnumerableFallback",0),
        ]);
        {
            //同一キーは上書きされる
            store.SaveFallbackDiagnostics(run.RunId,[
                new("System.Array.FLinq","Aggregate","EnumerableFallback",5),
            ]);
        }

        var diagnostics = store.ListFallbackDiagnostics(run.RunId);
        Assert.Equal(2,diagnostics.Count);
        Assert.Contains(diagnostics,item => item.Method=="Aggregate"&&Math.Abs(item.CounterValue-5)<0.001);

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{
            DataSource=path,
        }.ToString());
        connection.Open();
        Assert.Equal(2,Scalar<long>(connection,"SELECT COUNT(*) FROM BenchmarkFallbackDiagnostics;"));
    }

    [Fact]
    public void SchemaMismatch_BackupPolicyRenamesOldDatabase(){
        var path = CreateDatabasePath();
        {
            //v3 のDBを作った後、旧バージョンを装う
            var store = new global::Seamless.UI.Storage.UiRunStore(path);
            store.EnsureSchema();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{
                DataSource=path,
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText="PRAGMA user_version=1;";
            command.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        {
            //開発ポリシーではバックアップリネームして新規作成する
            var store = new global::Seamless.UI.Storage.UiRunStore(path,UiRunSchemaMismatchPolicy.BackupAndRecreate);
            store.EnsureSchema();
            Assert.True(File.Exists(path));
            Assert.Contains(Directory.GetFiles(Path.GetDirectoryName(path)!),file => file.Contains(".v1.")&&file.EndsWith(".bak"));
        }
    }

    [Fact]
    public void SchemaMismatch_ThrowPolicyRefusesOldDatabase(){
        var path = CreateDatabasePath();
        {
            var store = new global::Seamless.UI.Storage.UiRunStore(path);
            store.EnsureSchema();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{
                DataSource=path,
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText="PRAGMA user_version=1;";
            command.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        {
            //通常運用ポリシーでは無警告に破棄せず例外で停止する
            var store = new global::Seamless.UI.Storage.UiRunStore(path,UiRunSchemaMismatchPolicy.Throw);
            Assert.Throws<InvalidOperationException>(() => store.EnsureSchema());
            Assert.True(File.Exists(path));
        }
    }

    [Fact]
    public void UiState_RoundTripsSelectionsAndViewState(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.ParameterSweep,
            "Sweep",
            "Default",
            "ParameterSweep",
            DateTimeOffset.UtcNow));

        store.SaveUiSelection(run.RunId,"cell","HashSet|Int32",true);
        store.SaveUiSelection(run.RunId,"cell","Dictionary|String",false);
        store.SaveUiViewState(run.RunId,"SelectedLowerTab","Chart");

        var selections = store.ListUiSelections(run.RunId);
        var viewStates = store.ListUiViewStates(run.RunId);

        Assert.Contains(selections,item => item.SelectionKey=="HashSet|Int32"&&item.IsSelected);
        Assert.Contains(selections,item => item.SelectionKey=="Dictionary|String"&&!item.IsSelected);
        var viewState = Assert.Single(viewStates);
        Assert.Equal("SelectedLowerTab",viewState.StateKey);
        Assert.Equal("Chart",viewState.StateValue);
    }

    private static T Scalar<T>(Microsoft.Data.Sqlite.SqliteConnection connection,string sql){
        using var command = connection.CreateCommand();
        command.CommandText=sql;
        return (T)command.ExecuteScalar()!;
    }

    private static string CreateDatabasePath(){
        var root = Path.Combine(AppContext.BaseDirectory,"UiRunStoreTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root,"runs.db");
    }
}




