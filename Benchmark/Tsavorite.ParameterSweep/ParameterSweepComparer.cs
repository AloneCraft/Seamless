namespace Tsavorite.ParameterSweep;

public static class ParameterSweepComparer{
    public static IReadOnlyList<ParameterSweepComparisonRow> Compare(
        IReadOnlyList<ParameterSweepResult> current,
        IReadOnlyList<ParameterSweepResult> baseline){
        var baselineByCase = baseline.ToDictionary(static item => item.CaseName,StringComparer.OrdinalIgnoreCase);
        return current
            .Select(item => CreateRow(item,baselineByCase.GetValueOrDefault(item.CaseName)))
            .ToArray();
    }

    private static ParameterSweepComparisonRow CreateRow(ParameterSweepResult current,ParameterSweepResult? baseline){
        if(baseline is null){
            return new(current,null,null,null,null,false);
        }
        return new(
            current,
            baseline,
            current.MaxSuccessfulCount-baseline.MaxSuccessfulCount,
            CreateRatio(current.AddElapsedMilliseconds,baseline.AddElapsedMilliseconds),
            current.LookupMeanNanoseconds is { } currentLookup&&baseline.LookupMeanNanoseconds is { } baselineLookup
                ? CreateRatio(currentLookup,baselineLookup)
                : null,
            current.FailureKind!=baseline.FailureKind);
    }

    private static double? CreateRatio(double current,double baseline) =>
        baseline>0 ? current/baseline : null;
}
