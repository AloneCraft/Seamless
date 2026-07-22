using Microsoft.Data.Sqlite;
using Storage = Seamless.UI.Storage;

namespace Test.Benchmark;

public class BenchmarkTabStorage {
    [Fact]
    public void 通常Benchmarkのスキーマは9タブ表と見出し由来列を作成する() {
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
        Assert.All(new[]{"書き換え","パイプライン融合","述語プッシュダウン","Array","IEnumerable","Range","List","Persisted","Tsavorite"}, table=>Assert.Contains(table,tables));

        foreach(var table in new[]{"書き換え","パイプライン融合","述語プッシュダウン","Array","IEnumerable","Range","List","Persisted","Tsavorite"}) {
            using var foreignKeyCommand=connection.CreateCommand();
            foreignKeyCommand.CommandText=$"SELECT on_delete FROM pragma_foreign_key_list('{table}') WHERE \"table\"='実行';";
            Assert.Equal("CASCADE",Assert.IsType<string>(foreignKeyCommand.ExecuteScalar()));
        }
        using var columns=connection.CreateCommand();
        columns.CommandText="SELECT name FROM pragma_table_info('Persisted');";
        using var columnReader=columns.ExecuteReader();
        var names=new List<string>();
        while(columnReader.Read()) names.Add(columnReader.GetString(0));
        Assert.Contains("FLinq(IEnumerable)_時間ns",names);
        Assert.Contains("FLinq(Persisted)_型完全名",names);
        Assert.True(names.IndexOf("FLinq(Persisted)_アセンブリ名")<names.IndexOf("ValueEnumerable_アセンブリ名"));
        Assert.True(names.IndexOf("ValueEnumerable_アセンブリ名")<names.IndexOf("FLinq(IEnumerable)_アセンブリ名"));
        Assert.True(names.IndexOf("FLinq(IEnumerable)_アセンブリ名")<names.IndexOf("System.Linq_アセンブリ名"));
    }

    [Fact]
    public void Persistedの4列目は保存後に読み込める() {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        var run=store.実行を作成(new(Storage.UI実行アプリ種別.ベンチマーク,"test","Fast","Seamless永続化",DateTimeOffset.UtcNow));
        var completed=new Storage.UIベンチマークグリッド側書込(Storage.UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,12.5,64,4);
        var identity=new Storage.UIベンチマークスナップショットケース書込("Assembly","Sample.Type","Where","Count=64","[]");
        store.ベンチマークスナップショット項目を保存(run.実行ID,[new("Seamless.Persisted","Persisted<int>","Where",0,true,completed,completed,identity,identity,completed,identity,completed,identity)]);

        var item=Assert.Single(store.ベンチマークスナップショット項目一覧を取得(run.実行ID));
        Assert.NotNull(item.中央);
        Assert.NotNull(item.追加);
        Assert.Equal(12.5,item.追加!.平均ナノ秒);
        Assert.Equal("Sample.Type",item.追加ケース!.型完全名);
        store.実行を削除(run.実行ID);
        Assert.Empty(store.ベンチマークスナップショット項目一覧を取得(run.実行ID));
    }

    [Fact]
    public void PersistedのFLinqIEnumerable失敗は理由付きで往復する() {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        var run=store.実行を作成(new(Storage.UI実行アプリ種別.ベンチマーク,"test","Fast","Seamless永続化",DateTimeOffset.UtcNow));
        var completed=new Storage.UIベンチマークグリッド側書込(Storage.UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,12.5,64,4);
        const string failureReason="Worker assembly missing: Benchmark IEnumerable FLinq";
        var failed=new Storage.UIベンチマークグリッド側書込(Storage.UI実行状態.失敗,failureReason,DateTimeOffset.UtcNow,null,null,4);
        var identity=new Storage.UIベンチマークスナップショットケース書込("Assembly","Sample.Type","Where","Count=64","[]");
        store.ベンチマークスナップショット項目を保存(run.実行ID,[new("Seamless.Persisted","Persisted<int>","Where",0,true,completed,completed,identity,identity,completed,identity,failed,identity)]);

        var item=Assert.Single(store.ベンチマークスナップショット項目一覧を取得(run.実行ID));

        Assert.NotNull(item.追加);
        Assert.Equal(Storage.UI実行状態.失敗,item.追加!.状態);
        Assert.Equal(failureReason,item.追加.失敗理由);
        Assert.Null(item.追加.平均ナノ秒);
    }
    [Theory]
    [InlineData("Optimization.Rewrite",false)]
    [InlineData("System.Array",true)]
    public void 二列と三列も見出し順で往復できる(string タブID,bool 中央あり) {
        var path=DBパスを作成();
        var store=new Storage.UI実行ストア(path);
        var run=store.実行を作成(new(Storage.UI実行アプリ種別.ベンチマーク,"test","Fast","System配列",DateTimeOffset.UtcNow));
        var completed=new Storage.UIベンチマークグリッド側書込(Storage.UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,12.5,64,4);
        var identity=new Storage.UIベンチマークスナップショットケース書込("Assembly","Sample.Type","Where","Count=64","[]");
        store.ベンチマークスナップショット項目を保存(run.実行ID,[new(タブID,"Sample","Where",0,true,completed,completed,identity,identity,中央あり ? completed : null,中央あり ? identity : null)]);

        var item=Assert.Single(store.ベンチマークスナップショット項目一覧を取得(run.実行ID));
        Assert.Equal(中央あり,item.中央 is not null);
        Assert.Null(item.追加);
    }
    private static string DBパスを作成() {
        var directory=Path.Combine(Path.GetTempPath(),"Seamless","BenchmarkTabStorage",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"Benchmark.db");
    }
}