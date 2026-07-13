using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkImprovementPromptBuilder{
    public const string FileName = "ImprovementPrompt.md";

    public static Benchmarkシェア概要 概要を作成(IReadOnlyList<BenchmarkComparisonRow> rows) =>
        new(
            シェア平均を計算(rows.Select(static row => row.左右シェア.時間シェア),out var timeIncluded,out var timeExcluded),
            シェア平均を計算(rows.Select(static row => row.左右シェア.メモリシェア),out var allocatedIncluded,out var allocatedExcluded),
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

    private static double? シェア平均を計算(IEnumerable<シェア値> shares,out int included,out int excluded){
        included=0;
        excluded=0;
        var sum=0.0;
        foreach(var share in shares){
            if(share.左シェア is { } value&&double.IsFinite(value)){
                sum+=value;
                included++;
            }else{
                excluded++;
            }
        }

        if(included==0){
            return null;
        }
        return sum/included;
    }
}

internal sealed record Benchmarkシェア概要(
    double? 時間左平均,
    double? メモリ左平均,
    int 時間対象数,
    int 時間除外数,
    int メモリ対象数,
    int メモリ除外数){
    public string DisplayText =>
        "時間シェア "+シェアを整形(this.時間左平均)
        +" (対象 "+this.時間対象数.ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+this.時間除外数.ToString(CultureInfo.InvariantCulture)+")"
        +" / メモリシェア "+シェアを整形(this.メモリ左平均)
        +" (対象 "+this.メモリ対象数.ToString(CultureInfo.InvariantCulture)
        +" / 除外 "+this.メモリ除外数.ToString(CultureInfo.InvariantCulture)+")";

    public string OverallDisplayText => "全体 "+this.DisplayText;

    public string ShortDisplayText =>
        "時間シェア "+シェアを整形(this.時間左平均)
        +",メモリシェア "+シェアを整形(this.メモリ左平均);

    private static string シェアを整形(double? leftShare){
        if(leftShare is not { } value||!double.IsFinite(value)){
            return "-";
        }
        return シェア値.左シェアから作成(value).表示文字列;
    }
}
