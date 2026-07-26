using Microsoft.Data.Sqlite;
using Storage = Seamless.UI.Storage;

namespace Test.Benchmark;

public class BenchmarkTabStorage {
    [Fact]
    public void 通常Benchmarkのスキーマは11表と左右2列の物理列を作成する() {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        store.スキーマを確認();

        using var connection=new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader=command.ExecuteReader();
        var tables=new List<string>();
        while(reader.Read()) tables.Add(reader.GetString(0));

        var expected=new[]{"書き換え","パイプライン融合","述語プッシュダウン","Array","IEnumerable","Range","List","Persisted(Seamless)","Persisted(System)","Tsavorite(Seamless)","Tsavorite(System)"};
        Assert.All(expected,table=>Assert.Contains(table,tables));
        Assert.Contains("実行",tables);
        Assert.DoesNotContain("Persisted",tables);
        Assert.DoesNotContain("Tsavorite",tables);
        foreach(var table in expected){
            using var foreignKeyCommand=connection.CreateCommand();
            foreignKeyCommand.CommandText=$"SELECT on_delete FROM pragma_foreign_key_list('{table}') WHERE \"table\"='実行';";
            Assert.Equal("CASCADE",Assert.IsType<string>(foreignKeyCommand.ExecuteScalar()));
        }

        var arrayColumns=列名を取得(connection,"Array");
        Assert.Contains("FLinq_アセンブリ名",arrayColumns);
        Assert.Contains("FLinq_型完全名",arrayColumns);
        Assert.Contains("ZLinq_アセンブリ名",arrayColumns);
        Assert.Contains("ZLinq_型完全名",arrayColumns);
        Assert.DoesNotContain("Enumerable_アセンブリ名",arrayColumns);
        Assert.DoesNotContain("Enumerable_型完全名",arrayColumns);
        Assert.Equal(8,arrayColumns.Count(static column=>column.StartsWith("FLinq_",StringComparison.Ordinal)));
        Assert.Equal(8,arrayColumns.Count(static column=>column.StartsWith("ZLinq_",StringComparison.Ordinal)));

        var persistedColumns=列名を取得(connection,"Persisted(System)");
        Assert.Contains("FLinq_アセンブリ名",persistedColumns);
        Assert.Contains("Persisted(System)_アセンブリ名",persistedColumns);
        Assert.DoesNotContain("ValueEnumerable_アセンブリ名",persistedColumns);
        Assert.DoesNotContain("FLinq(IEnumerable)_アセンブリ名",persistedColumns);
    }

    [Fact]
    public void 左右2列の結果は保存後に読み戻せる() {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        var run=store.実行を作成(new(Storage.UI実行アプリ種別.ベンチマーク,"test","Fast","読込確認",DateTimeOffset.UtcNow));
        var completed=new Storage.UIベンチマークグリッド側書込(Storage.UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,12.5,64,4);
        var failed=new Storage.UIベンチマークグリッド側書込(Storage.UI実行状態.失敗,"worker failure",DateTimeOffset.UtcNow,null,null,4);
        var leftIdentity=new Storage.UIベンチマークスナップショットケース書込("Left.Assembly","Persisted<int>","Persisted<int>.Aggregate_func","Count=64","[]");
        var rightIdentity=new Storage.UIベンチマークスナップショットケース書込("Right.Assembly","Persisted<int>","Persisted<int>.Aggregate_func","Count=64","[]");

        store.ベンチマークスナップショット項目を保存(run.実行ID,[
            new("Seamless.Persisted.System","Persisted<int>","Persisted<int>.Aggregate_func",0,true,completed,failed,leftIdentity,rightIdentity),
            new("Seamless.Tsavorite.Seamless","Tsavorite<ClassInt32>","Tsavorite<ClassInt32>.Aggregate_func",0,true,completed,completed,leftIdentity,rightIdentity),
            new("System.Array","Array<int>","Array<int>.Where",0,true,completed,completed,leftIdentity,rightIdentity),
        ]);

        var items=store.ベンチマークスナップショット項目一覧を取得(run.実行ID);
        Assert.Equal(3,items.Count);
        var persisted=Assert.Single(items,item=>item.タブ名=="Seamless.Persisted.System");
        Assert.Equal("Persisted<int>",persisted.型名);
        Assert.Equal("Persisted<int>.Aggregate_func",persisted.ベンチマーク名);
        Assert.Equal(12.5,persisted.左.平均ナノ秒);
        Assert.Equal(64,persisted.左.割当バイト);
        Assert.Equal(leftIdentity,persisted.左ケース);
        Assert.Equal(rightIdentity,persisted.右ケース);
        Assert.Equal(Storage.UI実行状態.失敗,persisted.右.状態);
        Assert.Equal("worker failure",persisted.右.失敗理由);
        var tsavorite=Assert.Single(items,item=>item.タブ名=="Seamless.Tsavorite.Seamless");
        Assert.Equal("Tsavorite<ClassInt32>",tsavorite.型名);
        Assert.Equal("Tsavorite<ClassInt32>.Aggregate_func",tsavorite.ベンチマーク名);
        var array=Assert.Single(items,item=>item.タブ名=="System.Array");
        Assert.Equal("Array<int>.Where",array.ベンチマーク名);

        store.実行を削除(run.実行ID);
        Assert.Empty(store.ベンチマークスナップショット項目一覧を取得(run.実行ID));
    }

    private static List<string> 列名を取得(SqliteConnection connection,string table) {
        using var command=connection.CreateCommand();
        command.CommandText=$"SELECT name FROM pragma_table_info('{table}');";
        using var reader=command.ExecuteReader();
        var result=new List<string>();
        while(reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    private static string DBパスを作成() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless","BenchmarkTabStorage",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"Benchmark.db");
    }
}