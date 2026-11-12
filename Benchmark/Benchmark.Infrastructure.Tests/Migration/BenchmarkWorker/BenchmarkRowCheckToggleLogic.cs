namespace Test.BenchmarkWorker;

public sealed class グリッドチェック切替ロジック {
    [Fact, Trait("Category","Unit")]
    public void 全てチェック済みなら解除する() {
        Assert.False(global::UI.グリッドチェック切替ロジック.目標チェック状態を解決([true,true]));
    }

    [Fact, Trait("Category","Unit")]
    public void 未チェックを含むなら全てチェックする() {
        Assert.True(global::UI.グリッドチェック切替ロジック.目標チェック状態を解決([false,false]));
    }

    [Fact, Trait("Category","Unit")]
    public void 混在なら全てチェックする() {
        Assert.True(global::UI.グリッドチェック切替ロジック.目標チェック状態を解決([true,false]));
        Assert.True(global::UI.グリッドチェック切替ロジック.目標チェック状態を解決([null]));
    }

    [Fact, Trait("Category","Unit")]
    public void 選択行がなければ何もしない() {
        Assert.Null(global::UI.グリッドチェック切替ロジック.目標チェック状態を解決([]));
    }
}
