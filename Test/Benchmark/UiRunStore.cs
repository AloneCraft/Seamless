using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UiRunStore{
    [Fact]
    public void EnsureSchema_CreatesOnlySimpleJapaneseTables(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);

        store.EnsureSchema();

        using var connection = Open(path);
        Assert.Equal(4L,Scalar<long>(connection,"PRAGMA user_version;"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行';"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ベンチマーク項目';"));
        Assert.Equal(2L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','ベンチマーク項目');"));
        Assert.Equal(0L,Scalar<long>(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
    }

    [Fact]
    public void EnsureSchema_DeletesOldDatabaseFileWithoutBackup(){
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
        Assert.Equal(4L,Scalar<long>(loaded,"PRAGMA user_version;"));
        Assert.Equal(0L,Scalar<long>(loaded,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Legacy';"));
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
            new("System.Array","System.Array.FLinq","int,string","Sample.Type`2[[System.Int32],[System.String]]","Sum",true,4096,8,null,"Count=4096","Count=4096","Sum","Left",""),
            new("System.Array","System.Array.ZLinq","int,string","Sample.Type`2[[System.Int32],[System.String]]","Sum",false,4096,8,null,"Count=4096","Count=4096","Sum","Right",""),
        ]);

        using var connection = Open(path);
        Assert.Equal(2L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"ベンチマーク項目\";"));
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"ベンチマーク項目\" WHERE \"選択済み\"=1;"));
        Assert.Equal("未計測",Scalar<string>(connection,"SELECT \"計測状態\" FROM \"ベンチマーク項目\" WHERE \"プロジェクト名\"='System.Array.FLinq';"));
        Assert.Equal("int,string",Scalar<string>(connection,"SELECT \"Type\" FROM \"ベンチマーク項目\" LIMIT 1;"));
    }

    [Fact]
    public void UpsertCaseJobAndMeasurement_UpdatesDetectedItem(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Array",
            DateTimeOffset.UtcNow));
        store.SaveDetectedBenchmarkItems(run.RunId,[
            new("System.Array","System.Array.FLinq","Sample","Sample.Type","Aggregate",true,64,1,null,"Count=64","Count=64","Aggregate","Left",""),
        ]);

        var testCase = store.UpsertCase(
            run.RunId,
            "case",
            "Aggregate",
            "System.Array.FLinq",
            "Aggregate",
            "Sample.Type",
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["Count"]="64"},
            "System.Array",
            "Count=64",
            "Left");
        var job = store.UpsertJob(run.RunId,testCase.CaseId,"job",UiRunStatus.Completed,64,1,7,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"",string.Empty);
        store.SaveMeasurement(job.JobId,[new("MeanNanoseconds",123.5,"ns"),new("AllocatedBytes",1024,"bytes")],DateTimeOffset.UtcNow);

        using var connection = Open(path);
        Assert.Equal(1L,Scalar<long>(connection,"SELECT COUNT(*) FROM \"ベンチマーク項目\";"));
        Assert.Equal("完了",Scalar<string>(connection,"SELECT \"計測状態\" FROM \"ベンチマーク項目\";"));
        Assert.Equal(123.5,Scalar<double>(connection,"SELECT \"平均ns\" FROM \"ベンチマーク項目\";"));
        Assert.Equal(7L,Scalar<long>(connection,"SELECT \"CPU番号\" FROM \"ベンチマーク項目\";"));
    }

    [Fact]
    public void UpsertCaseJobAndMeasurement_UpdatesMatchingDetectedElementCount(){
        var path = CreateDatabasePath();
        var store = new global::Seamless.UI.Storage.UiRunStore(path);
        var run = store.CreateRun(new(
            UiRunAppKind.Benchmark,
            "Benchmark",
            "Fast",
            "System.Collections.Generic.IEnumerable",
            DateTimeOffset.UtcNow));
        store.SaveDetectedBenchmarkItems(run.RunId,[
            new("System.Collections.Generic.IEnumerable","System.Collections.Generic.IEnumerable.FLinq","T","System.Collections.Generic.IEnumerable`1[[System.Int32]]","AggregateBy_keySelector",true,64,1,null,"","", "AggregateBy_keySelector","Left",""),
            new("System.Collections.Generic.IEnumerable","System.Collections.Generic.IEnumerable.FLinq","T","System.Collections.Generic.IEnumerable`1[[System.Int32]]","AggregateBy_keySelector",true,4096,1,null,"","", "AggregateBy_keySelector","Left",""),
        ]);

        var testCase = store.UpsertCase(
            run.RunId,
            "case",
            "AggregateBy_keySelector",
            "System.Collections.Generic.IEnumerable.FLinq",
            "AggregateBy_keySelector",
            "System.Collections.Generic.IEnumerable`1[[System.Int32]]",
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase),
            "System.Collections.Generic.IEnumerable",
            "",
            "Left",
            "",
            4096,
            1);
        var job = store.UpsertJob(run.RunId,testCase.CaseId,"job",UiRunStatus.Completed,4096,1,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"",string.Empty);
        store.SaveMeasurement(job.JobId,[new("MeanNanoseconds",99.0,"ns")],DateTimeOffset.UtcNow);

        using var connection = Open(path);
        Assert.Equal("未計測",Scalar<string>(connection,"SELECT \"計測状態\" FROM \"ベンチマーク項目\" WHERE \"要素数\"=64;"));
        Assert.Equal("完了",Scalar<string>(connection,"SELECT \"計測状態\" FROM \"ベンチマーク項目\" WHERE \"要素数\"=4096;"));
        Assert.Equal(99.0,Scalar<double>(connection,"SELECT \"平均ns\" FROM \"ベンチマーク項目\" WHERE \"要素数\"=4096;"));
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

        store.SaveUiViewState(run.RunId,"BenchmarkProgressStateJson","{}");
        store.SaveUiViewState(run.RunId,"SelectedSourceTab","SystemArray");
        store.SaveUiViewState(run.RunId,"SelectedResultElementCount","64");

        var viewStates = store.ListUiViewStates(run.RunId);
        Assert.Contains(viewStates,item => item.StateKey=="BenchmarkProgressStateJson"&&item.StateValue=="{}");
        Assert.Contains(viewStates,item => item.StateKey=="SelectedSourceTab"&&item.StateValue=="SystemArray");
        Assert.Contains(viewStates,item => item.StateKey=="SelectedResultElementCount"&&item.StateValue=="64");
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
