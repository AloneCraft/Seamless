namespace UI;

public static class BenchmarkAcceptancePolicy {
    public static bool 最適化タブか(BenchmarkSourceTab tab) => tab is
        BenchmarkSourceTab.OptimizationRewrite or
        BenchmarkSourceTab.OptimizationPipelineFusion or
        BenchmarkSourceTab.OptimizationPredicatePushdown;

    public static bool 回帰か(BenchmarkSourceTab tab,シェア値 時間シェア) {
        if(時間シェア.左シェア is not { } 左シェア) {
            return false;
        }
        if(最適化タブか(tab)) {
            return 左シェア>1.02/2.02;
        }
        return tab is BenchmarkSourceTab.SystemArray or BenchmarkSourceTab.SystemIEnumerable or BenchmarkSourceTab.SystemList or BenchmarkSourceTab.SystemRange
            &&左シェア>0.95/1.95;
    }

    public static bool 最適化回帰が反復したか(IEnumerable<double> 左シェア一覧) {
        var recent=左シェア一覧.Where(double.IsFinite).TakeLast(3).ToArray();
        return recent.Length==3&&recent.Count(static 左シェア=>左シェア>1.02/2.02)>=2;
    }
}
