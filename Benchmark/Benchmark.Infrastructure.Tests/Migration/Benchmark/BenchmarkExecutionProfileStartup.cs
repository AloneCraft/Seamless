namespace Test.Benchmark;

public sealed class ベンチマーク起動実行プロファイル {
    [Fact,Trait("Category","Unit")]
    public void 解決() {
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.高速,global::UI.ベンチマーク起動実行プロファイル.解決(null));
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.高速,global::UI.ベンチマーク起動実行プロファイル.解決(""));
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.正確,global::UI.ベンチマーク起動実行プロファイル.解決("正確"));
        Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク起動実行プロファイル.解決("typo"));
    }
}
