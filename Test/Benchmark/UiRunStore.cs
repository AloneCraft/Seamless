using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UiRunStoreTests{
    [Fact]
    public void EnsureSchema_CreatesOnlySimpleJapaneseTables(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);

        store.EnsureSchema();

        using var connection = Open(path);
        Assert.Equal(6L,Scalar<long>(connection,"PRAGMA user_version;"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行';"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ベンチマーク項目';"));
        Assert.Equal(2L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','ベンチマーク項目');"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name IN('項目ID','プロジェクト名','Assembly','Type','型フル名','引数キー','行キー','側','平均ns','誤差ns','標準偏差ns','割当バイト');"));
    }

    [Fact]
    public void EnsureSchema_BackupsAndDeletesOldDatabaseFile(){
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

        var store = new global::Seamless.UI.Storage.UiRunStore(path,UiRunSchemaMismatchPolicy.Throw);
        store.EnsureSchema();

        using var loaded = Open(path);
        Assert.Equal(6L,Scalar<long>(loaded,"PRAGMA user_version;"));
        Assert.Equal(0L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Legacy';"));
        Assert.Contains(Directory.GetFiles(Path.GetDirectoryName(path)!),file => file.EndsWith(".bak",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureSchema_DeletesOldShapeEvenWhenUserVersionMatches(){
        var path = CreateDatabasePath();
        using(var connection = Open(path)){
            using var command = connection.CreateCommand();
            command.CommandText="""
                PRAGMA user_version=6;
                CREATE TABLE Apps(AppId INTEGER PRIMARY KEY AUTOINCREMENT,AppKind TEXT NOT NULL);
                CREATE TABLE Runs(RunId INTEGER PRIMARY KEY AUTOINCREMENT,AppId INTEGER NOT NULL);
                CREATE VIEW BenchmarkFlatResults AS SELECT 1 AS Value;
                """;
            command.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var store = new global::Seamless.UI.Storage.UiRunStore(path,UiRunSchemaMismatchPolicy.Throw);
        store.EnsureSchema();

        using var loaded = Open(path);
        Assert.Equal(6L,Scalar<long>(loaded,"PRAGMA user_version;"));
        Assert.Equal(1L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行';"));
        Assert.Equal(1L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ベンチマーク項目';"));
        Assert.Equal(0L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('Apps','Runs');"));
        Assert.Equal(0L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
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
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"ベンチマーク項目\";"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT \"左チェック\" FROM \"ベンチマーク項目\";"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT \"右チェック\" FROM \"ベンチマーク項目\";"));
        Assert.Equal("待機",Scalar<string>(connection,"SELECT \"左状態\" FROM \"ベンチマーク項目\";"));
        Assert.Equal("int,string",Scalar<string>(connection,"SELECT \"型\" FROM \"ベンチマーク項目\" LIMIT 1;"));
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
                new(true,UiRunStatus.Completed,string.Empty,measuredAt,12.5,1024,4096),
                new(false,UiRunStatus.Failed,"failed",measuredAt.AddSeconds(1),null,null,4096)),
        ]);

        var items = store.ListBenchmarkSnapshotItems(run.RunId);

        Assert.Single(items);
        var completed = items[0];
        Assert.Equal(run.RunId,completed.RunId);
        Assert.Equal("System.Array",completed.TabName);
        Assert.Equal("int",completed.TypeName);
        Assert.Equal("Any",completed.BenchmarkName);
        Assert.Equal(17,completed.CpuNumber);
        Assert.True(completed.Left.IsSelected);
        Assert.Equal(UiRunStatus.Completed,completed.Left.Status);
        Assert.Equal(4096,completed.Left.ElementCount);
        Assert.Equal(measuredAt,completed.Left.MeasuredAt);
        Assert.Equal(12.5,completed.Left.MeanNanoseconds);
        Assert.Equal(1024,completed.Left.AllocatedBytes);
        Assert.False(completed.Right.IsSelected);
        Assert.Equal(UiRunStatus.Failed,completed.Right.Status);
        Assert.Equal("failed",completed.Right.FailureReason);
        Assert.Null(completed.Right.MeanNanoseconds);
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
            new("System.Array","int","Any",0,new(true,UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,1,10,64),new(false,UiRunStatus.Queued,string.Empty,null,null,null,null)),
        ]);
        store.SaveBenchmarkSnapshotItems(run.RunId,[
            new("System.Array","int","Any",1,new(true,UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,2,20,4096),new(true,UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,3,30,4096)),
        ]);

        using var connection = Open(path);
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"ベンチマーク項目\";"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT \"CPU番号\" FROM \"ベンチマーク項目\";"));
        Assert.Equal(2.0,Scalar<double>(connection,"SELECT \"左時間ns\" FROM \"ベンチマーク項目\";"));
        Assert.Equal(3.0,Scalar<double>(connection,"SELECT \"右時間ns\" FROM \"ベンチマーク項目\";"));
    }

    [Fact]
    public void UiViewState_UsesRunTableColumns(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));

        store.SaveUiViewState(run.RunId,"SelectedSourceTab","SystemArray");
        store.SaveUiViewState(run.RunId,"SelectedResultElementCount","64");

        var viewStates = store.ListUiViewStates(run.RunId);
        Assert.DoesNotContain(viewStates,item => item.StateKey=="BenchmarkProgressStateJson");
        Assert.Contains(viewStates,item => item.StateKey=="SelectedSourceTab"&&item.StateValue=="SystemArray");
        Assert.Contains(viewStates,item => item.StateKey=="SelectedResultElementCount"&&item.StateValue=="64");
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
            new("System.Array","Sample","Aggregate",7,new(true,UiRunStatus.Completed,string.Empty,DateTimeOffset.UtcNow,123.5,1024,64),new(false,UiRunStatus.Queued,string.Empty,null,null,null,null)),
        ]);
        store.SaveUiViewState(run.RunId,"SelectedSourceTab","SystemArray");
        store.SaveUiViewState(run.RunId,"SelectedResultElementCount","64");

        Assert.NotEmpty(store.ListRuns());
        Assert.NotEmpty(store.ListBenchmarkSnapshotItems(run.RunId));
        Assert.NotEmpty(store.ListMetrics(run.RunId));
        Assert.NotEmpty(store.ListUiViewStates(run.RunId));

        store.DeleteAllRecords();

        Assert.Empty(store.ListRuns());
        Assert.Empty(store.ListBenchmarkSnapshotItems(run.RunId));
        Assert.Empty(store.ListMetrics(run.RunId));
        Assert.Empty(store.ListUiViewStates(run.RunId));
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
        return Path.Combine(root,"runs.db");
    }
}
