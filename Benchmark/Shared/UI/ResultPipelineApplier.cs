namespace UI;

// 縺薙％繧堤ｷｨ髮・☆繧句ｴ蜷・
// - 蜿ｯ隕門喧蜿肴丐鬆・・繝ｭ繧ｸ繝・け螟画峩
// - 螳溯｡御ｸｭ繧ｵ繝ｳ繝励Ν陦ｨ遉ｺ繧ｿ繧､繝槭・縺ｮ豕ｨ蜈･
internal sealed class ResultPipelineApplier:IResultPipelineApplier {
    private readonly IResultPipelineApplierContext context;
    private readonly IRunVisualScheduler visualScheduler;

    public ResultPipelineApplier(IResultPipelineApplierContext context,IRunVisualScheduler visualScheduler) {
        this.context=context;
        this.visualScheduler=visualScheduler;
    }

    public void AcceptRunResult(BenchmarkResultItem result,int runGeneration,long? uiRunId) =>
        this.context.AcceptRunResultCore(result,runGeneration,uiRunId);

    public void AcceptRunStatus(string status) =>
        this.context.AcceptRunStatusCore(status);

    public void ApplyRunUiSnapshot(UiSnapshot snapshot) =>
        this.context.ApplyRunUiSnapshotCore(snapshot);

    public void ApplyFinalLiveVisualCleanup(bool discardPendingRunUpdates = false) =>
        this.context.ApplyFinalLiveVisualCleanupCore(discardPendingRunUpdates);

    internal DateTimeOffset UtcNow => this.visualScheduler.UtcNow;

    internal Task DelayAsync(TimeSpan delay,CancellationToken cancellationToken = default) =>
        this.visualScheduler.DelayAsync(delay,cancellationToken);
}

