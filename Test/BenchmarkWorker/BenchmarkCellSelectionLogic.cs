using UI;

namespace Test.BenchmarkWorker;

public sealed class BenchmarkCellSelectionLogic{
    [Fact]
    public void CellSelectionUsesExcelModifiers(){
        var result = global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            4,
            [],
            null,
            BenchmarkCellSelectionTarget.Cell(1,1),
            BenchmarkCellSelectionGesture.Replace);

        AssertSelection(result,(1,1));
        Assert.Equal(new BenchmarkCellCoordinate(1,1),result.Anchor?.Cell);

        result=global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            4,
            result.SelectedCells,
            result.Anchor,
            BenchmarkCellSelectionTarget.Cell(3,2),
            BenchmarkCellSelectionGesture.Toggle);

        AssertSelection(result,(1,1),(3,2));
        Assert.Equal(new BenchmarkCellCoordinate(3,2),result.Anchor?.Cell);

        result=global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            4,
            result.SelectedCells,
            result.Anchor,
            BenchmarkCellSelectionTarget.Cell(4,3),
            BenchmarkCellSelectionGesture.Extend);

        AssertSelection(result,(3,2),(3,3),(4,2),(4,3));
        Assert.Equal(new BenchmarkCellCoordinate(3,2),result.Anchor?.Cell);
    }

    [Fact]
    public void CtrlShiftTogglesWholeRange(){
        var anchor = new BenchmarkCellSelectionAnchor(BenchmarkCellSelectionTargetKind.Cell,new BenchmarkCellCoordinate(0,0));
        var current = new[] {
            new BenchmarkCellCoordinate(0,0),
            new BenchmarkCellCoordinate(0,1),
            new BenchmarkCellCoordinate(1,0),
            new BenchmarkCellCoordinate(1,1),
        };

        var result = global::UI.BenchmarkCellSelectionLogic.Apply(
            4,
            4,
            current,
            anchor,
            BenchmarkCellSelectionTarget.Cell(1,1),
            BenchmarkCellSelectionGesture.ExtendToggle);

        AssertSelection(result);
        Assert.Equal(anchor,result.Anchor);

        result=global::UI.BenchmarkCellSelectionLogic.Apply(
            4,
            4,
            [new BenchmarkCellCoordinate(0,0)],
            anchor,
            BenchmarkCellSelectionTarget.Cell(1,1),
            BenchmarkCellSelectionGesture.ExtendToggle);

        AssertSelection(result,(0,0),(0,1),(1,0),(1,1));
    }

    [Fact]
    public void RowHeaderShiftSelectsRowRangeAcrossAllColumns(){
        var result = global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            3,
            [],
            null,
            BenchmarkCellSelectionTarget.RowHeader(1),
            BenchmarkCellSelectionGesture.Replace);

        AssertSelection(result,(1,0),(1,1),(1,2));
        Assert.Equal(BenchmarkCellSelectionTargetKind.RowHeader,result.Anchor?.Kind);
        Assert.Equal(new BenchmarkCellCoordinate(1,0),result.Anchor?.Cell);

        result=global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            3,
            result.SelectedCells,
            result.Anchor,
            BenchmarkCellSelectionTarget.RowHeader(3),
            BenchmarkCellSelectionGesture.Extend);

        AssertSelection(result,(1,0),(1,1),(1,2),(2,0),(2,1),(2,2),(3,0),(3,1),(3,2));
    }

    [Fact]
    public void ColumnHeaderShiftSelectsColumnRangeAcrossAllRows(){
        var result = global::UI.BenchmarkCellSelectionLogic.Apply(
            3,
            5,
            [],
            null,
            BenchmarkCellSelectionTarget.ColumnHeader(1,1),
            BenchmarkCellSelectionGesture.Replace);

        AssertSelection(result,(0,1),(1,1),(2,1));
        Assert.Equal(BenchmarkCellSelectionTargetKind.ColumnHeader,result.Anchor?.Kind);
        Assert.Equal(new BenchmarkCellCoordinate(0,1),result.Anchor?.Cell);

        result=global::UI.BenchmarkCellSelectionLogic.Apply(
            3,
            5,
            result.SelectedCells,
            result.Anchor,
            BenchmarkCellSelectionTarget.ColumnHeader(3,3),
            BenchmarkCellSelectionGesture.Extend);

        AssertSelection(result,(0,1),(0,2),(0,3),(1,1),(1,2),(1,3),(2,1),(2,2),(2,3));
    }

    [Fact]
    public void MixedHeaderShiftUsesNormalRectangle(){
        var result = global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            5,
            [],
            null,
            BenchmarkCellSelectionTarget.RowHeader(2),
            BenchmarkCellSelectionGesture.Replace);

        result=global::UI.BenchmarkCellSelectionLogic.Apply(
            5,
            5,
            result.SelectedCells,
            result.Anchor,
            BenchmarkCellSelectionTarget.ColumnHeader(3,3),
            BenchmarkCellSelectionGesture.Extend);

        AssertSelection(result,(0,0),(0,1),(0,2),(0,3),(1,0),(1,1),(1,2),(1,3),(2,0),(2,1),(2,2),(2,3));
    }

    [Fact]
    public void CornerHeaderAlwaysSelectsAllCellsAndKeepsAnchor(){
        var anchor = new BenchmarkCellSelectionAnchor(BenchmarkCellSelectionTargetKind.Cell,new BenchmarkCellCoordinate(1,1));
        var result = global::UI.BenchmarkCellSelectionLogic.Apply(
            2,
            3,
            [new BenchmarkCellCoordinate(1,1)],
            anchor,
            BenchmarkCellSelectionTarget.CornerHeader(),
            BenchmarkCellSelectionGesture.Toggle);

        AssertSelection(result,(0,0),(0,1),(0,2),(1,0),(1,1),(1,2));
        Assert.Equal(anchor,result.Anchor);
    }

    [Fact]
    public void WholeRowUiSelectionRequiresAllCaseCells(){
        Assert.True(BenchmarkRowUiSelectionLogic.IsWholeRowSelected([
            new BenchmarkRowUiSelectionCell(true,true),
            new BenchmarkRowUiSelectionCell(true,true),
            new BenchmarkRowUiSelectionCell(false,false),
        ]));

        Assert.False(BenchmarkRowUiSelectionLogic.IsWholeRowSelected([
            new BenchmarkRowUiSelectionCell(true,true),
            new BenchmarkRowUiSelectionCell(true,false),
            new BenchmarkRowUiSelectionCell(false,false),
        ]));

        Assert.False(BenchmarkRowUiSelectionLogic.IsWholeRowSelected([
            new BenchmarkRowUiSelectionCell(false,false),
            new BenchmarkRowUiSelectionCell(false,true),
        ]));
    }

    private static void AssertSelection(BenchmarkCellSelectionResult result,params (int Row,int Column)[] expected){
        var actual = result.SelectedCells
            .OrderBy(static item => item.RowIndex)
            .ThenBy(static item => item.ColumnIndex)
            .Select(static item => (item.RowIndex,item.ColumnIndex))
            .ToArray();
        Assert.Equal(expected,actual);
    }
}


