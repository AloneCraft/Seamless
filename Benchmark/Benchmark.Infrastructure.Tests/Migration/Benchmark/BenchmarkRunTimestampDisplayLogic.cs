namespace Test.Benchmark;

public sealed class ベンチマーク実行日時表示ロジック {
    [Fact]
    public void 仕様確認01() {
        var 開始日時 = new DateTimeOffset(2026,7,1,17,6,18,TimeSpan.Zero);
        var 観測日時 = new DateTimeOffset(2026,7,1,17,6,30,TimeSpan.Zero);

        var 日時 = global::UI.ベンチマーク実行日時表示ロジック.実行中日時を解決(開始日時,観測日時);

        Assert.Equal(開始日時,日時);
    }

    [Fact]
    public void 仕様確認02() {
        var 観測日時 = new DateTimeOffset(2026,7,1,17,6,30,TimeSpan.Zero);

        var 日時 = global::UI.ベンチマーク実行日時表示ロジック.実行中日時を解決(null,観測日時);

        Assert.Equal(観測日時,日時);
    }
}



