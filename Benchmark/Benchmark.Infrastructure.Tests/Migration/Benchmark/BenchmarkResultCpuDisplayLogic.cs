namespace Test.Benchmark;

public sealed class ベンチマーク結果CPU表示ロジック {
    [Fact]
    public void 仕様確認01() {
        var 論理CPU = global::UI.ベンチマーク結果CPU表示ロジック.割り当て論理CPUを解決(4,[0,2,4,6]);

        Assert.Equal(4,論理CPU);
    }

    [Fact]
    public void 仕様確認02() {
        Assert.Null(global::UI.ベンチマーク結果CPU表示ロジック.割り当て論理CPUを解決(null,[0,2,4,6]));
        Assert.Null(global::UI.ベンチマーク結果CPU表示ロジック.割り当て論理CPUを解決(5,[0,2,4,6]));
    }
}



