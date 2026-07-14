using Seamless.UI.Storage;

namespace Test.Benchmark;

public sealed class UI実行ストア{
    [Fact]
    public void スキーマを確認_通常DBを日本語スキーマ版20で作成する(){
        var パス=DBパスを作成();
        new global::Seamless.UI.Storage.UI実行ストア(パス).スキーマを確認();
        using var 接続=開く(パス);
        Assert.Equal(20L,スカラー<long>(接続,"PRAGMA user_version;"));
        Assert.Equal(3L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','ベンチマーク','診断カウンター');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('Runs','Benchmark','Apps');"));
        Assert.Equal(7L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('アセンブリ名','型完全名','メソッド名','引数キー','引数JSON','左ループ回数','右ループ回数');"));
        Assert.Equal(2L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('履歴時間シェア','履歴メモリシェア');"));
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
    public void 実行設定を保存_状態メッセージと要素数を復元する(){
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
        Assert.Equal("再開待ち",読取実行.状態メッセージ);
        using var 接続=開く(ストア.データベースパス);
        Assert.Equal("Paused",スカラー<string>(接続,"SELECT \"状態\" FROM \"実行\";"));
    }

    [Fact]
    public void ベンチマークスナップショット項目を保存_識別情報とシェアを往復する(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        var 識別=new UIベンチマークスナップショットケース書込("Left.Assembly","Sample.Type","Any","Count=100","[{\"名前\":\"Count\",\"値\":\"100\"}]");
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[new("System.Array","int","Any",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8),識別,識別)]);
        var 項目=Assert.Single(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
        Assert.Equal(20,項目.左.平均ナノ秒);Assert.Equal(8,項目.左.ループ回数);Assert.Equal(識別,項目.左ケース);
        var 時間=Assert.Single(ストア.分析ビューを照会("履歴時間シェア"));
        Assert.Equal(2.0/3.0,Convert.ToDouble(時間["左シェア"]),12);
        var メモリ=Assert.Single(ストア.分析ビューを照会("履歴メモリシェア"));
        Assert.Equal(0.8,Convert.ToDouble(メモリ["左シェア"]),12);
    }

    [Fact]
    public void シェア履歴を取得_型ベンチマーク引数を完全一致させる(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var 対象識別=new UIベンチマークスナップショットケース書込("Assembly","Type","Any","Count=100","[{\"名前\":\"Count\",\"値\":\"100\"}]");
        var 別識別=対象識別 with{引数キー="Count=200",引数JSON="[{\"名前\":\"Count\",\"値\":\"200\"}]"};
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[
            new("System.Array","int","Any",0,true,new(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,10,0),new(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,30,0),対象識別,対象識別),
            new("System.Array","int","Any",0,true,new(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,90,900),new(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,10,100),別識別,別識別),
        ]);
        var 条件=new UIシェア履歴条件(UI実行アプリ種別.ベンチマーク,"System.Array","int","Any",対象識別);
        var 時間=Assert.Single(ストア.シェア履歴を取得(条件,UIシェア指標.時間));
        Assert.Equal(0.25,時間.左シェア,12);Assert.Equal(0.75,時間.右シェア,12);
        var メモリ=Assert.Single(ストア.シェア履歴を取得(条件,UIシェア指標.メモリ));
        Assert.Equal(0,メモリ.左シェア);Assert.Equal(0,メモリ.右シェア);
        Assert.Throws<InvalidDataException>(()=>ストア.シェア履歴を取得(条件 with{ベンチマークケース=null},UIシェア指標.時間));
    }

    [Fact]
    public void スケーラブル_前回との時間メモリシェアを返す(){
        var ストア=new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成(),UI実行アプリ種別.スケーラブルベンチマーク);
        var 識別=new UIベンチマークスナップショットケース書込("","","Add","Count=100","[{\"名前\":\"Count\",\"値\":\"100\"}]");
        var 前回=ストア.実行を作成(new(UI実行アプリ種別.スケーラブルベンチマーク,"前回","Fast","Scalable",DateTimeOffset.UtcNow.AddMinutes(-1)));
        ストア.ベンチマークスナップショット項目を保存(前回.実行ID,[new("Scalable","int","Add",2,true,new(UI実行状態.完了,"",DateTimeOffset.UtcNow,10,20),new(UI実行状態.待機中,"",null,null,null),識別,null)]);
        ストア.実行状態を設定(前回.実行ID,UI実行状態.完了);
        var 今回=ストア.実行を作成(new(UI実行アプリ種別.スケーラブルベンチマーク,"今回","Fast","Scalable",DateTimeOffset.UtcNow));
        ストア.ベンチマークスナップショット項目を保存(今回.実行ID,[new("Scalable","int","Add",2,true,new(UI実行状態.完了,"",DateTimeOffset.UtcNow,8,16),new(UI実行状態.待機中,"",null,null,null),識別,null)]);
        var 点=Assert.Single(ストア.シェア履歴を取得(new(UI実行アプリ種別.スケーラブルベンチマーク,"Scalable","int","Add",識別),UIシェア指標.時間));
        Assert.Equal(8.0/18.0,点.左シェア,12);Assert.Equal(10.0/18.0,点.右シェア,12);
        using var 接続=開く(ストア.データベースパス);
        Assert.Equal(2L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('スケーラブル履歴','スケーラブル前回シェア');"));
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
        ストア.フォールバック診断を保存(実行.実行ID,[new("Project","Method","Counter",1){タブ名="System.Array",引数キー="Count=1"}]);
        Assert.NotEmpty(ストア.フォールバック診断一覧を取得(実行.実行ID));
        ストア.全記録を削除();
        Assert.Empty(ストア.実行一覧を取得());Assert.Empty(ストア.フォールバック診断一覧を取得(実行.実行ID));
    }

    private static Microsoft.Data.Sqlite.SqliteConnection 開く(string パス){var 接続=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=パス}.ToString());接続.Open();return 接続;}
    private static T値 スカラー<T値>(Microsoft.Data.Sqlite.SqliteConnection 接続,string SQL){using var コマンド=接続.CreateCommand();コマンド.CommandText=SQL;return (T値)コマンド.ExecuteScalar()!;}
    private static string DBパスを作成(){var ルート=Path.Combine(AppContext.BaseDirectory,"UI実行ストア試験",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(ルート);return Path.Combine(ルート,"Benchmark.db");}
}
