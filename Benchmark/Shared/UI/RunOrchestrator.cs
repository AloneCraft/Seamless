namespace UI;

// ここを編集する場合:
// - Start/Resume の遷移条件変更
// - 中断後復元時の安全側状態を調整
internal sealed class RunOrchestrator:IRunOrchestrator {
    private readonly IRunOrchestratorContext context;

    public RunOrchestrator(IRunOrchestratorContext context) {
        this.context=context;
    }

    public Task RunOrResumeAsync() =>
        this.context.RunOrResumeAsyncCore();

    public Task RunInfiniteAsync() =>
        this.context.RunInfiniteAsyncCore();

    public void ToggleRun() =>
        this.context.ToggleRunCore();

    public Task StopAsync() =>
        this.context.StopAsyncCore();

    public Task PauseAsync() =>
        this.context.PauseAsyncCore();

    public Task CancelAndWaitAsync() =>
        this.context.CancelAndWaitAsyncCore();

    public void Cancel() =>
        this.context.CancelCore();
}
