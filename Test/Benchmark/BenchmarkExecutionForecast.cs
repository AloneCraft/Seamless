namespace Test.Benchmark;

public sealed class ベンチマーク実行予測ロジック {
    [Fact]
    public void 予測を計算_終端件数から平均と残りと終了予定を求める() {
        var 現在日時=new DateTimeOffset(2026,7,27,12,0,0,TimeSpan.Zero);

        var 予測=global::UI.ベンチマーク実行予測ロジック.計算(
            TimeSpan.FromSeconds(30),
            3,
            5,
            現在日時);

        Assert.NotNull(予測);
        Assert.Equal(TimeSpan.FromSeconds(10),予測.Value.平均実行時間);
        Assert.Equal(TimeSpan.FromSeconds(20),予測.Value.残り時間);
        Assert.Equal(現在日時.AddSeconds(20),予測.Value.終了予定日時);
    }

    [Fact]
    public void 表示文字列_終端件数ゼロではプレースホルダーを返す() {
        var 表示=global::UI.ベンチマーク実行予測ロジック.表示文字列(
            TimeSpan.FromSeconds(30),
            0,
            5,
            new DateTimeOffset(2026,7,27,12,0,0,TimeSpan.Zero));

        Assert.Equal("平均 --/件・残り --・終了予定 --",表示);
    }

    [Fact]
    public void 表示文字列_完了時は終了済みを表示する() {
        var 表示=global::UI.ベンチマーク実行予測ロジック.表示文字列(
            TimeSpan.FromSeconds(30),
            5,
            5,
            new DateTimeOffset(2026,7,27,12,0,0,TimeSpan.Zero));

        Assert.Equal("平均 6.0秒/件・残り 0秒・終了予定 終了済み",表示);
    }
}
