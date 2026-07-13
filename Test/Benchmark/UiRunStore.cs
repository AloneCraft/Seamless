using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UiRunStoreTests{
    [Fact]
    public void EnsureSchema_CreatesSeparateBenchmarkTables(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);

        store.EnsureSchema();

        using var connection = Open(path);
        Assert.Equal(19L,Scalar<long>(connection,"PRAGMA user_version;"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行';"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Benchmark';"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ScalableBenchmark';"));
        Assert.Equal(3L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('ベンチマーク項目','BenchmarkSnapshotCaseIdentity','BenchmarkSnapshotMeasurement');"));
        Assert.Equal(2L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('基準時間シェア','基準メモリシェア');"));
        Assert.Equal(2L,Scalar<long>(connection,"SELECT COUNT(*) FROM pragma_table_info('Benchmark') WHERE name IN('左LoopCount','右LoopCount');"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM pragma_table_info('Benchmark') WHERE name IN('Count','Initial','Data','Index','Page','Segment','Memory','Fraction');"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name='前回実行ID';"));
        new global::Seamless.UI.Storage.UiRunStore(path).EnsureSchema();
    }

    [Fact]
    public void EnsureSchema_ThrowsWithoutBackupOrDeleteForOldDatabaseFile(){
        var path = CreateDatabasePath();
        using(var connection = Open(path)){
            using var command = connection.CreateCommand();
            command.CommandText="""
                PRAGMA user_version=1;
                CREATE TABLE Legacy(Value TEXT NOT NULL);
                INSERT INTO Legacy(Value) VALUES('old');
                """;
            command.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var exception = Assert.Throws<InvalidOperationException>(store.EnsureSchema);

        using var loaded = Open(path);
        Assert.Contains("schema mismatch",exception.Message,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L,Scalar<long>(loaded,"PRAGMA user_version;"));
        Assert.Equal(1L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Legacy';"));
        Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(path)!),file => file.EndsWith(".bak",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureSchema_ThrowsWithoutBackupOrDeleteForOldShapeEvenWhenUserVersionMatches(){
        var path = CreateDatabasePath();
        using(var connection = Open(path)){
            using var command = connection.CreateCommand();
            command.CommandText="""
                PRAGMA user_version=19;
                CREATE TABLE Apps(AppId INTEGER PRIMARY KEY AUTOINCREMENT,AppKind TEXT NOT NULL);
                CREATE TABLE Runs(RunId INTEGER PRIMARY KEY AUTOINCREMENT,AppId INTEGER NOT NULL);
                CREATE VIEW BenchmarkFlatResults AS SELECT 1 AS Value;
                """;
            command.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var exception = Assert.Throws<InvalidOperationException>(store.EnsureSchema);

        using var loaded = Open(path);
        Assert.Contains("schema mismatch",exception.Message,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(19L,Scalar<long>(loaded,"PRAGMA user_version;"));
        Assert.Equal(1L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Apps';"));
        Assert.Equal(1L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Runs';"));
        Assert.Equal(1L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name='BenchmarkFlatResults';"));
        Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(path)!),file => file.EndsWith(".bak",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveDetectedBenchmarkItems_RoundTripsJapaneseColumnsAndSelection(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));

        store.SaveDetectedBenchmarkItems(run.RunId,[
            new("System.Array","System.Array.FLinq","System.Array.FLinq.Benchmark","int,string","Sample.Type`2[[System.Int32],[System.String]]","Sum",true,4096,8,null,"Count=4096","Count=4096","Sum","Left",""),
            new("System.Array","System.Array.ZLinq","System.Array.ZLinq.Benchmark","int,string","Sample.Type`2[[System.Int32],[System.String]]","Sum",false,4096,8,null,"Count=4096","Count=4096","Sum","Right",""),
        ]);

        using var connection = Open(path);
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"Benchmark\";"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT \"行チェック\" FROM \"Benchmark\";"));
        Assert.Equal("待機",Scalar<string>(connection,"SELECT \"左状態\" FROM \"Benchmark\";"));
        Assert.Equal("int,string",Scalar<string>(connection,"SELECT \"型\" FROM \"Benchmark\" LIMIT 1;"));
    }

    [Fact]
    public void SaveBenchmarkSnapshotItems_RoundTripsGridSnapshotRows(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        var measuredAt = DateTimeOffset.Parse(
            "2026-07-08T12:34:56.0000000+09:00",
            global::System.Globalization.CultureInfo.InvariantCulture);

        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new(
                "System.Array",
                "int",
                "Any",
                17,
                true,
                new(UiRunStatus.Completed,string.Empty,measuredAt,12.5,1024,524288,34_787_200),
                new(UiRunStatus.Failed,"failed",measuredAt.AddSeconds(1),null,null,524288,43_230_000),
                new("Left.Assembly","Left.Namespace.Type","Any","left-key","[1024]"),
                new("Right.Assembly","Right.Namespace.Type","Any","right-key","[1024]")),
        ]);

        var items = store.ListBenchmarkSnapshotItems(run.RunId);

        Assert.Single(items);
        var completed = items[0];
        Assert.Equal(run.RunId,completed.RunId);
        Assert.Equal("System.Array",completed.TabName);
        Assert.Equal("int",completed.TypeName);
        Assert.Equal("Any",completed.BenchmarkName);
        Assert.Equal(17,completed.CpuNumber);
        Assert.True(completed.IsSelected);
        Assert.Equal(UiRunStatus.Completed,completed.Left.Status);
        Assert.Equal(measuredAt,completed.Left.MeasuredAt);
        Assert.Equal(12.5,completed.Left.MeanNanoseconds);
        Assert.Equal(1024,completed.Left.AllocatedBytes);
        Assert.Equal(524288,completed.Left.LoopCount);
        Assert.Null(completed.Left.TotalNanoseconds);
        Assert.Equal(UiRunStatus.Failed,completed.Right.Status);
        Assert.Equal("failed",completed.Right.FailureReason);
        Assert.Null(completed.Right.MeanNanoseconds);
        Assert.Equal(524288,completed.Right.LoopCount);
        Assert.Null(completed.Right.TotalNanoseconds);
        Assert.Null(completed.LeftCase);
        Assert.Null(completed.RightCase);
    }

    [Fact]
    public void SaveBenchmarkSnapshotItems_ReplacesSameGridRow(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","int","Any",0,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,1,10),new(UiRunStatus.Queued,string.Empty,null,null,null)),
        ]);
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","int","Any",1,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,2,20),new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,3,30)),
        ]);

        using var connection = Open(path);
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"Benchmark\";"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT \"CPU番号\" FROM \"Benchmark\";"));
        Assert.Equal(2.0,Scalar<double>(connection,"SELECT \"左時間ns\" FROM \"Benchmark\";"));
        Assert.Equal(3.0,Scalar<double>(connection,"SELECT \"右時間ns\" FROM \"Benchmark\";"));
    }

    [Fact]
    public void ScalableBenchmark_StoresDedicatedArgumentsAndLoadsPreviousRun(){
        var path=CreateDatabasePath();
        var store=new global::Seamless.UI.Storage.UiRunStore(path,UiRunAppKind.ScalableBenchmark);
        var first=store.CreateRun(new(UiRunAppKind.ScalableBenchmark,"first","Fast","Scalable.Tsavorite",DateTimeOffset.UtcNow.AddMinutes(-1)));
        var identity=new UiBenchmarkSnapshotCaseWrite("assembly","type","Add","Count=100,Index=64,Page=32,Segment=16,Memory=1024,Fraction=0.5","[{\"Name\":\"Count\",\"Value\":\"100\"},{\"Name\":\"Index\",\"Value\":\"64\"},{\"Name\":\"Page\",\"Value\":\"32\"},{\"Name\":\"Segment\",\"Value\":\"16\"},{\"Name\":\"Memory\",\"Value\":\"1024\"},{\"Name\":\"Fraction\",\"Value\":\"0.5\"}]");
        store.SaveBenchmarkSnapshotItems(first.RunId,[new("Scalable.Tsavorite","int","Add",2,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,10,20),new(UiRunStatus.Queued,string.Empty,null,null,null),identity,null)]);
        store.SetRunStatus(first.RunId,UiRunStatus.Completed);

        var second=store.CreateRun(new(UiRunAppKind.ScalableBenchmark,"second","Fast","Scalable.Tsavorite",DateTimeOffset.UtcNow));
        store.SaveBenchmarkSnapshotItems(second.RunId,[new("Scalable.Tsavorite","int","Add",3,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,8,16),new(UiRunStatus.Queued,string.Empty,null,null,null),identity,null)]);

        var item=Assert.Single(store.ListBenchmarkSnapshotItems(second.RunId));
        Assert.Equal(8,item.Left.MeanNanoseconds);
        Assert.Equal(10,item.Right.MeanNanoseconds);
        Assert.Equal("Count=100,Index=64,Page=32,Segment=16,Memory=1024,Fraction=0.5",item.LeftCase!.ArgumentKey);
        using var connection=Open(path);
        Assert.Equal(first.RunId,Scalar<long>(connection,$"SELECT \"前回実行ID\" FROM \"実行\" WHERE \"実行ID\"={second.RunId};"));
        Assert.Equal(100L,Scalar<long>(connection,"SELECT \"Count\" FROM \"Benchmark\" WHERE \"実行ID\"="+second.RunId+";"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM pragma_table_info('Benchmark') WHERE name LIKE '前回%';"));
        var share=Assert.Single(store.QueryAnalysisView("Scalable前回シェア"),row => Convert.ToInt64(row["実行ID"])==second.RunId);
        Assert.Equal(8.0/18.0,Convert.ToDouble(share["今回時間シェア"]),12);
        Assert.Equal(10.0/18.0,Convert.ToDouble(share["前回時間シェア"]),12);
        Assert.Equal(16.0/36.0,Convert.ToDouble(share["今回メモリシェア"]),12);
        Assert.Equal(20.0/36.0,Convert.ToDouble(share["前回メモリシェア"]),12);
        var focused=Assert.Single(store.ListShareHistory(
            new(UiRunAppKind.ScalableBenchmark,"Scalable.Tsavorite","int","Add",identity),
            UiShareMetric.Time));
        Assert.Equal(second.RunId,focused.RunId);
        Assert.Equal(8.0/18.0,focused.LeftShare,12);
        Assert.Equal(10.0/18.0,focused.RightShare,12);
        var otherIdentity=identity with {
            ArgumentKey="Count=101",
            ArgumentsJson="[{\"Name\":\"Count\",\"Value\":\"101\"}]",
        };
        Assert.Empty(store.ListShareHistory(
            new(UiRunAppKind.ScalableBenchmark,"Scalable.Tsavorite","int","Add",otherIdentity),
            UiShareMetric.Time));
        Assert.Throws<InvalidDataException>(() => store.ListShareHistory(
            new(UiRunAppKind.ScalableBenchmark,"Scalable.Tsavorite","int","Add"),
            UiShareMetric.Time));
    }

    [Fact]
    public void ParameterSweep_StoresAndLoadsDedicatedResult(){
        var path=CreateDatabasePath();
        var store=new global::Seamless.UI.Storage.UiRunStore(path,UiRunAppKind.ParameterSweep);
        var run=store.CreateRun(new(UiRunAppKind.ParameterSweep,"sweep","Fast","ParameterSweep",DateTimeOffset.UtcNow));
        var measuredAt=DateTimeOffset.UtcNow;
        store.SaveParameterSweepResult(new(
            run.RunId,"HashSet.Int32.Memory.IndexSize","HashSet","Int32","Memory","IndexSize",
            64,32,16,1024,0.5,4096,4096,"Completed",string.Empty,12.5,8.25,measuredAt));

        var result=Assert.Single(store.ListParameterSweepResults(run.RunId));
        Assert.Equal(4096,result.MaxSuccessfulCount);
        Assert.Equal(12.5,result.AddElapsedMilliseconds);
        Assert.Equal(8.25,result.LookupMeanNanoseconds);
        using var connection=Open(path);
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"Benchmark\";"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name='アプリ種別';"));
    }

    [Fact]
    public void QueryAnalysisView_時間とメモリの日本語シェアビューを公開する(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","int","Any",0,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,20,200),new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,10,50)),
        ]);

        var time = Assert.Single(store.QueryAnalysisView("履歴時間シェア"));
        var memory = Assert.Single(store.QueryAnalysisView("履歴メモリシェア"));

        Assert.Equal("System.Array",time["タブ名"]);
        Assert.Equal(2.0/3.0,Convert.ToDouble(time["左シェア"],global::System.Globalization.CultureInfo.InvariantCulture),12);
        Assert.Equal(1.0/3.0,Convert.ToDouble(time["右シェア"],global::System.Globalization.CultureInfo.InvariantCulture),12);
        Assert.Equal("System.Array",memory["タブ名"]);
        Assert.Equal(0.8,Convert.ToDouble(memory["左シェア"],global::System.Globalization.CultureInfo.InvariantCulture),12);
        Assert.Equal(0.2,Convert.ToDouble(memory["右シェア"],global::System.Globalization.CultureInfo.InvariantCulture),12);
        Assert.Empty(store.QueryAnalysisView("基準時間シェア"));
        Assert.Empty(store.QueryAnalysisView("基準メモリシェア"));
        Assert.Empty(store.QueryAnalysisView("BenchmarkPerformanceRatioHistory"));
        Assert.Empty(store.QueryAnalysisView("BenchmarkMemoryRatioHistory"));
        using var connection=Open(path);
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('履歴時間比','基準時間比','履歴メモリ比','基準メモリ比');"));
    }

    [Fact]
    public void ListShareHistory_フォーカス行を厳密に絞り両側ゼロを保持する(){
        var path=CreateDatabasePath();
        var store=new global::Seamless.UI.Storage.UiRunStore(path);
        var first=store.CreateRun(new(UiRunAppKind.Benchmark,"first","Fast","System.Array",DateTimeOffset.UtcNow.AddMinutes(-1)));
        store.SaveBenchmarkSnapshotItems(first.RunId,[
            new("System.Array","int","Any",0,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,10,0),new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,30,0)),
            new("System.Array","int","Sum",1,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,50,500),new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,50,500)),
        ]);
        store.SetRunStatus(first.RunId,UiRunStatus.Completed);
        var second=store.CreateRun(new(UiRunAppKind.Benchmark,"second","Fast","System.Array",DateTimeOffset.UtcNow));
        store.SaveBenchmarkSnapshotItems(second.RunId,[new("System.Array","int","Any",0,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,30,0),new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,10,0))]);

        var query=new UiShareHistoryQuery(UiRunAppKind.Benchmark,"System.Array","int","Any");
        var time=store.ListShareHistory(query,UiShareMetric.Time);
        var memory=store.ListShareHistory(query,UiShareMetric.Memory);

        Assert.Equal(2,time.Count);
        Assert.Equal(first.RunId,time[0].RunId);
        Assert.Equal(0.25,time[0].LeftShare,12);
        Assert.Equal(0.75,time[0].RightShare,12);
        Assert.Equal(second.RunId,time[1].RunId);
        Assert.Equal(0.75,time[1].LeftShare,12);
        Assert.Equal(0.25,time[1].RightShare,12);
        Assert.Equal(2,memory.Count);
        Assert.All(memory,static point => {
            Assert.Equal(0,point.LeftShare);
            Assert.Equal(0,point.RightShare);
        });
    }

    [Fact]
    public void SaveBenchmarkSnapshotItems_UpdatesSingleRowWithoutDeletingOtherRows(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","int","Any",0,true,new(UiRunStatus.Queued,string.Empty,null,null,null),new(UiRunStatus.Queued,string.Empty,null,null,null)),
            new("System.Array","int","Sum",1,true,new(UiRunStatus.Queued,string.Empty,null,null,null),new(UiRunStatus.Queued,string.Empty,null,null,null)),
        ]);

        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","int","Any",2,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,10,100),new(UiRunStatus.Queued,string.Empty,null,null,null)),
        ]);

        var items = store.ListBenchmarkSnapshotItems(run.RunId);

        Assert.Equal(2,items.Count);
        var any = Assert.Single(items,static item => item.BenchmarkName=="Any");
        var sum = Assert.Single(items,static item => item.BenchmarkName=="Sum");
        Assert.Equal(UiRunStatus.Completed,any.Left.Status);
        Assert.Equal(10,any.Left.MeanNanoseconds);
        Assert.Equal(UiRunStatus.Queued,sum.Left.Status);
        Assert.Equal(1,sum.CpuNumber);
    }

    [Fact]
    public void FallbackDiagnostics_RoundTripsCounters(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        var recordedAt = DateTimeOffset.Parse(
            "2026-07-08T12:00:00.0000000+09:00",
            global::System.Globalization.CultureInfo.InvariantCulture);

        store.SaveFallbackDiagnostics(run.RunId,[
            new("System.Array.FLinq","ToList","DirectSpanUsed",1){
                SourceTab="System.Array",
                ArgumentKey="Count=4096",
                ElementCount=4096,
            },
        ],recordedAt);

        var items = store.ListFallbackDiagnostics(run.RunId);

        var item = Assert.Single(items);
        Assert.Equal(run.RunId,item.RunId);
        Assert.Equal("System.Array",item.SourceTab);
        Assert.Equal("System.Array.FLinq",item.Project);
        Assert.Equal("ToList",item.Method);
        Assert.Equal("Count=4096",item.ArgumentKey);
        Assert.Null(item.ElementCount);
        Assert.Equal("DirectSpanUsed",item.CounterName);
        Assert.Equal(1,item.CounterValue);
        Assert.Equal(recordedAt,item.RecordedAt);
    }

    [Fact]
    public void DeleteAllRecords_RemovesRunsAndRelatedState(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","Sample","Aggregate",7,true,new(UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,123.5,1024),new(UiRunStatus.Queued,string.Empty,null,null,null)),
        ]);
        store.SaveFallbackDiagnostics(run.RunId,[
            new("System.Array.FLinq","Aggregate","EnumerationLoopUsed",1),
        ]);

        Assert.NotEmpty(store.ListRuns());
        Assert.NotEmpty(store.ListBenchmarkSnapshotItems(run.RunId));
        Assert.NotEmpty(store.ListFallbackDiagnostics(run.RunId));
        Assert.NotEmpty(store.ListMetrics(run.RunId));

        store.DeleteAllRecords();

        Assert.Empty(store.ListRuns());
        Assert.Empty(store.ListBenchmarkSnapshotItems(run.RunId));
        Assert.Empty(store.ListFallbackDiagnostics(run.RunId));
        Assert.Empty(store.ListMetrics(run.RunId));
    }

    private static Microsoft.Data.Sqlite.SqliteConnection Open(string path){
        var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{
            DataSource=path,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static T Scalar<T>(Microsoft.Data.Sqlite.SqliteConnection connection,string sql){
        using var command = connection.CreateCommand();
        command.CommandText=sql;
        return (T)command.ExecuteScalar()!;
    }

    private static string CreateDatabasePath(){
        var root = Path.Combine(AppContext.BaseDirectory,"UiRunStoreTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root,"Benchmark.db");
    }
}
