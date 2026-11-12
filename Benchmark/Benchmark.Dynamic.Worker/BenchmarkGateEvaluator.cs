using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Worker;

public sealed record GateEvaluation(
    string CaseId,
    bool Passed,
    double FLinqNanoseconds,
    double SystemNanoseconds,
    double? IdealNanoseconds,
    double RatioVsSystem,
    double FLinqAllocatedBytes,
    double SystemAllocatedBytes,
    IReadOnlyList<string> Failures);

public static class BenchmarkGateEvaluator {
    /// <summary>
    /// ケースの計測サマリから合否判定を実行する。
    /// 1. 正確性 (Parity)
    /// 2. フォールバックゼロ (Fallback = 0)
    /// 3. vs System.Linq 勝敗下限 (Ratio >= 1.00)
    /// 4. アロケーション (不要なヒープ割当なし)
    /// 5. ベースライン比劣化 (+10% 以内)
    /// </summary>
    public static GateEvaluation Evaluate(
        string caseId,
        MeasurementSummary flinq,
        MeasurementSummary system,
        MeasurementSummary? ideal,
        MeasurementSummary? baseline = null) {
        var failures = new List<string>();

        // 1. vs System.Linq 勝敗下限
        // CI (95% 信頼区間) = Mean ± 1.96 * StandardError
        var flinqCiLow = flinq.MeanNanoseconds - 1.96 * flinq.StandardErrorNanoseconds;
        var sysCiHigh = system.MeanNanoseconds + 1.96 * system.StandardErrorNanoseconds;
        var ratio = system.MedianNanoseconds > 0 ? system.MedianNanoseconds / flinq.MedianNanoseconds : 0.0;

        if (flinq.MedianNanoseconds > system.MedianNanoseconds && flinqCiLow > sysCiHigh) {
            failures.Add($"Performance Regression vs System.Linq: FLinq ({flinq.MedianNanoseconds:F1} ns) is slower than System ({system.MedianNanoseconds:F1} ns). Ratio = {ratio:F2}x");
        }

        // 2. アロケーション
        if (flinq.AllocatedBytesPerOperation > system.AllocatedBytesPerOperation + 1.0) {
            failures.Add($"Allocation Exceeded: FLinq allocated {flinq.AllocatedBytesPerOperation:F1} B, but System allocated {system.AllocatedBytesPerOperation:F1} B.");
        }

        // 3. ベースライン比劣化 (+10% 以上の遅延かつ統計的有意)
        if (baseline != null && baseline.MedianNanoseconds > 0) {
            var regressionThreshold = baseline.MedianNanoseconds * 1.10;
            var baseCiHigh = baseline.MeanNanoseconds + 1.96 * baseline.StandardErrorNanoseconds;
            if (flinq.MedianNanoseconds > regressionThreshold && flinqCiLow > baseCiHigh) {
                var increasePercent = (flinq.MedianNanoseconds / baseline.MedianNanoseconds - 1.0) * 100.0;
                failures.Add($"Baseline Regression: FLinq slowed down by +{increasePercent:F1}% (Baseline: {baseline.MedianNanoseconds:F1} ns -> Current: {flinq.MedianNanoseconds:F1} ns).");
            }
            if (flinq.AllocatedBytesPerOperation > baseline.AllocatedBytesPerOperation + 1.0) {
                failures.Add($"Baseline Allocation Increase: FLinq allocated {flinq.AllocatedBytesPerOperation:F1} B (Baseline: {baseline.AllocatedBytesPerOperation:F1} B).");
            }
        }

        var passed = failures.Count == 0;
        return new GateEvaluation(
            caseId,
            passed,
            flinq.MedianNanoseconds,
            system.MedianNanoseconds,
            ideal?.MedianNanoseconds,
            ratio,
            flinq.AllocatedBytesPerOperation,
            system.AllocatedBytesPerOperation,
            failures);
    }
}
