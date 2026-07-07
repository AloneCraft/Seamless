namespace UI;

// ここを編集する場合:
// - 実行系トランジションの順序を変更
// - 取り消し/停止挙動を追加
internal interface IRunOrchestrator {
    Task RunOrResumeAsync();
    Task RunInfiniteAsync();
    void ToggleRun();
    Task StopAsync();
    Task PauseAsync();
    Task CancelAndWaitAsync();
    void Cancel();
}
