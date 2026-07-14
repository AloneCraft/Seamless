namespace Test.BenchmarkWorker;

public sealed class ベンチマークセル選択ロジック {
    [Fact, Trait("Category","Unit")]
    public void 仕様確認01() {
        Assert.True(global::UI.ベンチマーク行UI選択ロジック.行全体を選択中か([
            new global::UI.ベンチマーク行UI選択セル(true,true),
            new global::UI.ベンチマーク行UI選択セル(true,true),
            new global::UI.ベンチマーク行UI選択セル(false,false),
        ]));

        Assert.False(global::UI.ベンチマーク行UI選択ロジック.行全体を選択中か([
            new global::UI.ベンチマーク行UI選択セル(true,true),
            new global::UI.ベンチマーク行UI選択セル(true,false),
            new global::UI.ベンチマーク行UI選択セル(false,false),
        ]));

        Assert.False(global::UI.ベンチマーク行UI選択ロジック.行全体を選択中か([
            new global::UI.ベンチマーク行UI選択セル(false,false),
            new global::UI.ベンチマーク行UI選択セル(false,true),
        ]));
    }
}
