namespace UI;

public sealed partial class RunnerViewModel {
    Task IRunOrchestratorContext.RunOrResumeAsyncCore() =>
        this.RunOrResumeAsync();

    Task IRunOrchestratorContext.RunInfiniteAsyncCore() =>
#if DEBUG
        this.RunInfiniteAsync();
#else
        Task.CompletedTask;
#endif

    void IRunOrchestratorContext.ToggleRunCore() =>
        this.ToggleRun();

    Task IRunOrchestratorContext.StopAsyncCore() =>
        this.StopAsyncCore();

    Task IRunOrchestratorContext.PauseAsyncCore() =>
        this.PauseAsyncCore();

    Task IRunOrchestratorContext.CancelAndWaitAsyncCore() =>
        this.CancelAndWaitAsyncCore();

    void IRunOrchestratorContext.CancelCore() =>
        this.Cancel();

    void IResultPipelineApplierContext.AcceptRunResultCore(BenchmarkResultItem result,int runGeneration,long? uiRunId) =>
        this.AcceptRunResult(result,runGeneration,uiRunId);

    void IResultPipelineApplierContext.AcceptRunStatusCore(string status) =>
        this.AcceptRunStatus(status);

    void IResultPipelineApplierContext.ApplyRunUiSnapshotCore(UiSnapshot snapshot) =>
        this.ApplyRunUiSnapshot(snapshot);

    void IResultPipelineApplierContext.ApplyFinalLiveVisualCleanupCore(bool discardPendingRunUpdates) =>
        this.ApplyFinalLiveVisualCleanup(discardPendingRunUpdates);

    void ISelectionControllerContext.ToggleAllBenchmarkSelectionCore(BenchmarkSourceTabDefinition? sourceTab) =>
        this.ToggleAllBenchmarkSelectionCore(sourceTab);

    void ISelectionControllerContext.ToggleBenchmarkColumnGroupCore(ColumnSelectionGroup group,BenchmarkSourceTabDefinition? sourceTab) =>
        this.ToggleBenchmarkColumnGroupCore(group,sourceTab);

    void ISelectionControllerContext.ToggleBenchmarkRowCore(TableRow row) =>
        this.ToggleBenchmarkRowCore(row);

    void ISelectionControllerContext.SelectAllBenchmarkUiCellsCore(BenchmarkSourceTabDefinition? sourceTab) =>
        this.SelectAllBenchmarkUiCellsCore(sourceTab);

    void ISelectionControllerContext.SelectBenchmarkCellCore(TableRow row,string project,BenchmarkCellSelectionGesture gesture) =>
        this.SelectBenchmarkCellCore(row,project,gesture);

    void ISelectionControllerContext.SelectBenchmarkRowCore(TableRow row,BenchmarkCellSelectionGesture gesture) =>
        this.SelectBenchmarkRowCore(row,gesture);

    void ISelectionControllerContext.SelectBenchmarkColumnGroupCore(ColumnSelectionGroup group,BenchmarkCellSelectionGesture gesture,BenchmarkSourceTabDefinition? sourceTab) =>
        this.SelectBenchmarkColumnGroupCore(group,gesture,sourceTab);

    void ISelectionControllerContext.ToggleBenchmarkCellCheckCore(TableRow row,string project) =>
        this.ToggleBenchmarkCellCheckCore(row,project);

    void ISelectionControllerContext.SelectBenchmarkCellFromChartCore(SourceTabViewState? state,string benchmark,string project,BenchmarkCellSelectionGesture gesture) =>
        this.SelectBenchmarkCellFromChartCore(state,benchmark,project,gesture);

    void ISelectionControllerContext.SelectBenchmarkRowFromChartCore(SourceTabViewState? state,string benchmark,BenchmarkCellSelectionGesture gesture) =>
        this.SelectBenchmarkRowFromChartCore(state,benchmark,gesture);

    void ISelectionControllerContext.SetAllBenchmarkSelectionCore(bool selected,BenchmarkSourceTabDefinition? sourceTab) =>
        this.SetAllBenchmarkSelection(selected,sourceTab);

    void IChartControllerContext.UpdateChartCoreForController(bool preserveExistingWhenNoMeasuredPoints) =>
        this.UpdateChart(preserveExistingWhenNoMeasuredPoints);

    void IChartControllerContext.UpdateAllSourceTabChartsCoreForController(bool preserveExistingWhenNoMeasuredPoints) =>
        this.UpdateAllSourceTabCharts(preserveExistingWhenNoMeasuredPoints);

    IReadOnlyList<BenchmarkRankedRatioPoint> IChartControllerContext.LoadImprovementCandidatesChartDataCore() =>
        this.LoadImprovementCandidatesChartDataCore();

    IReadOnlyList<BenchmarkRankedRatioPoint> IChartControllerContext.LoadTimeRatioWorstChartDataCore() =>
        this.LoadTimeRatioWorstChartDataCore();

    IReadOnlyList<BenchmarkRankedRatioPoint> IChartControllerContext.LoadAllocationRatioWorstChartDataCore() =>
        this.LoadAllocationRatioWorstChartDataCore();

    IReadOnlyList<BenchmarkCategoryRatioPoint> IChartControllerContext.LoadCategorySummaryChartDataCore() =>
        this.LoadCategorySummaryChartDataCore();

    IReadOnlyList<BenchmarkTrendSeries> IChartControllerContext.LoadHistoryTrendChartDataCore() =>
        this.LoadHistoryTrendChartDataCore();

    IReadOnlyList<BenchmarkTrendSeries> IChartControllerContext.LoadScaleTrendChartDataCore() =>
        this.LoadScaleTrendChartDataCore();

    IReadOnlyList<BenchmarkBadgeItem> IChartControllerContext.LoadMissingFailureMatrixBadgesCore() =>
        this.LoadMissingFailureMatrixBadgesCore();

    IReadOnlyList<BenchmarkQualityGaugePoint> IChartControllerContext.LoadMeasurementQualityGaugesCore() =>
        this.LoadMeasurementQualityGaugesCore();

    IReadOnlyList<BenchmarkBadgeItem> IChartControllerContext.LoadFallbackDiagnosticsBadgesCore() =>
        this.LoadFallbackDiagnosticsBadgesCore();

    IReadOnlyList<SourceTabViewState> ISortControllerContext.SourceTabViewStates =>
        this.sourceTabViewStates;

    BenchmarkSourceTabDefinition ISortControllerContext.SelectedSourceDefinition =>
        this.SelectedSourceDefinition;

    bool ISortControllerContext.HasBenchmarkTypeColumn {
        get => this.HasBenchmarkTypeColumn;
        set => this.HasBenchmarkTypeColumn=value;
    }

    GridSortColumn ISortControllerContext.CurrentSortColumn {
        get => this.CurrentSortColumn;
        set => this.CurrentSortColumn=value;
    }

    bool ISortControllerContext.CurrentSortDescending {
        get => this.CurrentSortDescending;
        set => this.CurrentSortDescending=value;
    }

    bool ISortControllerContext.IsBenchmarkRunActive =>
        this.IsBenchmarkRunActive;

    bool ISortControllerContext.IsBenchmarkPaused =>
        this.IsBenchmarkPaused;

    IUiPreferences ISortControllerContext.UiPreferences =>
        this.uiPreferences;

    void ISortControllerContext.NotifyBenchmarkSortSummaryChanged() =>
        this.OnPropertyChanged(nameof(this.BenchmarkSortSummaryText));

    void ISortControllerContext.UpdateAllSourceTabChartsForSort() =>
        this.UpdateAllSourceTabCharts();

    bool ICpuTopologyControllerContext.IsRefreshing =>
        this.IsRefreshing;

    void ICpuTopologyControllerContext.NotifyCpuTopologyPropertyChanged(string propertyName) =>
        this.OnPropertyChanged(propertyName);

    void IRunStatePersistenceContext.SaveUiStateCore() =>
        this.SaveUiStateCore();

    void IRunStatePersistenceContext.DiscardPendingCloseStateSaveCore() =>
        this.DiscardPendingCloseStateSaveCore();

    void IRunStatePersistenceContext.DiscardUnsavedBenchmarkResultsCore() =>
        this.DiscardUnsavedBenchmarkResultsCore();

    Task IRunStatePersistenceContext.RefreshRunsForSelectedProfileAsyncCore() =>
        this.RefreshRunsForSelectedProfileAsyncCore();
}


