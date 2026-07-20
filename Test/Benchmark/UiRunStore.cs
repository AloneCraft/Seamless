using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UI実行ストア{
    [Fact]
    public void スキーマを確認_通常DBを日本語スキーマ版23で2テーブル作成する(){
        var パス=DBパスを作成();
        new global::Seamless.UI.Storage.UI実行ストア(パス).スキーマを確認();
        using var 接続=開く(パス);
        Assert.Equal(23L,スカラー<long>(接続,"PRAGMA user_version;"));
        Assert.Equal(2L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','ベンチマーク');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('Runs','Benchmark','Apps','診断カウンター');"));
        Assert.Equal(16L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('左アセンブリ名','左型完全名','中央アセンブリ名','中央型完全名','右アセンブリ名','右型完全名','引数キー','引数JSON','左ループ回数','中央状態','中央失敗理由','中央日時','中央時間ns','中央メモリバイト','中央ループ回数','右ループ回数');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name='メソッド名';"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name IN('状態メッセージ','適応閾値ns','前回実行ID');"));
        Assert.Equal(1L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name='計測時間ns';"));
    }

    [Fact]
    public void スキーマを確認_旧DBを暗黙変換しない(){
        var パス=DBパスを作成();
        using(var 接続=開く(パス)){using var コマンド=接続.CreateCommand();コマンド.CommandText="PRAGMA user_version=19;CREATE TABLE Runs(Id INTEGER);";コマンド.ExecuteNonQuery();}
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(パス);
        var 例外=Assert.Throws<InvalidOperationException>(ストア.スキーマを確認);
        Assert.Contains("バックアップ",例外.Message,StringComparison.Ordinal);
        using var 読取接続=開く(パス);
        Assert.Equal(19L,スカラー<long>(読取接続,"PRAGMA user_version;"));
        Assert.Equal(1L,スカラー<long>(読取接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Runs';"));
    }

    [Fact]
    public void 実行設定を保存_要素数と計測時間とCPU一覧を復元する(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        ストア.実行設定を保存(実行.実行ID,"Root",[1024,4096],1000,[0,2]);
        ストア.実行状態を設定(実行.実行ID,UI実行状態.一時停止中,"再開待ち");
        var 設定=Assert.IsType<UI実行設定記録>(ストア.実行設定を取得(実行.実行ID));
        Assert.Equal("1024,4096",設定.要素数一覧);
        Assert.Equal(1000,設定.適応閾値ナノ秒);
        Assert.Equal("0,2",設定.論理CPU一覧);
        var 読取実行=Assert.Single(ストア.実行一覧を取得());
        Assert.Equal(UI実行状態.一時停止中,読取実行.状態);
        using var 接続=開く(ストア.データベースパス);
        Assert.Equal("Paused",スカラー<string>(接続,"SELECT \"状態\" FROM \"実行\";"));
        Assert.Equal(1000d,スカラー<double>(接続,"SELECT \"計測時間ns\" FROM \"実行\";"));
    }

    [Fact]
    public void ベンチマークスナップショット項目を保存_識別情報とシェアを往復する(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        var 識別=new UIベンチマークスナップショットケース書込("Left.Assembly","Sample.Type","Any","Count=100","[{\"名前\":\"Count\",\"値\":\"100\"}]");
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[new("System.Array","int","Any",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8),識別,識別,new(UI実行状態.完了,string.Empty,日時,15,75,8),識別)]);
        var 項目=Assert.Single(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
        Assert.Equal(20,項目.左.平均ナノ秒);Assert.Equal(8,項目.左.ループ回数);Assert.Equal(識別,項目.左ケース);
        Assert.Equal(15,Assert.IsType<UIベンチマークグリッド側記録>(項目.中央).平均ナノ秒);
        Assert.Equal(識別,項目.中央ケース);
        Assert.Equal(10,項目.右.平均ナノ秒);
    }

    [Fact]
    public void ベンチマークスナップショット項目を保存_左中央右で型が異なっても個別に往復する(){
        // Tsavorite等の実タブでは左(FLinq)/中央(ValueLinq)/右(System.Linq)がそれぞれ別アセンブリ・別クラスであるため、
        // 型完全名が側ごとに異なる状態を正しく保存・復元できることを確認する(旧スキーマでは1組しか保存できず混線していた)。
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","Seamless.Tsavorite",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        var 左識別=new UIベンチマークスナップショットケース書込("Benchmark.Tsavorite.Linq.Enumerable.FLinq","Benchmark.Tsavorite.Linq.Enumerable.FLinqClassInt32","AggregateBy_keySelector","","[]");
        var 中央識別=new UIベンチマークスナップショットケース書込("Benchmark.Tsavorite.Linq.Enumerable.ValueLinq","Benchmark.Tsavorite.Linq.Enumerable.ValueLinqClassInt32","AggregateBy_keySelector","","[]");
        var 右識別=new UIベンチマークスナップショットケース書込("Benchmark.Tsavorite.Linq.Enumerable.Linq","Benchmark.Tsavorite.Linq.Enumerable.LinqClassInt32","AggregateBy_keySelector","","[]");
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[new("Seamless.Tsavorite","Tsavorite<ClassInt32>","AggregateBy_keySelector",7,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,1),new(UI実行状態.完了,string.Empty,日時,10,50,1),左識別,右識別,new(UI実行状態.完了,string.Empty,日時,15,75,1),中央識別)]);

        var 項目=Assert.Single(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
        Assert.Equal(左識別,項目.左ケース);
        Assert.Equal(中央識別,項目.中央ケース);
        Assert.Equal(右識別,項目.右ケース);
        Assert.NotEqual(項目.左ケース?.型完全名,項目.中央ケース?.型完全名);
        Assert.NotEqual(項目.左ケース?.型完全名,項目.右ケース?.型完全名);
    }



    [Fact]
    public void パラメータスイープ_日本語列と英語保存値を往復する(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成(),UI実行アプリ種別.パラメータスイープ);
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.パラメータスイープ,"スイープ","Fast","ParameterSweep",DateTimeOffset.UtcNow));
        ストア.パラメータスイープ結果を保存(new(実行.実行ID,"Case","HashSet","Int32","Memory","IndexSize",64,32,16,1024,0.5,4096,4096,"Completed","",12.5,8.25,DateTimeOffset.UtcNow));
        var 結果=Assert.Single(ストア.パラメータスイープ結果一覧を取得(実行.実行ID));
        Assert.Equal("Completed",結果.失敗種別);Assert.Equal(4096,結果.最大成功Count);
        using var 接続=開く(ストア.データベースパス);
        Assert.Equal(11L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('コレクション','ジェネリック','バックエンド','スイープ','Indexサイズ','Pageサイズ','Segmentサイズ','Memoryサイズ','可変Fraction','最大成功Count','エントリCount');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
    }

    [Fact]
    public void 全記録を削除_関連記録を削除する(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[new("System.Array","int","Any",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8))]);
        Assert.NotEmpty(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
        ストア.全記録を削除();
        Assert.Empty(ストア.実行一覧を取得());Assert.Empty(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
    }

    private static Microsoft.Data.Sqlite.SqliteConnection 開く(string パス){var 接続=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=パス}.ToString());接続.Open();return 接続;}
    private static T値 スカラー<T値>(Microsoft.Data.Sqlite.SqliteConnection 接続,string SQL){using var コマンド=接続.CreateCommand();コマンド.CommandText=SQL;return (T値)コマンド.ExecuteScalar()!;}
    private static string DBパスを作成(){var ルート=Path.Combine(AppContext.BaseDirectory,"UI実行ストア試験",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(ルート);return Path.Combine(ルート,"Benchmark.db");}
}
