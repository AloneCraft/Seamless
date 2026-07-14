using UI;

namespace Test.Benchmark;

public sealed class ベンチマーク分析表示マッパー {
    [Fact]
    public void 改善候補を変換すると改善スコア順になる() {
        var 点一覧 = global::UI.ベンチマーク分析表示マッパー.改善候補を変換([
            行を作成(("メソッド名","Where"),("改善スコア",1.2),("左プロジェクト","Left"),("右プロジェクト","Right"),("要素数",128)),
            行を作成(("メソッド名","Select"),("改善スコア",2.4),("左プロジェクト","Left"),("右プロジェクト","Right"),("要素数",256)),
        ]);

        Assert.Equal(["Select","Where"],点一覧.Select(static 点 => 点.ラベル).ToArray());
        Assert.Equal(2.4,点一覧[0].値);
        Assert.Contains("要素数=256",点一覧[0].詳細文字列);
    }

    [Fact]
    public void カテゴリ概要を変換すると時間シェア順になる() {
        var 点一覧 = global::UI.ベンチマーク分析表示マッパー.カテゴリ概要を変換([
            行を作成(("カテゴリ","Projection"),("平均時間左シェア",0.5),("平均メモリ左シェア",0.4),("サンプル数",3)),
            行を作成(("カテゴリ","Filter"),("平均時間左シェア",0.7),("平均メモリ左シェア",0.6),("サンプル数",5)),
        ]);

        Assert.Equal(["Filter","Projection"],点一覧.Select(static 点 => 点.カテゴリ).ToArray());
        Assert.Equal(0.7,点一覧[0].平均時間左シェア);
        Assert.Equal(0.6,点一覧[0].平均メモリ左シェア);
        Assert.Equal(5,点一覧[0].サンプル数);
    }

    [Fact]
    public void 規模傾向を変換するとメソッド別に要素数順になる() {
        var 系列一覧 = global::UI.ベンチマーク分析表示マッパー.規模傾向を変換([
            行を作成(("メソッド名","Where"),("要素数",1024),("平均時間左シェア",0.8)),
            行を作成(("メソッド名","Where"),("要素数",256),("平均時間左シェア",0.6)),
            行を作成(("メソッド名","Select"),("要素数",128),("平均時間左シェア",0.9)),
        ]);

        var Where系列 = 系列一覧.First(系列 => 系列.系列名=="Where");
        Assert.Equal([256,1024],Where系列.点一覧.Select(static 点 => (int)点.横軸値).ToArray());
        Assert.Equal([0.6,0.8],Where系列.点一覧.Select(static 点 => 点.縦軸値).ToArray());
    }

    [Fact]
    public void 欠測失敗行列を変換すると重要度へ対応する() {
        var バッジ一覧 = global::UI.ベンチマーク分析表示マッパー.欠測失敗行列を変換([
            行を作成(("メソッド名","Where"),("プロジェクト","Left"),("側","L"),("要素数",128),("ジョブ状態","Failed"),("失敗種別","Exit")),
            行を作成(("メソッド名","Select"),("プロジェクト","Right"),("側","R"),("要素数",256),("ジョブ状態","Completed"),("対向欠測",1)),
            行を作成(("メソッド名","Sum"),("プロジェクト","Left"),("側","L"),("要素数",512),("ジョブ状態","Completed"),("対向欠測",0)),
        ]);

        Assert.Equal(ベンチマークバッジ重要度.重大,バッジ一覧[0].重要度);
        Assert.Equal(ベンチマークバッジ重要度.警告,バッジ一覧[1].重要度);
        Assert.Equal(ベンチマークバッジ重要度.情報,バッジ一覧[2].重要度);
    }

    [Fact]
    public void 測定品質を変換するとゲージ割合を範囲内へ収める() {
        var ゲージ一覧 = global::UI.ベンチマーク分析表示マッパー.測定品質を変換([
            行を作成(("メソッド名","Where"),("プロジェクト","Left"),("標準偏差割合",1.8),("誤差割合",-0.5),("要素数",128),("割り当て論理CPU",2)),
        ]);

        var ゲージ = Assert.Single(ゲージ一覧);
        Assert.Equal(1,ゲージ.標準偏差割合);
        Assert.Equal(0,ゲージ.誤差割合);
        Assert.Contains("CPU 2",ゲージ.詳細文字列);
    }

    private static IReadOnlyDictionary<string,object?> 行を作成(params (string キー,object? 値)[] 値一覧) =>
        値一覧.ToDictionary(static 項目 => 項目.キー,static 項目 => 項目.値,StringComparer.OrdinalIgnoreCase);
}
