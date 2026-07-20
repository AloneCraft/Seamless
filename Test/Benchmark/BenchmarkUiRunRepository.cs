using Seamless.UI.Storage;

namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマークUI実行リポジトリ {
    [Fact]
    public async Task 仕様確認01() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.ベンチマーク,"Benchmark","Fast",ソースタブ.タブ);
        var 結果 = 結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Aggregate",64,123.5,"Sample.Type","Count=64",[new global::UI.ベンチマーク引数("Count","64")]);
        リポジトリ.実行設定保存を予約(実行ID,string.Empty,[64],TimeSpan.Zero,[]);

        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[
            スナップショットを作成(実行ID!.Value,ソースタブ,"int","Aggregate",7,完了側を作成(結果),空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"Sample.Type","Aggregate")),
        ]);

        await リポジトリ.保存完了を待機Async();
        Assert.Contains(ストア.指標一覧を取得(実行ID!.Value),static 項目 => 項目.指標名=="平均ナノ秒");
        Assert.Equal("int",Assert.Single(リポジトリ.ベンチマークスナップショット項目一覧を読み込む(実行ID.Value)).型名);
        var 読込結果 = リポジトリ.結果項目一覧を読み込む(実行ID!.Value,ソースタブ)
            .Single(static 項目 => 項目.平均ナノ秒 is not null);
        Assert.Equal(ソースタブ.左プロジェクト,読込結果.プロジェクト);
        Assert.Equal("Sample.Type",読込結果.型完全名);
        Assert.Equal(global::UI.ベンチマーク作業軸.要素数を解決(ソースタブ.タブ,64),読込結果.要素数);
        Assert.Equal(global::UI.ベンチマーク作業軸.ループ回数を解決(ソースタブ.タブ,64),読込結果.ループ回数);
        Assert.Equal(123.5,読込結果.平均ナノ秒);
        Assert.Equal(123.5*global::UI.ベンチマーク作業軸.ループ回数を解決(ソースタブ.タブ,64),読込結果.合計ナノ秒);
        Assert.Equal(7,読込結果.割り当て論理CPU);
    }

    [Fact]
    public async Task 仕様確認02() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System範囲);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.ベンチマーク,"Benchmark","Fast",ソースタブ.タブ);
        var スナップショット = スナップショットを作成(
            実行ID!.Value,
            ソースタブ,
            string.Empty,
            "ThenBy_keySelector",
            7,
            空側を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Sample.Type","ThenBy_keySelector"),
            空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"Sample.Type","ThenBy_keySelector"));

        var 例外 = Assert.Throws<InvalidDataException>(() => リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[スナップショット]));

        Assert.Contains("System.Range",例外.Message,StringComparison.Ordinal);
        Assert.Contains("ThenBy_keySelector",例外.Message,StringComparison.Ordinal);
        Assert.Empty(ストア.ベンチマークスナップショット項目一覧を取得(実行ID.Value));
    }

    [Fact]
    public async Task 仕様確認03() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.ベンチマーク,"Benchmark","Fast",ソースタブ.タブ);
        リポジトリ.実行設定保存を予約(実行ID,string.Empty,[64],TimeSpan.Zero,[]);

        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[
            スナップショットを作成(実行ID!.Value,ソースタブ,"Sample.Type","Aggregate",7,完了側を作成(結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Aggregate",64,100,"Sample.Type","Count=64",[new global::UI.ベンチマーク引数("Count","64")])),空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"Sample.Type","Aggregate")),
        ]);
        await リポジトリ.保存完了を待機Async();
        Assert.Contains(ストア.指標一覧を取得(実行ID!.Value),static 項目 => 項目.指標名=="平均ナノ秒"&&Math.Abs(項目.数値-100)<0.001);

        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[
            スナップショットを作成(実行ID!.Value,ソースタブ,"Sample.Type","Aggregate",7,完了側を作成(結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Aggregate",64,250,"Sample.Type","Count=64",[new global::UI.ベンチマーク引数("Count","64")])),空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"Sample.Type","Aggregate")),
        ]);

        await リポジトリ.保存完了を待機Async();
        var 指標一覧 = ストア.指標一覧を取得(実行ID!.Value).Where(static 項目 => 項目.指標名=="平均ナノ秒").ToArray();
        Assert.Single(指標一覧);
        Assert.Equal(250,指標一覧[0].数値);
    }

    [Fact]
    public async Task 結果項目一覧読込は実行型識別情報を保持する() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成(),UI実行アプリ種別.スケーラブルベンチマーク);
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.スケーラブルTsavoriteセット);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.スケーラブルベンチマーク,"Scalable","Fast",ソースタブ.タブ);
        var 結果 = 結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Add",262144,500,"SetInt32",string.Empty,[]);
        リポジトリ.実行設定保存を予約(実行ID,string.Empty,[262144],TimeSpan.Zero,[]);

        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[
            スナップショットを作成(実行ID!.Value,ソースタブ,"SetInt32","Add",7,完了側を作成(結果),空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"SetInt32","Add")),
        ]);

        await リポジトリ.保存完了を待機Async();
        Assert.Contains(ストア.指標一覧を取得(実行ID!.Value),static 項目 => 項目.指標名=="平均ナノ秒");
        var 読込結果 = リポジトリ.結果項目一覧を読み込む(実行ID!.Value,ソースタブ)
            .Single(static 項目 => 項目.平均ナノ秒 is not null);
        Assert.Equal("SetInt32",読込結果.型完全名);
        Assert.Equal("Add",読込結果.メソッド);
        Assert.Equal(string.Empty,読込結果.引数キー);
    }

    [Fact]
    public async Task 仕様確認04() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.ベンチマーク,"Benchmark","Fast",ソースタブ.タブ);
        var スナップショット = スナップショットを作成(
            実行ID!.Value,
            ソースタブ,
            "Sample.Type",
            "Aggregate",
            7,
            完了側を作成(結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Aggregate",64,123.5,"Sample.Type","Count=64",[new global::UI.ベンチマーク引数("Count","64")])),
            空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"Sample.Type","Aggregate"));

        リポジトリ.実行削除を予約(実行ID);
        リポジトリ.実行設定保存を予約(実行ID,string.Empty,[64],TimeSpan.Zero,[]);
        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[スナップショット]);

        await リポジトリ.保存完了を待機Async();

        Assert.Empty(ストア.実行一覧を取得(UI実行アプリ種別.ベンチマーク));
        Assert.Empty(ストア.ベンチマークスナップショット項目一覧を取得(実行ID.Value));
        Assert.Empty(ストア.指標一覧を取得(実行ID.Value));
    }

    [Fact]
    public async Task 仕様確認05() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成(),UI実行アプリ種別.スケーラブルベンチマーク);
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.スケーラブルTsavoriteセット);
        const string 型完全名 = "Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32";
        var 引数一覧 = new[]{
            new global::UI.ベンチマーク引数("Count","262144"),
            new global::UI.ベンチマーク引数("Index","64"),
            new global::UI.ベンチマーク引数("Page","32"),
            new global::UI.ベンチマーク引数("Segment","16"),
            new global::UI.ベンチマーク引数("Memory","1024"),
            new global::UI.ベンチマーク引数("Fraction","0.5"),
        };
        const string 引数キー = "Count=262144,Index=64,Page=32,Segment=16,Memory=1024,Fraction=0.5";
        var 初回実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.スケーラブルベンチマーク,"先頭","Fast",ソースタブ.タブ);
        var 結果 = 結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Add",262144,500,型完全名,引数キー,引数一覧);
        リポジトリ.ベンチマークスナップショット項目保存を予約(初回実行ID,[スナップショットを作成(初回実行ID!.Value,ソースタブ,"int","Add",7,完了側を作成(結果),空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"int","Add"))]);
        await リポジトリ.保存完了を待機Async();
        ストア.実行状態を設定(初回実行ID.Value,UI実行状態.完了);

        var 今回実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.スケーラブルベンチマーク,"今回","Fast",ソースタブ.タブ);
        var 前回 = リポジトリ.前回結果を検索(UI実行アプリ種別.スケーラブルベンチマーク,ソースタブ,"Add",ソースタブ.左プロジェクト,型完全名,262144,null,引数キー,今回実行ID);

        Assert.NotNull(前回);
        Assert.Equal(500,前回.平均ナノ秒);
        Assert.Equal(引数キー,前回.引数キー);
        Assert.Equal(初回実行ID,ストア.前回実行IDを取得(今回実行ID!.Value));
    }

    // 最適化パイプライン融合タブは左右とも型接頭辞(Benchmark.Optimization.PipelineFusion.)が
    // 設定されているため、型完全名が空だと ケースを含むか の StartsWith 判定で確実に不一致になる。
    // (System配列タブは型接頭辞が空でどんな型完全名でも一致してしまうため再現に使えない)
    private const string 型接頭辞付きテスト型完全名 = "Benchmark.Optimization.PipelineFusion.SampleType";

    [Fact]
    public async Task ベンチマークスナップショット項目保存_片側の型完全名が空でも有効な型を共有する() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.最適化パイプライン融合);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.ベンチマーク,"Benchmark","Fast",ソースタブ.タブ);
        var 右結果 = 結果を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,"Aggregate",64,123.5,型接頭辞付きテスト型完全名,"Count=64",[new global::UI.ベンチマーク引数("Count","64")]);

        // 左サイドはケース自体はあるが型完全名を解決できなかった(空文字)行を模擬する。
        // 行の表示型は右サイドから解決できているため保存前ガードはすり抜ける。
        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[
            スナップショットを作成(実行ID!.Value,ソースタブ,型接頭辞付きテスト型完全名,"Aggregate",7,
                空側を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,string.Empty,"Aggregate",ソースタブ.左アセンブリ名),
                完了側を作成(右結果,ソースタブ.右アセンブリ名)),
        ]);
        await リポジトリ.保存完了を待機Async();

        var スキップ理由一覧 = new List<string>();
        var 読込項目 = Assert.Single(リポジトリ.ベンチマークスナップショット項目一覧を読み込む(実行ID.Value,スキップ理由一覧.Add));
        Assert.Empty(スキップ理由一覧);
        Assert.Equal(型接頭辞付きテスト型完全名,読込項目.左.結果項目.型完全名);
        Assert.Equal(型接頭辞付きテスト型完全名,読込項目.右.結果項目.型完全名);
    }

    [Fact]
    public async Task ベンチマークスナップショット項目一覧読込は不正な行をスキップして残りを返す() {
        var ストア = new global::Seamless.UI.Storage.UI実行ストア(DBパスを作成());
        var リポジトリ = new global::UI.ベンチマークUI実行リポジトリ(ストア);
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.最適化パイプライン融合);
        var 実行ID = await リポジトリ.実行を作成Async(UI実行アプリ種別.ベンチマーク,"Benchmark","Fast",ソースタブ.タブ);
        var 有効結果 = 結果を作成(ソースタブ.左プロジェクト,ソースタブ.タブ,"Aggregate",64,123.5,型接頭辞付きテスト型完全名,"Count=64",[new global::UI.ベンチマーク引数("Count","64")]);
        リポジトリ.ベンチマークスナップショット項目保存を予約(実行ID,[
            スナップショットを作成(実行ID!.Value,ソースタブ,型接頭辞付きテスト型完全名,"Aggregate",7,
                完了側を作成(有効結果,ソースタブ.左アセンブリ名),
                空側を作成(ソースタブ.右プロジェクト,ソースタブ.タブ,型接頭辞付きテスト型完全名,"Aggregate",ソースタブ.右アセンブリ名)),
        ]);
        await リポジトリ.保存完了を待機Async();

        // 型完全名がこのソースタブの型接頭辞と一致しない、真に解決不能な壊れた行を直接注入して再現する
        // (行の表示型自体が誤っているため、型完全名フォールバックでも救済されないケース)。
        var 壊れた識別 = new UIベンチマークスナップショットケース書込(string.Empty,"Totally.Unrelated.Type","Broken",string.Empty,"[]");
        ストア.ベンチマークスナップショット項目を保存(実行ID.Value,[
            new(ソースタブ.ID,"Totally.Unrelated.Type","Broken",null,true,
                new(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,10,10,8),
                new(UI実行状態.完了,string.Empty,DateTimeOffset.UtcNow,10,10,8),
                壊れた識別,壊れた識別),
        ]);

        var スキップ理由一覧 = new List<string>();
        var 項目一覧 = リポジトリ.ベンチマークスナップショット項目一覧を読み込む(実行ID.Value,スキップ理由一覧.Add);

        var 読込項目 = Assert.Single(項目一覧);
        Assert.Equal("Aggregate",読込項目.ベンチマーク名);
        Assert.Single(スキップ理由一覧);
        Assert.Contains("Broken",スキップ理由一覧[0],StringComparison.Ordinal);
    }

    private static global::UI.ベンチマークグリッドスナップショット項目 スナップショットを作成(
        long 実行ID,
        global::UI.ベンチマークソースタブ定義 ソースタブ,
        string 型名,
        string ベンチマーク名,
        int? CPU番号,
        global::UI.ベンチマークグリッドスナップショット側 左,
        global::UI.ベンチマークグリッドスナップショット側 右) =>
        new(実行ID,0,ソースタブ.ID,型名,ベンチマーク名,CPU番号,true,左,右);

    private static global::UI.ベンチマークグリッドスナップショット側 完了側を作成(global::UI.ベンチマーク結果項目 結果) =>
        完了側を作成(結果,"Assembly");

    private static global::UI.ベンチマークグリッドスナップショット側 完了側を作成(global::UI.ベンチマーク結果項目 結果,string アセンブリ名) =>
        new(
            UI実行状態.完了,
            string.Empty,
            結果.開始日時,
            結果.平均ナノ秒,
            結果.割り当てバイト数値,
            結果,
            new global::UI.ベンチマークケース(結果.プロジェクト,アセンブリ名,結果.型完全名,結果.メソッド,結果.引数キー,結果.引数一覧));

    private static global::UI.ベンチマークグリッドスナップショット側 空側を作成(
        string プロジェクト,
        global::UI.ベンチマークソースタブ ソースタブ,
        string 型名,
        string ベンチマーク名) =>
        空側を作成(プロジェクト,ソースタブ,型名,ベンチマーク名,"Assembly");

    private static global::UI.ベンチマークグリッドスナップショット側 空側を作成(
        string プロジェクト,
        global::UI.ベンチマークソースタブ ソースタブ,
        string 型名,
        string ベンチマーク名,
        string アセンブリ名) =>
        new(
            UI実行状態.待機中,
            string.Empty,
            null,
            null,
            null,
            結果を作成(プロジェクト,ソースタブ,ベンチマーク名,0,0,型名,string.Empty,[]),
            new global::UI.ベンチマークケース(プロジェクト,アセンブリ名,型名,ベンチマーク名));

    private static global::UI.ベンチマーク結果項目 結果を作成(
        string プロジェクト,
        global::UI.ベンチマークソースタブ ソースタブ,
        string メソッド,
        int 要素数,
        double 平均,
        string 型完全名,
        string 引数キー,
        IReadOnlyList<global::UI.ベンチマーク引数>? 引数一覧 = null) {
        var ループ回数 = global::UI.ベンチマーク作業軸.ループ回数を解決(ソースタブ,要素数);
        return new(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            メソッド名=メソッド,
            型完全名=型完全名,
            ソースタブ=ソースタブ,
            要素数=global::UI.ベンチマーク作業軸.要素数を解決(ソースタブ,要素数),
            ループ回数=ループ回数,
            引数キー=引数キー,
            引数一覧=引数一覧?.ToList()?? [],
            状態=global::UI.ジョブ状態.完了,
            平均ナノ秒=保存合計へ変換(ソースタブ,ループ回数,平均),
            割り当てバイト数=保存合計へ変換(ソースタブ,ループ回数,1024),
            開始日時=DateTimeOffset.UtcNow,
            割り当て論理CPU=7,
            終了コード=0,
        });
    }

    private static double 保存合計へ変換(global::UI.ベンチマークソースタブ ソースタブ,int ループ回数,double 操作単位) =>
        global::UI.ベンチマーク作業軸.ループ回数を使うか(ソースタブ)&&ループ回数>1 ? 操作単位*ループ回数 : 操作単位;

    private static long 保存合計へ変換(global::UI.ベンチマークソースタブ ソースタブ,int ループ回数,long 操作単位) =>
        global::UI.ベンチマーク作業軸.ループ回数を使うか(ソースタブ)&&ループ回数>1 ? 操作単位*ループ回数 : 操作単位;

    private static string DBパスを作成() {
        var ルート = Path.Combine(AppContext.BaseDirectory,"BenchmarkUiRunRepositoryTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return Path.Combine(ルート,"Benchmark.db");
    }
}
