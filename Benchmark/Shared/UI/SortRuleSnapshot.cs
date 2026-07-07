namespace UI;

// ここを編集する場合:
// - 既存設定保存データの互換項目を追加する場合
internal readonly record struct SortRuleSnapshot(GridSortColumn Column,bool Descending);
