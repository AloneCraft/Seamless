namespace UI;

internal interface IResultPipelineApplierContext{
    void AcceptRunResultCore(BenchmarkResultItem result,int runGeneration,long? uiRunId);
    void AcceptRunStatusCore(string status);
    void ApplyRunUiSnapshotCore(UiSnapshot snapshot);
    void ApplyFinalLiveVisualCleanupCore(bool discardPendingRunUpdates);
}
