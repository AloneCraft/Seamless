namespace UI;

// ここを編集する場合:
// - VM Core 保存処理呼び出しの窓口を変更
internal interface IRunStatePersistenceContext {
    void SaveUiStateCore();
    void DiscardPendingCloseStateSaveCore();
    void DiscardUnsavedBenchmarkResultsCore();
    Task RefreshRunsForSelectedProfileAsyncCore();
}
