namespace UI;

// 縺薙％繧堤ｷｨ髮・☆繧句ｴ蜷・
// - 螟画峩縺ｯ蜴溷援縺薙％縺ｫ髮・ｴ・＠縲〃M Core 繧定埋縺丈ｿ昴▽縺薙→
internal sealed class SelectionController:ISelectionController {
    private readonly ISelectionControllerContext context;

    public SelectionController(ISelectionControllerContext context) {
        this.context=context;
    }

    public void ToggleAllBenchmarkSelection(BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.context.ToggleAllBenchmarkSelectionCore(sourceTab);

    public void ToggleBenchmarkColumnGroup(ColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.context.ToggleBenchmarkColumnGroupCore(group,sourceTab);

    public void ToggleBenchmarkRow(TableRow row) =>
        this.context.ToggleBenchmarkRowCore(row);

    public void SelectAllBenchmarkUiCells(BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.context.SelectAllBenchmarkUiCellsCore(sourceTab);

    public void SelectBenchmarkCell(TableRow row,string project,BenchmarkCellSelectionGesture gesture) =>
        this.context.SelectBenchmarkCellCore(row,project,gesture);

    public void SelectBenchmarkRow(TableRow row,BenchmarkCellSelectionGesture gesture) =>
        this.context.SelectBenchmarkRowCore(row,gesture);

    public void SelectBenchmarkColumnGroup(ColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.context.SelectBenchmarkColumnGroupCore(group,gesture,sourceTab);

    public void ToggleBenchmarkCellCheck(TableRow row,string project) =>
        this.context.ToggleBenchmarkCellCheckCore(row,project);

    public void SelectBenchmarkCellFromChart(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture) =>
        this.context.SelectBenchmarkCellFromChartCore(state,benchmark,project,gesture);

    public void SelectBenchmarkRowFromChart(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture) =>
        this.context.SelectBenchmarkRowFromChartCore(state,benchmark,project,gesture);

    public void SetAllBenchmarkSelection(bool selected,BenchmarkSourceTabDefinition? sourceTab = null) =>
        this.context.SetAllBenchmarkSelectionCore(selected,sourceTab);
}

