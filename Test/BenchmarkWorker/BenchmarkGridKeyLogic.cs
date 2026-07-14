using Syncfusion.Maui.Core.Internals;

namespace Test.BenchmarkWorker;

public sealed class グリッドチェック選択ロジック {
    [Fact, Trait("Category","Unit")]
    public void 空白キー単独だけチェック切替として処理する() {
        Assert.True(global::UI.グリッドチェック選択ロジック.チェックを切り替えるべきか(KeyboardKey.Space,false,false));
        Assert.False(global::UI.グリッドチェック選択ロジック.チェックを切り替えるべきか(KeyboardKey.Space,true,false));
        Assert.False(global::UI.グリッドチェック選択ロジック.チェックを切り替えるべきか(KeyboardKey.Space,false,true));
    }

    [Fact, Trait("Category","Unit")]
    public void 標準選択キーはSyncfusionへ委譲する() {
        Assert.False(global::UI.グリッドチェック選択ロジック.チェックを切り替えるべきか(KeyboardKey.Up,false,false));
        Assert.False(global::UI.グリッドチェック選択ロジック.チェックを切り替えるべきか(KeyboardKey.Down,false,true));
        Assert.False(global::UI.グリッドチェック選択ロジック.チェックを切り替えるべきか(KeyboardKey.A,true,false));
    }
}
