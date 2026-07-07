namespace UI;

// ここを編集する場合:
// - CPU 選択状態を追加/取得する仕様変更
// - 例外的な CPU セクション更新ルールの追加
internal interface ICpuTopologyController {
    void Initialize();
    void ToggleCpu(CpuSelectionItem cpu);
    void ToggleCpuSection(CpuTableSection section);
    void ToggleCpuColumn(CpuTableSection section,int columnIndex);
    void ToggleCpuRow(CpuTableRow row);
    void ApplyCpuSelection(IEnumerable<CpuSelectionItem> cpus,bool selected);
    void EnsureAtLeastOneCpuSelected(CpuSelectionItem? fallback);
    void RefreshSelectedLogicalCpus();
    void RebuildCpuSections();
    IReadOnlyList<int> GetSelectedLogicalCpus();
}
