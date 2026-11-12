namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク改善プロンプトビルダー {
    [Fact]
    public void 概要を作成_左右シェアの算術平均と件数を計算する() {
        var 行一覧 = new[]{
            行を作成("A",global::UI.シェア値.左シェアから作成(0.2),global::UI.シェア値.左シェアから作成(0.8)),
            行を作成("B",global::UI.シェア値.左シェアから作成(0.8),global::UI.シェア値.欠測),
            行を作成("C",global::UI.シェア値.欠測,global::UI.シェア値.左シェアから作成(0.6)),
            行を作成("D",global::UI.シェア値.欠測,global::UI.シェア値.欠測),
        };

        var 概要 = global::UI.ベンチマーク改善プロンプトビルダー.概要を作成(行一覧);

        Assert.Equal(0.5,概要.時間左平均!.Value,6);
        Assert.Equal(0.7,概要.メモリ左平均!.Value,6);
        Assert.Equal(2,概要.時間対象数);
        Assert.Equal(2,概要.時間除外数);
        Assert.Equal(2,概要.メモリ対象数);
        Assert.Equal(2,概要.メモリ除外数);
        Assert.Equal("時間シェア 0.50:0.50 (対象 2 / 除外 2) / メモリシェア 0.70:0.30 (対象 2 / 除外 2)",概要.表示文字列);
    }

    [Fact]
    public void 概要を作成_空の行一覧を処理する() {
        var 概要 = global::UI.ベンチマーク改善プロンプトビルダー.概要を作成([]);

        Assert.Null(概要.時間左平均);
        Assert.Null(概要.メモリ左平均);
        Assert.Equal(0,概要.時間対象数);
        Assert.Equal(0,概要.時間除外数);
        Assert.Equal(0,概要.メモリ対象数);
        Assert.Equal(0,概要.メモリ除外数);
        Assert.Equal("全体 時間シェア - (対象 0 / 除外 0) / メモリシェア - (対象 0 / 除外 0)",概要.全体表示文字列);
    }

    [Fact]
    public void 候補を構築_時間シェアが大きい回帰から並べる() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 状態 = 状態を作成(
            ジョブを作成(ソースタブ.左プロジェクト,"Aggregate_sum",64,300,100),
            ジョブを作成(ソースタブ.右プロジェクト,"Aggregate_sum",64,100,100),
            ジョブを作成(ソースタブ.左プロジェクト,"Any_predicate",64,120,100),
            ジョブを作成(ソースタブ.右プロジェクト,"Any_predicate",64,100,100));

        var 候補一覧 = global::UI.ベンチマーク改善候補ビルダー.候補を構築(状態);

        Assert.Equal("Aggregate_sum",候補一覧[0].メソッド名);
        Assert.Equal(0.75,候補一覧[0].時間シェア.左シェア!.Value,6);
        Assert.Equal("Any_predicate",候補一覧[1].メソッド名);
    }

    [Fact]
    public void 候補を構築_時間改善とメモリ回帰を個別に判定する() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 状態 = 状態を作成(
            ジョブを作成(ソースタブ.左プロジェクト,"Where_predicate",64,80,500),
            ジョブを作成(ソースタブ.右プロジェクト,"Where_predicate",64,100,100));

        var 候補 = Assert.Single(global::UI.ベンチマーク改善候補ビルダー.候補を構築(状態));

        Assert.False(候補.時間回帰か);
        Assert.True(候補.メモリ回帰か);
        Assert.Equal(5.0/6.0,候補.メモリシェア.左シェア!.Value,6);
    }

    [Fact]
    public void 候補を構築_時間とメモリの最悪左シェア順に並べる() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 状態 = 状態を作成(
            ジョブを作成(ソースタブ.左プロジェクト,"Aggregate_time",64,250,100),
            ジョブを作成(ソースタブ.右プロジェクト,"Aggregate_time",64,100,100),
            ジョブを作成(ソースタブ.左プロジェクト,"Where_allocated",64,100,600),
            ジョブを作成(ソースタブ.右プロジェクト,"Where_allocated",64,100,100));

        var 候補一覧 = global::UI.ベンチマーク改善候補ビルダー.候補を構築(状態);

        Assert.Equal("Where_allocated",候補一覧[0].メソッド名);
        Assert.Equal(6.0/7.0,候補一覧[0].メモリシェア.左シェア!.Value,6);
        Assert.Equal("Aggregate_time",候補一覧[1].メソッド名);
        Assert.Equal(5.0/7.0,候補一覧[1].時間シェア.左シェア!.Value,6);
    }

    [Fact]
    public void プロンプトを構築_上位十件に制限せず全回帰候補を含める() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ジョブ一覧 = new List<global::UI.ベンチマークジョブ>();
        for(var インデックス = 0;インデックス<12;インデックス++) {
            var メソッド名 = "Aggregate_"+インデックス.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            ジョブ一覧.Add(ジョブを作成(ソースタブ.左プロジェクト,メソッド名,64,200+インデックス,200+インデックス));
            ジョブ一覧.Add(ジョブを作成(ソースタブ.右プロジェクト,メソッド名,64,100,100));
        }
        var プロンプト = global::UI.ベンチマーク改善プロンプトビルダー.プロンプトを構築(状態を作成(ジョブ一覧.ToArray()));

        for(var インデックス = 0;インデックス<12;インデックス++) {
            Assert.Contains("Aggregate_"+インデックス.ToString(global::System.Globalization.CultureInfo.InvariantCulture),プロンプト);
        }
    }

    [Fact]
    public void プロンプトを構築_全候補とカテゴリ区画を使う() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 状態 = 状態を作成(
            ジョブを作成(ソースタブ.左プロジェクト,"Aggregate_sum",64,300,100),
            ジョブを作成(ソースタブ.右プロジェクト,"Aggregate_sum",64,100,100),
            ジョブを作成(ソースタブ.左プロジェクト,"Any_predicate",64,200,100),
            ジョブを作成(ソースタブ.右プロジェクト,"Any_predicate",64,100,100));
        var プロンプト = global::UI.ベンチマーク改善プロンプトビルダー.プロンプトを構築(状態);

        Assert.Contains("## 集約",プロンプト);
        Assert.Contains("Aggregate_sum",プロンプト);
        Assert.Contains("検索ヒント",プロンプト);
        Assert.Contains("単体テスト",プロンプト);
        Assert.Contains("ベンチマーク",プロンプト);
        Assert.Contains("Any_predicate",プロンプト);
        Assert.Contains("一般化できない特殊化",プロンプト);
        Assert.Contains("ベンチマーク実行の検出",プロンプト);
        Assert.Contains("列挙順、遅延実行、例外、破棄、デリゲートの副作用",プロンプト);
        Assert.Contains("平均ナノ秒と割り当てバイト数を同時に比較",プロンプト);
        Assert.Contains("異なるデータ分布",プロンプト);
        Assert.Contains("反復測定",プロンプト);
    }

    private static global::UI.進行状態 状態を作成(params global::UI.ベンチマークジョブ[] ジョブ一覧) =>
        new() {
            実行ID="実行処理",
            ソースタブ=global::UI.ベンチマークソースタブ.System配列,
            ジョブ一覧=ジョブ一覧.ToList(),
        };

    private static global::UI.ベンチマークジョブ ジョブを作成(
        string プロジェクト,
        string メソッド名,
        int 要素数,
        double 平均ナノ秒,
        long 割り当てバイト数) =>
        new() {
            プロジェクト=プロジェクト,
            アセンブリ名="Benchmarks",
            型完全名="Sample.Type",
            メソッド名=メソッド名,
            ソースタブ=global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ??global::UI.ベンチマークソースタブ.System配列,
            要素数=要素数,
            状態=global::UI.ジョブ状態.完了,
            平均ナノ秒=平均ナノ秒,
            割り当てバイト数=割り当てバイト数,
            終了コード=0,
        };

    private static global::UI.ベンチマーク比較行 行を作成(string ベンチマーク,global::UI.シェア値 時間シェア,global::UI.シェア値 メモリシェア) {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 定義 = new global::UI.比較ペア定義(
            ソースタブ.ID,
            ソースタブ.表示名,
            ソースタブ.表示名,
            ソースタブ.左プロジェクト,
            ソースタブ.右プロジェクト);
        return new global::UI.ベンチマーク比較行(
            ベンチマーク,
            ソースタブ,
            global::UI.プロジェクト測定値.欠測,
            global::UI.プロジェクト測定値.欠測) {
            左右シェア=new global::UI.シェア比較(
                定義,
                global::UI.プロジェクト測定値.欠測,
                global::UI.プロジェクト測定値.欠測,
                時間シェア,
                メモリシェア),
        };
    }
}
