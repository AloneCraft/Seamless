namespace UI;

internal interface ISelectionController{
    void ToggleAllBenchmarkSelection(BenchmarkSourceTabDefinition? sourceTab = null);
    void ToggleBenchmarkColumnGroup(ColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab = null);
    void ToggleBenchmarkRow(TableRow row);
    void SelectAllBenchmarkUiCells(BenchmarkSourceTabDefinition? sourceTab = null);
    void SelectBenchmarkCell(TableRow row,string project,BenchmarkCellSelectionGesture gesture);
    void SelectBenchmarkRow(TableRow row,BenchmarkCellSelectionGesture gesture);
    void SelectBenchmarkColumnGroup(ColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab = null);
    void ToggleBenchmarkCellCheck(TableRow row,string project);
    void SelectBenchmarkCellFromChart(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture);
    void SelectBenchmarkRowFromChart(SourceTabViewState? state,string benchmark,BenchmarkCellSelectionGesture gesture);
    void SetAllBenchmarkSelection(bool selected,BenchmarkSourceTabDefinition? sourceTab = null);
}
