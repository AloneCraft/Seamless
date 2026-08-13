namespace Benchmark.Headless.Tests;

public sealed class PerformanceGateRunnerTest{
    [Fact]
    public void 成果物ルート名はミリ秒と一意IDを含む(){
        var 日時=new DateTimeOffset(2026,8,12,13,14,15,678,TimeSpan.Zero);
        var 一意ID=Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        var 名前=global::Benchmark.Headless.PerformanceGateRunner.成果物ルート名を作成(日時,一意ID);

        Assert.Equal("20260812-131415-678-0123456789abcdef0123456789abcdef",名前);
    }

    [Fact]
    public void 同一ミリ秒でも一意IDが異なれば衝突しない(){
        var 日時=new DateTimeOffset(2026,8,12,13,14,15,678,TimeSpan.Zero);

        var 左=global::Benchmark.Headless.PerformanceGateRunner.成果物ルート名を作成(
            日時,Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
        var 右=global::Benchmark.Headless.PerformanceGateRunner.成果物ルート名を作成(
            日時,Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210"));

        Assert.NotEqual(左,右);
    }
}
