namespace UI;

// 縺薙％繧堤ｷｨ髮・☆繧句ｴ蜷・
// - 驕ｸ謚樒憾諷九・菫晏ｭ・蠕ｩ蜈・・繧､繝ｳ繝・// - 蛻・陦悟腰菴阪・驕ｸ謚槫渚譏縺ｮ鬆・ｺ上ｒ螟画峩
internal interface ISelectionControllerContext {
    void ToggleAllBenchmarkSelectionCore(BenchmarkSourceTabDefinition? sourceTab);
    void ToggleBenchmarkColumnGroupCore(ColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab);
    void ToggleBenchmarkRowCore(TableRow row);
    void SelectAllBenchmarkUiCellsCore(BenchmarkSourceTabDefinition? sourceTab);
    void SelectBenchmarkCellCore(TableRow row,string project,BenchmarkCellSelectionGesture gesture);
    void SelectBenchmarkRowCore(TableRow row,BenchmarkCellSelectionGesture gesture);
    void SelectBenchmarkColumnGroupCore(ColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab);
    void ToggleBenchmarkCellCheckCore(TableRow row,string project);
    void SelectBenchmarkCellFromChartCore(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture);
    void SelectBenchmarkRowFromChartCore(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture);
    void SetAllBenchmarkSelectionCore(bool selected,BenchmarkSourceTabDefinition? sourceTab);
}

