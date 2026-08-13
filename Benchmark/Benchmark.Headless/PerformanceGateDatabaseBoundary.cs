using UI;

namespace Benchmark.Headless;

internal static class PerformanceGateDatabaseBoundary{
    public static PerformanceGateSaveResult?判定後更新を実行(
        ベンチマーク性能ゲート判定 判定,
        long?退役実行ID,
        Func<PerformanceGateSaveResult>合格時保存){
        ArgumentNullException.ThrowIfNull(判定);
        ArgumentNullException.ThrowIfNull(合格時保存);
        if(!判定.合格か||退役実行ID is null){
            return null;
        }
        return 合格時保存();
    }
}
