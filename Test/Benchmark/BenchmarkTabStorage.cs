using Microsoft.Data.Sqlite;
using Storage = Seamless.UI.Storage;

namespace Test.Benchmark;

public class BenchmarkTabStorage {
    [Fact]
    public void 通常Benchmarkのスキーマは11表と見出し由来の物理列を作成する() {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        store.スキーマを確認();

        using var connection=new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        using var reader=command.ExecuteReader();
        var tables=new List<string>();
        while(reader.Read()){
            tables.Add(reader.GetString(0));
        }

        var expected=new[]{"書き換え","パイプライン融合","述語プッシュダウン","Array","IEnumerable","Range","List","Persisted(Seamless)","Persisted(System)","Tsavorite(Seamless)","Tsavorite(System)"};
        Assert.All(expected,table=>Assert.Contains(table,tables));
        Assert.DoesNotContain("Persisted",tables);
        Assert.DoesNotContain("Tsavorite",tables);
        foreach(var table in expected){
            using var foreignKeyCommand=connection.CreateCommand();
            foreignKeyCommand.CommandText=$"SELECT on_delete FROM pragma_foreign_key_list('{table}') WHERE \"table\"='実行';";
            Assert.Equal("CASCADE",Assert.IsType<string>(foreignKeyCommand.ExecuteScalar()));
        }

        var arrayColumns=列名を取得(connection,"Array");
        Assert.Contains("Enumerable_アセンブリ名",arrayColumns);
        Assert.DoesNotContain("System.Linq_アセンブリ名",arrayColumns);
        var persistedColumns=列名を取得(connection,"Persisted(System)");
        Assert.Contains("FLinq_アセンブリ名",persistedColumns);
        Assert.Contains("Persisted(System)_アセンブリ名",persistedColumns);
        Assert.DoesNotContain("ValueEnumerable_アセンブリ名",persistedColumns);
        Assert.DoesNotContain("FLinq(IEnumerable)_アセンブリ名",persistedColumns);
    }

    [Fact]
    public void 二列と三列の結果は保存後に読み戻せて実行削除で全表から消える() {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        var run=store.実行を作成(new(Storage.UI実行アプリ種別.ベンチマーク,"test","Fast","検証",DateTimeOffset.UtcNow));
        var completed=new Storage.UIベンチマークグリッド側記録(Storage.UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,12.5,64,4);
        var failed=new Storage.UIベンチマークグリッド側記録(Storage.UI実行状態.失敗,"worker failure",DateTimeOffset.UtcNow,null,null,4);
        var leftIdentity=new Storage.UIベンチマークスナップショットケース書込("Left.Assembly","Left.Type","Where","Count=64","[]");
        var rightIdentity=new Storage.UIベンチマークスナップショットケース書込("Right.Assembly","Right.Type","Where","Count=64","[]");
        var centerIdentity=new Storage.UIベンチマークスナップショットケース書込("Center.Assembly","Center.Type","Where","Count=64","[]");

        store.ベンチマークスナップショット項目を保存(run.実行ID,[
            new("Seamless.Persisted.System","Persisted<int>","Where",0,true,completed,failed,leftIdentity,rightIdentity),
            new("System.Array","Array<int>","Where",0,true,completed,completed,leftIdentity,rightIdentity,completed,centerIdentity),
        ]);

        var items=store.ベンチマークスナップショット項目一覧を取得(run.実行ID);
        Assert.Equal(2,items.Count);
        var persisted=Assert.Single(items.Where(item=>item.タブID=="Seamless.Persisted.System"));
        Assert.Null(persisted.中央);
        Assert.Null(persisted.追加);
        Assert.Equal(Storage.UI実行状態.失敗,persisted.右.状態);
        Assert.Equal("worker failure",persisted.右.失敗理由);
        var array=Assert.Single(items.Where(item=>item.タブID=="System.Array"));
        Assert.NotNull(array.中央);
        Assert.Null(array.追加);

        store.実行を削除(run.実行ID);
        Assert.Empty(store.ベンチマークスナップショット項目一覧を取得(run.実行ID));
    }

    private static List<string> 列名を取得(SqliteConnection connection,string table) {
        using var command=connection.CreateCommand();
        command.CommandText=$"SELECT name FROM pragma_table_info('{table}');";
        using var reader=command.ExecuteReader();
        var result=new List<string>();
        while(reader.Read()){
            result.Add(reader.GetString(0));
        }
        return result;
    }

    private static string DBパスを作成() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless","BenchmarkTabStorage",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"Benchmark.db");
    }
}