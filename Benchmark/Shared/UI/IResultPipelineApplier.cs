namespace UI;

// 縺薙％繧堤ｷｨ髮・☆繧句ｴ蜷・
// - snapshot 驕ｩ逕ｨ鬆・ｺ丞､画峩
// - running visual 驕・ｻｶ/譛蟆剰｡ｨ遉ｺ譎る俣縺ｮ隱ｿ謨ｴ
internal interface IResultPipelineApplier {
    void AcceptRunResult(BenchmarkResultItem result,int runGeneration,long? uiRunId);
    void AcceptRunStatus(string status);
    void ApplyRunUiSnapshot(UiSnapshot snapshot);
    void ApplyFinalLiveVisualCleanup(bool discardPendingRunUpdates = false);
}

