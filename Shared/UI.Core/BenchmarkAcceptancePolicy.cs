namespace UI;

public static class BenchmarkAcceptancePolicy {
    public static bool IsOptimizationTab(BenchmarkSourceTab tab) => tab is
        BenchmarkSourceTab.OptimizationRewrite or
        BenchmarkSourceTab.OptimizationPipelineFusion or
        BenchmarkSourceTab.OptimizationPredicatePushdown;

    public static bool IsRegression(BenchmarkSourceTab tab,RatioValue ratio) {
        if(ratio.Kind!=RatioValueKind.Numeric||ratio.NumericValue is not { } value) {
            return false;
        }
        if(IsOptimizationTab(tab)) {
            return value>1.02;
        }
        return tab is BenchmarkSourceTab.SystemArray or BenchmarkSourceTab.SystemIEnumerable or BenchmarkSourceTab.SystemList or BenchmarkSourceTab.SystemRange
            &&value>0.95;
    }

    public static bool IsRepeatedOptimizationRegression(IEnumerable<double> leftOverRightRatios) {
        var recent=leftOverRightRatios.Where(double.IsFinite).TakeLast(3).ToArray();
        return recent.Length==3&&recent.Count(static ratio=>ratio>1.02)>=2;
    }
}
