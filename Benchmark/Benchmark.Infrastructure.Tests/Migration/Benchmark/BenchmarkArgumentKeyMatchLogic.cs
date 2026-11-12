namespace Test.Benchmark;

public sealed class ベンチマーク引数キー一致ロジック {
    [Fact]
    public void 仕様確認01() {
        Assert.True(global::UI.ベンチマーク引数キー一致ロジック.厳密一致か("Count=1","Count=1"));
        Assert.True(global::UI.ベンチマーク引数キー一致ロジック.厳密一致か(string.Empty,string.Empty));
        Assert.False(global::UI.ベンチマーク引数キー一致ロジック.厳密一致か("Count=1",string.Empty));
        Assert.False(global::UI.ベンチマーク引数キー一致ロジック.厳密一致か(string.Empty,"Count=1"));
        Assert.False(global::UI.ベンチマーク引数キー一致ロジック.厳密一致か("Count=1","Count=2"));
    }
}



