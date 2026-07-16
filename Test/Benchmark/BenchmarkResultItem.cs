namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク結果項目テスト {
    [Fact]
    public void 仕様確認01() {
        var 永続化 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.Seamless永続化);
        var Tsavorite = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);

        var 永続化結果 = new global::UI.ベンチマーク結果項目(保存結果を作成(
            永続化.右プロジェクト,
            "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
            永続化.ID));
        var Tsavorite結果 = new global::UI.ベンチマーク結果項目(保存結果を作成(
            Tsavorite.右プロジェクト,
            "Benchmark.Tsavorite.Linq.Enumerable.LinqInt32",
            Tsavorite.ID));

        Assert.Equal(global::UI.ベンチマークソースタブ.Seamless永続化,永続化結果.ソースタブ);
        Assert.Equal(global::UI.ベンチマークソースタブ.SeamlessTsavorite,Tsavorite結果.ソースタブ);
    }

    [Fact]
    public void 仕様確認02() {
        var Tsavorite = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);

        var 結果 = global::UI.ベンチマーク結果項目.失敗を作成(
            new global::UI.ベンチマークケース(
                Tsavorite.右プロジェクト,
                Tsavorite.右アセンブリ名,
                "Benchmark.Tsavorite.Linq.Enumerable.LinqInt32",
                "Aggregate_func"),
            new InvalidOperationException("boom"));

        Assert.Equal(global::UI.ベンチマークソースタブ.SeamlessTsavorite,結果.ソースタブ);
    }

    [Fact]
    public void 仕様確認03() {
        var 配列 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);

        var 結果 = new global::UI.ベンチマーク結果項目(保存結果を作成(
            配列.左プロジェクト,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            string.Empty));

        Assert.Equal(global::UI.ベンチマークソースタブ.System配列,結果.ソースタブ);
    }

    [Fact]
    public void 仕様確認04() {
        var 永続化 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.Seamless永続化);

        var 結果 = new global::UI.ベンチマーク結果項目(保存結果を作成(
            永続化.右プロジェクト,
            "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
            string.Empty));
        Assert.Equal(global::UI.ベンチマークソースタブ.Seamless永続化,結果.ソースタブ);
    }

    [Fact]
    public void 仕様確認05() {
        var 配列 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);

        Assert.Throws<InvalidDataException>(() => new global::UI.ベンチマーク結果項目(保存結果を作成(
            配列.左プロジェクト,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            "Unknown.タブ")));
    }

    [Fact]
    public void 仕様確認06() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 結果 = new global::UI.ベンチマーク結果項目(new global::UI.ベンチマークジョブ {
            プロジェクト=ソースタブ.左プロジェクト,
            アセンブリ名=ソースタブ.左アセンブリ名,
            メソッド名="Aggregate_func",
            型完全名="Benchmark.System.Linq.Enumerable.FLinqInt32",
            ソースタブ=global::UI.ベンチマークソースタブ.System配列,
            要素数=4096,
            ループ回数=8,
            状態=global::UI.ジョブ状態.完了,
            平均ナノ秒=800,
            誤差ナノ秒=80,
            標準偏差ナノ秒=40,
            割り当てバイト数=1024,
        });

        Assert.Equal(100,結果.平均ナノ秒);
        Assert.Equal(800,結果.合計ナノ秒);
        Assert.Equal(10,結果.誤差ナノ秒);
        Assert.Equal(5,結果.標準偏差ナノ秒);
        Assert.Equal(128,結果.割り当てバイト数値);
    }

    [Fact]
    public void 仕様確認07() {
        var 保存値 = 保存結果を作成(
            global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列).左プロジェクト,
            "Benchmark.System.Linq.Enumerable.FLinqInt32",
            global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列).ID);

        var 結果 = new global::UI.ベンチマーク結果項目(保存値);

        Assert.Equal(100,結果.平均ナノ秒);
        Assert.Null(結果.合計ナノ秒);
    }

    private static global::UI.ベンチマーク保存結果 保存結果を作成(
        string プロジェクト,
        string 型完全名,
        string ソースタブID) =>
        new(
            プロジェクト,
            "Aggregate_func",
            型完全名,
            64,
            global::UI.ベンチマーク作業軸.既定ループ回数,
            "Completed",
            "100",
            100,
            128,
            DateTimeOffset.UtcNow,
            null,
            string.Empty) {
            ソースタブID=ソースタブID,
        };
}
