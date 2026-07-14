namespace Test.BenchmarkWorker;

public sealed class ベンチマーク整形テスト {
    [Fact]
    public void 仕様確認01() {
        Assert.Equal("332.11 μs",global::UI.ベンチマークフォーマッター.ナノ秒を整形(332_110));
    }
}
