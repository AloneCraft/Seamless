namespace UI;

// ここを編集する場合:
// - VM 側状態を追加/変更するか
// - Sort 検証や Chart 反映トリガーの順序を変える場合
internal interface ISortControllerContext {
    ObservableCollection<SortRule> SortRules { get; }
    ObservableCollection<SortColumnOption> SortColumnOptions { get; }
    IReadOnlyList<SourceTabViewState> SourceTabViewStates { get; }
    SourceTabDefinition SelectedSourceDefinition { get; }
    bool HasBenchmarkTypeColumn { get; set; }
    GridSortColumn CurrentSortColumn { get; set; }
    bool CurrentSortDescending { get; set; }
    bool IsRunActive { get; }
    bool IsPaused { get; }
    IUiPreferences UiPreferences { get; }
    void NotifySortSummaryChanged();
    void UpdateAllSourceTabChartsForSort();
}
