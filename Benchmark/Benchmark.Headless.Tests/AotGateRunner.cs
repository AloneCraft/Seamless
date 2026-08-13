using Benchmark.Headless;

using UI;

namespace Test.Benchmark;

public sealed class AotGateRunnerTests {
    [Fact]
    public void Smokeは現行全標準タブから左右一件ずつ解決する() {
        var 対象一覧=AotGateRunner.対象を解決(ベンチマークカタログ.読み込む(),"smoke");
        Assert.Equal(ベンチマークソースタブ一覧.標準全て.Count,対象一覧.Count);
        foreach(var 対象 in 対象一覧) {
            Assert.Equal(2,対象.ケース一覧.Count);
            Assert.Contains(対象.ケース一覧,ケース=>string.Equals(ケース.プロジェクト,対象.タブ.プロジェクト順[0],StringComparison.OrdinalIgnoreCase));
            Assert.Contains(対象.ケース一覧,ケース=>string.Equals(ケース.プロジェクト,対象.タブ.プロジェクト順[1],StringComparison.OrdinalIgnoreCase));
            Assert.Single(対象.ケース一覧.Select(static ケース=>(ケース.メソッド名,ケース.引数キー)).Distinct());
        }
    }

    [Fact]
    public void Fullは現行全標準タブの全カタログを重複欠落なく解決する() {
        var カタログ=ベンチマークカタログ.読み込む();
        var 対象一覧=AotGateRunner.対象を解決(カタログ,"full");
        var 期待一覧=ベンチマークソースタブ一覧.標準全て
            .SelectMany(タブ=>カタログ.Where(タブ.ケースを含むか))
            .Select(static ケース=>キーを作成(ケース))
            .ToHashSet(StringComparer.Ordinal);
        var 実際一覧=対象一覧
            .SelectMany(static 対象=>対象.ケース一覧)
            .Select(static ケース=>キーを作成(ケース))
            .ToArray();
        Assert.Equal(実際一覧.Length,実際一覧.Distinct(StringComparer.Ordinal).Count());
        Assert.True(期待一覧.SetEquals(実際一覧));
    }

    private static string キーを作成(ベンチマークケース ケース) =>
        string.Join("|",ケース.プロジェクト,ケース.アセンブリ名,ケース.型完全名,ケース.メソッド名,ケース.引数キー);
}
