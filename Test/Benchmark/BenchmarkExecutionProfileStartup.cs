namespace Test.Benchmark;

public sealed class ベンチマーク起動実行プロファイル {
    [Fact,Trait("Category","Unit")]
    public void 解決() {
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.高速,global::UI.ベンチマーク起動実行プロファイル.解決(null));
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.高速,global::UI.ベンチマーク起動実行プロファイル.解決(""));
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.AOT,global::UI.ベンチマーク起動実行プロファイル.解決("AOT"));
        Assert.Throws<InvalidDataException>(()=>global::UI.ベンチマーク起動実行プロファイル.解決("typo"));
    }
}
