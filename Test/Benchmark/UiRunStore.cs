using UI.Storage;

namespace Test.Benchmark;

public sealed class UI実行ストア{
    [Fact]
    public void スキーマを確認_通常DBを日本語スキーマ版26で実行表とタブ別11表と11ビューを作成する(){
        var パス=DBパスを作成();
        new global::UI.Storage.UI実行ストア(パス).スキーマを確認();
        using var 接続=開く(パス);
        Assert.Equal(26L,スカラー<long>(接続,"PRAGMA user_version;"));
        Assert.Equal(1L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行';"));
        // 単一の「ベンチマーク」表は版24でタブ別11表へ分割済み。Persisted/Tsavorite は Seamless版・System版の2タブに分かれる。
        Assert.Equal(11L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('書き換え','パイプライン融合','述語プッシュダウン','Array','IEnumerable','Range','List','Persisted(Seamless)','Persisted(System)','Tsavorite(Seamless)','Tsavorite(System)');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('Runs','Benchmark','Apps','診断カウンター','ベンチマーク','Persisted','Tsavorite');"));
        // 版26でタブ別11表それぞれに比率View({表名}View)が追加された。
        Assert.Equal(11L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
        Assert.Equal(1L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name='ArrayView';"));
        // 識別情報は側ごとに独立した列を持つ。1組を共有していた旧スキーマでは左中央右の型完全名が混線した。
        Assert.Equal(4L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name IN('FLinq_アセンブリ名','FLinq_型完全名','ZLinq_アセンブリ名','ZLinq_型完全名');"));
        Assert.Equal(7L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name IN('引数キー','引数JSON','ZLinq_状態','ZLinq_失敗理由','ZLinq_日時','ZLinq_時間ns','ZLinq_メモリバイト');"));
        Assert.Equal(2L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name IN('FLinq_ループ回数','ZLinq_ループ回数');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name IN('Enumerable_アセンブリ名','Enumerable_型完全名','メソッド名','左アセンブリ名','中央型完全名','右ループ回数');"));
        Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name IN('状態メッセージ','適応閾値ns','前回実行ID');"));
        Assert.Equal(1L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name='計測時間ns';"));
    }

    [Fact]
    public void スキーマを確認_旧DBはバックアップ後に全体を再作成し旧データを読まない(){
        var パス=DBパスを作成();
        using(var 接続=開く(パス)){
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="PRAGMA user_version=19;CREATE TABLE Runs(Id INTEGER);INSERT INTO Runs VALUES(1);";
            コマンド.ExecuteNonQuery();
        }

        var ストア=new global::UI.Storage.UI実行ストア(パス);
        ストア.スキーマを確認();

        using(var 接続=開く(パス)){
            Assert.Equal(26L,スカラー<long>(接続,"PRAGMA user_version;"));
            Assert.Equal(11L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','パイプライン融合','述語プッシュダウン','Array','IEnumerable','Range','List','Persisted(Seamless)','Persisted(System)','Tsavorite(Seamless)','Tsavorite(System)');"));
            Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Runs';"));
        }

        var バックアップ=Assert.Single(Directory.GetFiles(Path.GetDirectoryName(パス)!,Path.GetFileName(パス)+".*.bak"));
        using var バックアップ接続=開く(バックアップ);
        Assert.Equal(19L,スカラー<long>(バックアップ接続,"PRAGMA user_version;"));
        Assert.Equal(1L,スカラー<long>(バックアップ接続,"SELECT COUNT(*) FROM Runs;"));
    }

    [Fact]
    public void スキーマを確認_版25でも旧3列形状ならバックアップ後に再作成する(){
        var パス=DBパスを作成();
        new global::UI.Storage.UI実行ストア(パス).スキーマを確認();
        using(var 接続=開く(パス)){
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="ALTER TABLE [Array] ADD COLUMN [Enumerable_アセンブリ名] TEXT;";
            コマンド.ExecuteNonQuery();
        }

        new global::UI.Storage.UI実行ストア(パス).スキーマを確認();

        using(var 接続=開く(パス)){
            Assert.Equal(26L,スカラー<long>(接続,"PRAGMA user_version;"));
            Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name='Enumerable_アセンブリ名';"));
            Assert.Equal(16L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name LIKE 'FLinq_%' OR name LIKE 'ZLinq_%';"));
        }

        var バックアップ=Assert.Single(Directory.GetFiles(Path.GetDirectoryName(パス)!,Path.GetFileName(パス)+".*.bak"));
        using var バックアップ接続=開く(バックアップ);
        Assert.Equal(26L,スカラー<long>(バックアップ接続,"PRAGMA user_version;"));
        Assert.Equal(1L,スカラー<long>(バックアップ接続,"SELECT COUNT(*) FROM pragma_table_info('Array') WHERE name='Enumerable_アセンブリ名';"));
    }
    [Fact]
    public void 実行設定を保存_要素数と計測時間とCPU一覧を復元する(){
        var ストア=new global::UI.Storage.UI実行ストア(DBパスを作成());
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
        var ストア=new global::UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        var 左識別=new UIベンチマークスナップショットケース書込("Left.Assembly","Persisted<int>","Persisted<int>.Aggregate_func","Count=100","[{\"名前\":\"Count\",\"値\":\"100\"}]");
        var 右識別=new UIベンチマークスナップショットケース書込("Right.Assembly","Persisted<int>","Persisted<int>.Aggregate_func","Count=100","[{\"名前\":\"Count\",\"値\":\"100\"}]");
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[new("System.Array","Persisted<int>","Persisted<int>.Aggregate_func",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8),左識別,右識別)]);

        var 項目=Assert.Single(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
        Assert.Equal("Persisted<int>",項目.型名);
        Assert.Equal("Persisted<int>.Aggregate_func",項目.ベンチマーク名);
        Assert.Equal(20,項目.左.平均ナノ秒);
        Assert.Equal(200,項目.左.割当バイト);
        Assert.Equal(8,項目.左.ループ回数);
        Assert.Equal(10,項目.右.平均ナノ秒);
        Assert.Equal(50,項目.右.割当バイト);
        Assert.Equal(左識別,項目.左ケース);
        Assert.Equal(右識別,項目.右ケース);
    }
    [Fact]
    public void ベンチマークスナップショット項目を保存_型が異なる同名メソッドを別行として維持する(){
        var ストア=new global::UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        var 永続化識別=new UIベンチマークスナップショットケース書込("Benchmark.System.Linq.Enumerable.FLinq","Persisted<int>","Persisted<int>.Aggregate_func","","[]");
        var Tsavorite識別=new UIベンチマークスナップショットケース書込("Benchmark.System.Linq.Enumerable.ZLinq","Tsavorite<ClassInt32>","Tsavorite<ClassInt32>.Aggregate_func","","[]");
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[
            new("System.Array","Persisted<int>","Persisted<int>.Aggregate_func",7,true,new(UI実行状態.完了,string.Empty,日時,20,200,1),new(UI実行状態.完了,string.Empty,日時,10,50,1),永続化識別,永続化識別),
            new("System.Array","Tsavorite<ClassInt32>","Tsavorite<ClassInt32>.Aggregate_func",7,true,new(UI実行状態.完了,string.Empty,日時,30,300,1),new(UI実行状態.完了,string.Empty,日時,15,75,1),Tsavorite識別,Tsavorite識別)]);

        var 項目一覧=ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID);
        Assert.Equal(2,項目一覧.Count);
        var 永続化=Assert.Single(項目一覧,項目=>項目.型名=="Persisted<int>");
        var Tsavorite=Assert.Single(項目一覧,項目=>項目.型名=="Tsavorite<ClassInt32>");
        Assert.Equal("Persisted<int>.Aggregate_func",永続化.ベンチマーク名);
        Assert.Equal("Tsavorite<ClassInt32>.Aggregate_func",Tsavorite.ベンチマーク名);
        Assert.Equal(永続化識別,永続化.左ケース);
        Assert.Equal(Tsavorite識別,Tsavorite.左ケース);
    }
    [Fact]
    public void パラメータスイープ_日本語列と英語保存値を往復する(){
        var パス=DBパスを作成();
        var ストア=new global::UI.Storage.UI実行ストア(パス,UI実行アプリ種別.パラメータスイープ);
        var 日時=DateTimeOffset.Parse("2026-09-25T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.パラメータスイープ,"スイープ","Fast","ParameterSweep",日時));
        // 各列に異なる値を入れ、32 bit を超える値とゼロも null と区別して往復する。
        var 観測あり=new UIパラメータスイープ結果(実行.実行ID,"Case","HashSet","Int32","Memory","IndexSize",64,32,16,1024,0.5,4096,4096,"Completed","",12.5,8.25,日時,4294967296,17,8589934599,0);
        // 末尾引数の省略は既存の呼び出しと同じ。新しい4項目は null で保存する。
        var 観測なし=new UIパラメータスイープ結果(実行.実行ID,"NoStatistics","HashSet","Int32","Memory","IndexSize",64,32,16,1024,0.5,0,0,"InvalidConfiguration","invalid",0,null,日時);
        ストア.パラメータスイープ結果を保存(観測あり);
        ストア.パラメータスイープ結果を保存(観測なし);

        var 再開ストア=new global::UI.Storage.UI実行ストア(パス,UI実行アプリ種別.パラメータスイープ);
        var 結果一覧=再開ストア.パラメータスイープ結果一覧を取得(実行.実行ID);
        Assert.Equal(2,結果一覧.Count);
        Assert.Equal(観測あり,結果一覧[0]);
        Assert.Equal(観測なし,結果一覧[1]);
        Assert.Equal("Completed",結果一覧[0].失敗種別);
        Assert.Equal(4096,結果一覧[0].最大成功Count);
        using(var 接続=開く(パス)){
            Assert.Equal(21L,スカラー<long>(接続,"PRAGMA user_version;"));
            Assert.Equal(15L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('コレクション','ジェネリック','バックエンド','スイープ','Indexサイズ','Pageサイズ','Segmentサイズ','Memoryサイズ','可変Fraction','最大成功Count','エントリCount','索引バケット数','溢れバケット数','メモリログバイト数','ディスクログバイト数');"));
            Assert.Equal(4L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('索引バケット数','溢れバケット数','メモリログバイト数','ディスクログバイト数') AND type='INTEGER' AND [notnull]=0;"));
            Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM sqlite_master WHERE type='view';"));
            // このテスト専用DBを旧版20の形に戻す。実データの自動移行を要求しない。
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="""
                ALTER TABLE "ベンチマーク" DROP COLUMN "索引バケット数";
                ALTER TABLE "ベンチマーク" DROP COLUMN "溢れバケット数";
                ALTER TABLE "ベンチマーク" DROP COLUMN "メモリログバイト数";
                ALTER TABLE "ベンチマーク" DROP COLUMN "ディスクログバイト数";
                PRAGMA user_version=20;
                """;
            コマンド.ExecuteNonQuery();
        }
        var 旧版ストア=new global::UI.Storage.UI実行ストア(パス,UI実行アプリ種別.パラメータスイープ);
        Assert.Throws<InvalidOperationException>(()=>旧版ストア.スキーマを確認());
        using(var 接続=開く(パス)){
            Assert.Equal(20L,スカラー<long>(接続,"PRAGMA user_version;"));
            Assert.Equal(2L,スカラー<long>(接続,"SELECT COUNT(*) FROM [ベンチマーク];"));
            Assert.Equal(0L,スカラー<long>(接続,"SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('索引バケット数','溢れバケット数','メモリログバイト数','ディスクログバイト数');"));
            // 版番号だけを変えても、観測列が欠けた形状は拒否する。
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="PRAGMA user_version=21;";
            コマンド.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(()=>new global::UI.Storage.UI実行ストア(パス,UI実行アプリ種別.パラメータスイープ).スキーマを確認());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(パス)!,Path.GetFileName(パス)+".*.bak"));
    }

    [Fact]
    public void 全記録を削除_関連記録を削除する(){
        var ストア=new global::UI.Storage.UI実行ストア(DBパスを作成());
        var 実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        ストア.ベンチマークスナップショット項目を保存(実行.実行ID,[new("System.Array","int","Any",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8))]);
        Assert.NotEmpty(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
        ストア.全記録を削除();
        Assert.Empty(ストア.実行一覧を取得());Assert.Empty(ストア.ベンチマークスナップショット項目一覧を取得(実行.実行ID));
    }

    [Fact]
    public void 全記録を削除_保持実行ID指定時はその実行と関連記録だけ残す(){
        var ストア=new global::UI.Storage.UI実行ストア(DBパスを作成());
        var 保持対象=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 削除対象=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        ストア.ベンチマークスナップショット項目を保存(保持対象.実行ID,[new("System.Array","int","Any",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8))]);
        ストア.ベンチマークスナップショット項目を保存(削除対象.実行ID,[new("System.Array","int","Any",17,true,
            new(UI実行状態.完了,string.Empty,日時,20,200,8),new(UI実行状態.完了,string.Empty,日時,10,50,8))]);

        ストア.全記録を削除(保持対象.実行ID);

        var 残存実行=Assert.Single(ストア.実行一覧を取得());
        Assert.Equal(保持対象.実行ID,残存実行.実行ID);
        Assert.NotEmpty(ストア.ベンチマークスナップショット項目一覧を取得(保持対象.実行ID));
        Assert.Empty(ストア.ベンチマークスナップショット項目一覧を取得(削除対象.実行ID));
    }

    [Fact]
    public void ArrayView_最新実行の型ベンチマーク比率を比率降順で返す(){
        var ストア=new global::UI.Storage.UI実行ストア(DBパスを作成());
        var 旧実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        var 日時=DateTimeOffset.Parse("2026-07-08T12:34:56+09:00",global::System.Globalization.CultureInfo.InvariantCulture);
        ストア.ベンチマークスナップショット項目を保存(旧実行.実行ID,[new("System.Array","OldType","OldBench",0,true,
            new(UI実行状態.完了,string.Empty,日時,999,200,8),new(UI実行状態.完了,string.Empty,日時,1,50,8))]);
        var 新実行=ストア.実行を作成(new(UI実行アプリ種別.ベンチマーク,"表示","Fast","System.Array",DateTimeOffset.UtcNow));
        ストア.ベンチマークスナップショット項目を保存(新実行.実行ID,[
            new("System.Array","Low","LowBench",0,true,new(UI実行状態.完了,string.Empty,日時,6,200,8),new(UI実行状態.完了,string.Empty,日時,94,50,8)),
            new("System.Array","High","HighBench",0,true,new(UI実行状態.完了,string.Empty,日時,94,200,8),new(UI実行状態.完了,string.Empty,日時,6,50,8)),
        ]);

        var 行一覧=ストア.分析ビューを照会("ArrayView");

        Assert.Equal(2,行一覧.Count);
        Assert.DoesNotContain(行一覧,行=>(string?)行["型"]=="OldType");
        Assert.Equal("High",行一覧[0]["型"]);
        Assert.Equal(0.94,Assert.IsType<double>(行一覧[0]["比率"]),3);
        Assert.Equal("Low",行一覧[1]["型"]);
        Assert.Equal(0.06,Assert.IsType<double>(行一覧[1]["比率"]),3);
    }

    private static Microsoft.Data.Sqlite.SqliteConnection 開く(string パス){var 接続=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=パス}.ToString());接続.Open();return 接続;}
    private static T値 スカラー<T値>(Microsoft.Data.Sqlite.SqliteConnection 接続,string SQL){using var コマンド=接続.CreateCommand();コマンド.CommandText=SQL;return (T値)コマンド.ExecuteScalar()!;}
    private static string DBパスを作成(){var ルート=Path.Combine(AppContext.BaseDirectory,"UI実行ストア試験",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(ルート);return Path.Combine(ルート,"Benchmark.db");}
}
