using Syncfusion.Maui.Core.Internals;

namespace Test.BenchmarkWorker;

public sealed class GridCheckSelectionLogic{
    [Fact,Trait("Category","Unit")]public void Space単独だけチェック切替として処理する(){
        Assert.True(global::UI.GridCheckSelectionLogic.ShouldToggleChecks(KeyboardKey.Space,false,false));
        Assert.False(global::UI.GridCheckSelectionLogic.ShouldToggleChecks(KeyboardKey.Space,true,false));
        Assert.False(global::UI.GridCheckSelectionLogic.ShouldToggleChecks(KeyboardKey.Space,false,true));
    }

    [Fact,Trait("Category","Unit")]public void 標準選択キーはSyncfusionへ委譲する(){
        Assert.False(global::UI.GridCheckSelectionLogic.ShouldToggleChecks(KeyboardKey.Up,false,false));
        Assert.False(global::UI.GridCheckSelectionLogic.ShouldToggleChecks(KeyboardKey.Down,false,true));
        Assert.False(global::UI.GridCheckSelectionLogic.ShouldToggleChecks(KeyboardKey.A,true,false));
    }
}
