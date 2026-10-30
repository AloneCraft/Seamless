using System.Reflection;

using UI;

namespace Test.Benchmark;

public sealed class ベンチマークスケーラブルメタデータ {
    [Fact]
    public void スケーラブルソースタブはセット辞書永続化リストで構成される() {
        Assert.Equal(
            [
                ベンチマークソースタブ一覧.スケーラブル永続化セットID,
                ベンチマークソースタブ一覧.スケーラブルTsavoriteセットID,
                ベンチマークソースタブ一覧.スケーラブル永続化辞書ID,
                ベンチマークソースタブ一覧.スケーラブルTsavorite辞書ID,
                ベンチマークソースタブ一覧.スケーラブル永続化リストID,
            ],
            ベンチマークソースタブ一覧.スケーラブル全て.Select(static タブ => タブ.ID).ToArray());

        var セット = ベンチマークソースタブ一覧.スケーラブル全て[0];
        Assert.Equal("今回",セット.左見出し);
        Assert.Equal("前回",セット.右見出し);
        Assert.Equal("Scalable.Persisted.Set.Previous",セット.右プロジェクト);

        var リスト = ベンチマークソースタブ一覧.スケーラブル全て[4];
        Assert.Equal("Scalable.Persisted.List",リスト.左プロジェクト);
        Assert.Equal("Scalable.Persisted.List.Previous",リスト.右プロジェクト);
        Assert.Equal(2,リスト.プロジェクト順.Count);
    }

    [Fact]
    public void スケーラブルカタログはスケーラブルプロジェクトだけを含む() {
        Assert.All(ベンチマークカタログ.スケーラブルプロジェクト一覧,プロジェクト => {
            Assert.StartsWith("Scalable.",プロジェクト.表示名,StringComparison.Ordinal);
            Assert.StartsWith("Benchmark.Scalable.",プロジェクト.アセンブリ名,StringComparison.Ordinal);
            Assert.StartsWith("Shared.Scalable.",プロジェクト.型接頭辞,StringComparison.Ordinal);
        });
    }

    [Fact]
    public void スケーラブル要素数は大規模な8段階である() {
        var 型 = Assembly.Load(new AssemblyName("Benchmark.Scalable.Persisted.Collections"))
            .GetType("Scalable.Collections.ScalableElementCounts",throwOnError: true)!;
        var 全て = ((IEnumerable<int>)型.GetProperty("All")!.GetValue(null)!).ToArray();
        var 既定初期値 = ((IEnumerable<int>)型.GetProperty("DefaultInitial")!.GetValue(null)!).ToArray();
        var 基本要素数 = (int)型.GetField("BaseElementCount")!.GetValue(null)!;

        Assert.Equal([65536,131072,262144,524288,1048576,2097152,4194304,8388608],全て);
        Assert.Equal([65536],既定初期値);
        Assert.Equal(65536,基本要素数);
    }
}
