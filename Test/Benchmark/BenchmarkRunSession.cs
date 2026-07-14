namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク実行セッション {
    [Fact]
    public void 仕様確認01() {
        var セッション = new global::UI.ベンチマーク実行セッション();

        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,100));
        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,250));

        var 結果 = Assert.Single(セッション.結果を取得(global::UI.ベンチマークソースタブ.System配列));
        Assert.Equal(1,セッション.件数);
        Assert.Equal(250,結果.平均ナノ秒);
    }

    [Fact]
    public void 仕様確認02() {
        var セッション = new global::UI.ベンチマーク実行セッション();

        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,100));
        セッション.結果を更新または追加(結果を作成("System.Range.FLinq","Aggregate",64,200));

        var 配列結果 = Assert.Single(セッション.結果を取得(global::UI.ベンチマークソースタブ.System配列));
        var 範囲結果 = Assert.Single(セッション.結果を取得(global::UI.ベンチマークソースタブ.System範囲));
        Assert.Equal("System.Array.FLinq",配列結果.プロジェクト);
        Assert.Equal("System.Range.FLinq",範囲結果.プロジェクト);
    }

    [Fact]
    public void 仕様確認03() {
        var セッション = new global::UI.ベンチマーク実行セッション();

        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,100));
        セッション.結果を更新または追加(結果を作成("System.Array.ZLinq","Aggregate",16,200));
        セッション.結果を更新または追加(結果を作成("System.Range.FLinq","Aggregate",4,300));

        Assert.Equal([16,64],セッション.要素数一覧を取得(global::UI.ベンチマークソースタブ.System配列));
    }

    [Fact]
    public void 仕様確認04() {
        var セッション = new global::UI.ベンチマーク実行セッション();

        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,100));
        セッション.結果を更新または追加(実行中結果を作成("System.Array.ZLinq","Aggregate",64));

        セッション.実行中結果をクリア();

        var 結果 = Assert.Single(セッション.結果を取得(global::UI.ベンチマークソースタブ.System配列));
        Assert.Equal("System.Array.FLinq",結果.プロジェクト);
        Assert.Equal("Completed",結果.状態);
    }

    [Fact]
    public void 仕様確認05() {
        var セッション = new global::UI.ベンチマーク実行セッション();

        セッション.結果を更新または追加(実行中結果を作成("System.Array.FLinq","Aggregate",64));

        セッション.実行中結果をクリア();

        Assert.Empty(セッション.結果を取得(global::UI.ベンチマークソースタブ.System配列));
        Assert.Equal(0,セッション.件数);
    }

    [Fact]
    public void 仕様確認06() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var セッション = new global::UI.ベンチマーク実行セッション();
        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,100,"Array.Type","Count=64",[new global::UI.ベンチマーク引数("Count","64")]));

        var 状態 = セッション.進行状態に変換(
            "実行処理-42",
            global::UI.ベンチマーク実行プロファイル.高速,
            ソースタブ,
            TimeSpan.FromMilliseconds(1),
            new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase) {
                ["System配列|行"]=true,
            },
            結果 => new global::UI.ベンチマークケース(結果.プロジェクト,ソースタブ.左アセンブリ名,結果.型完全名,結果.メソッド,結果.引数キー,結果.引数一覧));

        var ジョブ = Assert.Single(状態.ジョブ一覧);
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.高速,状態.実行プロファイル);
        Assert.Equal(global::UI.ベンチマークソースタブ.System配列,状態.ソースタブ);
        Assert.Equal([64],状態.要素数一覧);
        Assert.Equal(ソースタブ.左アセンブリ名,ジョブ.アセンブリ名);
        Assert.Equal("Array.Type",ジョブ.型完全名);
        Assert.Equal("Count=64",ジョブ.引数キー);
        Assert.True(状態.ベンチマーク選択.行選択["System配列|行"]);
    }

    [Fact]
    public void 仕様確認07() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);
        var セッション = new global::UI.ベンチマーク実行セッション();
        セッション.結果を更新または追加(結果を作成(
            ソースタブ.右プロジェクト,
            "Aggregate_func",
            64,
            100,
            "Benchmark.Tsavorite.Linq.Enumerable.LinqInt32",
            ソースタブ: ソースタブ.タブ));

        var 状態 = セッション.進行状態に変換(
            "実行処理-42",
            global::UI.ベンチマーク実行プロファイル.高速,
            ソースタブ,
            TimeSpan.FromMilliseconds(1),
            new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase),
            結果 => new global::UI.ベンチマークケース(
                結果.プロジェクト,
                ソースタブ.右アセンブリ名,
                結果.型完全名,
                結果.メソッド,
                結果.引数キー,
                結果.引数一覧));

        var ジョブ = Assert.Single(状態.ジョブ一覧);
        Assert.Equal(ソースタブ.タブ,ジョブ.ソースタブ);
        Assert.Equal(ソースタブ.右プロジェクト,ジョブ.プロジェクト);
        Assert.Equal(ソースタブ.右アセンブリ名,ジョブ.アセンブリ名);
    }

    [Fact]
    public void 仕様確認08() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var セッション = new global::UI.ベンチマーク実行セッション();
        セッション.結果を更新または追加(結果を作成("System.Array.FLinq","Aggregate",64,100,"int"));

        Assert.Throws<InvalidDataException>(() => セッション.進行状態に変換(
            "実行処理-42",
            global::UI.ベンチマーク実行プロファイル.高速,
            ソースタブ,
            TimeSpan.FromMilliseconds(1),
            new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase),
            _ => null));
    }

    private static global::UI.ベンチマーク結果項目 結果を作成(
        string プロジェクト,
        string メソッド,
        int 要素数,
        double 平均,
        string 型完全名 = "Sample.Type",
        string 引数キー = "",
        IReadOnlyList<global::UI.ベンチマーク引数>? 引数一覧 = null,
        global::UI.ベンチマークソースタブ? ソースタブ = null) =>
        new(
            プロジェクト,
            メソッド,
            global::UI.プロジェクト測定値.測定から作成(平均,128),
            要素数,
            型完全名,
            引数キー,
            引数一覧,
            ソースタブ) {
        };

    private static global::UI.ベンチマーク結果項目 実行中結果を作成(
        string プロジェクト,
        string メソッド,
        int 要素数,
        string 型完全名 = "Sample.Type",
        string 引数キー = "") =>
        new(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            メソッド名=メソッド,
            型完全名=型完全名,
            ソースタブ=global::UI.ベンチマークソースタブ.System配列,
            要素数=要素数,
            ループ回数=要素数,
            引数キー=引数キー,
            状態=global::UI.ジョブ状態.実行中,
            開始日時=DateTimeOffset.UtcNow,
        });
}
