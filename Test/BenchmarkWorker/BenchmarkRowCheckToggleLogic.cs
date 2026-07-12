namespace Test.BenchmarkWorker;

public sealed class GridCheckToggleLogic{
    [Fact,Trait("Category","Unit")]public void 全てチェック済みなら解除する(){
        Assert.False(global::UI.GridCheckToggleLogic.ResolveTargetCheckedState([true,true]));
    }

    [Fact,Trait("Category","Unit")]public void 未チェックを含むなら全てチェックする(){
        Assert.True(global::UI.GridCheckToggleLogic.ResolveTargetCheckedState([false,false]));
    }

    [Fact,Trait("Category","Unit")]public void 混在なら全てチェックする(){
        Assert.True(global::UI.GridCheckToggleLogic.ResolveTargetCheckedState([true,false]));
        Assert.True(global::UI.GridCheckToggleLogic.ResolveTargetCheckedState([null]));
    }

    [Fact,Trait("Category","Unit")]public void 選択行がなければ何もしない(){
        Assert.Null(global::UI.GridCheckToggleLogic.ResolveTargetCheckedState([]));
    }
}
