namespace UI;

// ここを編集する場合:
// - CPU トポロジ描画状態の更新イベントを追加する場合
// - VM 側の選択同期タイミングを変更する場合
internal interface ICpuTopologyControllerContext {
    ObservableCollection<CpuSelectionItem> CpuItems { get; }
    ObservableCollection<CpuTableSection> CpuSections { get; }
    bool IsRefreshing { get; }
    void NotifyCpuTopologyPropertyChanged(string propertyName);
}
