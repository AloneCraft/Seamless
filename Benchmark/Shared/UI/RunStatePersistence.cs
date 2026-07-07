namespace UI;

// ここを編集する場合:
// - 保存/読込の失敗時挙動
// - 選択状態復元の追加保存項目
internal sealed class RunStatePersistence:IRunStatePersistence {
    private readonly IRunStatePersistenceContext context;

    public RunStatePersistence(IRunStatePersistenceContext context) {
        this.context=context;
    }

    public void SaveUiState() =>
        this.context.SaveUiStateCore();

    public void DiscardPendingCloseStateSave() =>
        this.context.DiscardPendingCloseStateSaveCore();

    public void DiscardUnsavedBenchmarkResults() =>
        this.context.DiscardUnsavedBenchmarkResultsCore();

    public Task RefreshRunsForSelectedProfileAsync() =>
        this.context.RefreshRunsForSelectedProfileAsyncCore();
}
