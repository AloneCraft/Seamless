namespace UI;

// ここを編集する場合:
// - VM Core 実装の責務境界を見直す際の入口
internal interface IRunOrchestratorContext {
    Task RunOrResumeAsyncCore();
    Task RunInfiniteAsyncCore();
    void ToggleRunCore();
    Task StopAsyncCore();
    Task PauseAsyncCore();
    Task CancelAndWaitAsyncCore();
    void CancelCore();
}
