namespace Test.BenchmarkWorker;

public sealed class BenchmarkRowCheckToggleLogic{
    [Fact,Trait("Category","Unit")]public void 全てチェック済みなら解除する(){
        Assert.False(global::UI.BenchmarkRowCheckToggleLogic.ResolveTargetCheckedState([true,true]));
    }

    [Fact,Trait("Category","Unit")]public void 未チェックを含むなら全てチェックする(){
        Assert.True(global::UI.BenchmarkRowCheckToggleLogic.ResolveTargetCheckedState([false,false]));
    }

    [Fact,Trait("Category","Unit")]public void 混在なら全てチェックする(){
        Assert.True(global::UI.BenchmarkRowCheckToggleLogic.ResolveTargetCheckedState([true,false]));
        Assert.True(global::UI.BenchmarkRowCheckToggleLogic.ResolveTargetCheckedState([null]));
    }

    [Fact,Trait("Category","Unit")]public void 選択行がなければ何もしない(){
        Assert.Null(global::UI.BenchmarkRowCheckToggleLogic.ResolveTargetCheckedState([]));
    }
}
