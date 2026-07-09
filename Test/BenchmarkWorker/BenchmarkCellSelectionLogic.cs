namespace Test.BenchmarkWorker;

public sealed class BenchmarkCellSelectionLogic{
    [Fact,Trait("Category","Unit")]public void WholeRowUiSelectionRequiresAllCaseCells(){
        Assert.True(global::UI.BenchmarkRowUiSelectionLogic.IsWholeRowSelected([
            new global::UI.BenchmarkRowUiSelectionCell(true,true),
            new global::UI.BenchmarkRowUiSelectionCell(true,true),
            new global::UI.BenchmarkRowUiSelectionCell(false,false),
        ]));

        Assert.False(global::UI.BenchmarkRowUiSelectionLogic.IsWholeRowSelected([
            new global::UI.BenchmarkRowUiSelectionCell(true,true),
            new global::UI.BenchmarkRowUiSelectionCell(true,false),
            new global::UI.BenchmarkRowUiSelectionCell(false,false),
        ]));

        Assert.False(global::UI.BenchmarkRowUiSelectionLogic.IsWholeRowSelected([
            new global::UI.BenchmarkRowUiSelectionCell(false,false),
            new global::UI.BenchmarkRowUiSelectionCell(false,true),
        ]));
    }
}
