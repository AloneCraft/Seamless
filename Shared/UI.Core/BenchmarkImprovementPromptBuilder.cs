using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkImprovementPromptBuilder{
    public const string FileName = "ImprovementPrompt.md";

    public static BenchmarkRatioSummary CreateSummary(IReadOnlyList<BenchmarkComparisonRow> rows) =>
        new(
            CalculateGeometricMean(rows.Select(static row => row.PairRatio.TimeRatio),out var timeIncluded,out var timeExcluded),
            CalculateAllocationMean(rows.Select(static row => row.PairRatio.AllocatedShare),out var allocatedIncluded,out var allocatedExcluded),
            timeIncluded,
            timeExcluded,
            allocatedIncluded,
            allocatedExcluded);

    public static string BuildPrompt(ProgressState state){
        var candidates = BenchmarkImprovementCandidateBuilder.BuildCandidates(state);
        var selectedKeys = BenchmarkImprovementCandidateBuilder.CreateDefaultSelectedKeys(candidates);
        return BenchmarkImprovementCandidateBuilder.BuildPrompt(candidates.Where(candidate => selectedKeys.Contains(candidate.Key)));
    }

    public static string BuildPrompt(IEnumerable<BenchmarkImprovementCandidate> candidates) =>
        BenchmarkImprovementCandidateBuilder.BuildPrompt(candidates);

    private static double? CalculateGeometricMean(IEnumerable<RatioValue> ratios,out int included,out int excluded){
        included=0;
        excluded=0;
        var logSum = 0.0;
        foreach(var ratio in ratios){
            if(ratio.NumericValue is { } value&&value>0&&double.IsFinite(value)){
                logSum+=Math.Log(value);
                included++;
            }else{
                excluded++;
            }
        }

        return included==0
            ? null
            : Math.Exp(logSum/included);
    }

    private static double? CalculateAllocationMean(IEnumerable<AllocationShareValue> shares,out int included,out int excluded){
        included=0;
        excluded=0;
        var sum = 0.0;
        foreach(var share in shares){
            if(share.LeftShare is { } value&&double.IsFinite(value)){
                sum+=value;
                included++;
            }else{
                excluded++;
            }
        }
        return included==0 ? null : sum/included;
    }
}

internal sealed record BenchmarkRatioSummary(
    double? TimeGeometricMean,
    double? AllocatedLeftMean,
    int TimeIncludedCount,
    int TimeExcludedCount,
    int AllocatedIncludedCount,
    int AllocatedExcludedCount){
    public string DisplayText =>
        "時間比 "+FormatRatio(this.TimeGeometricMean)
        +" (対象 "+this.TimeIncludedCount.ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+this.TimeExcludedCount.ToString(CultureInfo.InvariantCulture)+")"
        +" / メモリシェア "+FormatShare(this.AllocatedLeftMean)
        +" (対象 "+this.AllocatedIncludedCount.ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+this.AllocatedExcludedCount.ToString(CultureInfo.InvariantCulture)+")";

    public string OverallDisplayText => "全体 "+this.DisplayText;

    public string ShortDisplayText =>
        "時間比 "+FormatRatio(this.TimeGeometricMean)
        +",メモリシェア "+FormatShare(this.AllocatedLeftMean);

    private static string FormatRatio(double? value) =>
        value is { } number&&double.IsFinite(number)
            ? number.ToString("0.00",CultureInfo.InvariantCulture)
            : "-";

    private static string FormatShare(double? leftShare) =>
        leftShare is { } value&&double.IsFinite(value)
            ? AllocationShareValue.FromLeftShare(value).DisplayText
            : "-";
}
