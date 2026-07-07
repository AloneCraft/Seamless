namespace UI;

// ここを編集する場合:
// - 保存順序や例外抑止の仕様変更
internal interface IRunStatePersistence {
    void SaveUiState();
    void DiscardPendingCloseStateSave();
    void DiscardUnsavedBenchmarkResults();
    Task RefreshRunsForSelectedProfileAsync();
}
