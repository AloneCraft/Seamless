using System.Globalization;
using System.Text;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class BenchmarkImprovementCandidateBuilder{
    public const int DefaultAutoSelectCount = 10;

    public static IReadOnlyList<BenchmarkImprovementCandidate> BuildCandidates(ProgressState state){
        var entries = CreateEntries(state).ToArray();
        return BuildCandidates(entries);
    }

    public static IReadOnlyList<BenchmarkImprovementCandidate> BuildCandidatesFromComparisonRows(
        IEnumerable<BenchmarkComparisonRow> rows,
        int elementCount){
        var entries = rows
            .Where(static row => row.SourceTab is not null)
            .Select(row => new BenchmarkImprovementEntry(
                row.SourceTab,
                BenchmarkImprovementCategoryClassifier.Classify(row.MethodName),
                elementCount,
                row))
            .ToArray();
        return BuildCandidates(entries);
    }

    private static IReadOnlyList<BenchmarkImprovementCandidate> BuildCandidates(IReadOnlyList<BenchmarkImprovementEntry> entries){
        return entries
            .GroupBy(static entry => CreateCandidateKey(entry.SourceTab,entry.Category,entry.Row.MethodName),StringComparer.OrdinalIgnoreCase)
            .Select(static group => CreateCandidate(group.Key,group.ToArray()))
            .Where(static candidate => candidate.回帰あり)
            .OrderByDescending(static candidate => 回帰重要度を取得(candidate.時間シェア,candidate.メモリシェア))
            .ThenBy(static candidate => candidate.SourceTab.Id,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static candidate => candidate.MethodName,StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlySet<string> CreateDefaultSelectedKeys(IReadOnlyList<BenchmarkImprovementCandidate> candidates,int takeCount = DefaultAutoSelectCount){
        var selected = new global::System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var candidate in candidates
            .Where(static item => item.時間回帰か)
            .OrderByDescending(static item => 順位値を取得(item.時間シェア))
            .Take(takeCount)){
            selected.Add(candidate.Key);
        }

        foreach(var candidate in candidates
            .Where(static item => item.メモリ回帰か)
            .OrderByDescending(static item => 順位値を取得(item.メモリシェア))
            .Take(takeCount)){
            selected.Add(candidate.Key);
        }

        return selected;
    }

    public static string BuildPrompt(IEnumerable<BenchmarkImprovementCandidate> candidates){
        var selected = candidates
            .OrderBy(static item => item.Category,StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(static item => 回帰重要度を取得(item.時間シェア,item.メモリシェア))
            .ThenBy(static item => item.MethodName,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if(selected.Length==0){
            return "ベンチマーク改善プロンプト\n\n改善対象として選択された候補がありません。\n";
        }

        var builder = new StringBuilder();
        builder.AppendLine("ベンチマーク改善プロンプト");
        builder.AppendLine();
        builder.AppendLine("目的: 以下の選択候補すべてを、段階分けせず一気に実装・テスト・検証まで進めてください。");
        builder.AppendLine("計画や調査だけで止まらず、コード変更と再測定可能な状態まで完了させてください。");
        builder.AppendLine("比較は Left:Right のシェアです。Leftシェアが0.50より大きいほどLeft(FLinq)側の負荷が大きく、改善優先度が高いです。");
        builder.AppendLine();
        builder.AppendLine("検証方針:");
        builder.AppendLine("- 対象メソッドの単体テストを実行し、既存挙動を壊していないことを確認してください。");
        builder.AppendLine("- 改善後に同じベンチマーク条件で再測定し、MeanNanoseconds と AllocatedBytes の差分を確認してください。");
        builder.AppendLine("- 実行時 fallback カウンタ(LinqCounter.EnumerableFallback)が 0 のままであることも確認してください。");
        builder.AppendLine();

        foreach(var group in selected.GroupBy(static item => item.Category).OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)){
            builder.AppendLine("## "+group.Key);
            foreach(var candidate in group){
                AppendCandidate(builder,candidate);
            }
            builder.AppendLine();
        }

        builder.AppendLine("調査手順:");
        builder.AppendLine("- メソッド名とプロジェクト名で実装箇所を検索してください。");
        builder.AppendLine("- 列挙処理、割り当て、分岐、バッファリング、特殊化漏れを重点的に確認してください。");
        builder.AppendLine("- 変更は対象カテゴリに絞り、無関係なリファクタリングは避けてください。");
        return builder.ToString();
    }

    public static string CreateCandidateKey(BenchmarkSourceTabDefinition sourceTab,string category,string methodName) =>
        sourceTab.Id+"|"+category+"|"+methodName+"|"+sourceTab.LeftProject+"|"+sourceTab.RightProject;

    private static BenchmarkImprovementCandidate CreateCandidate(string key,IReadOnlyList<BenchmarkImprovementEntry> entries){
        var time = entries
            .OrderByDescending(static item => 順位値を取得(item.Row.左右シェア.時間シェア))
            .ThenBy(static item => item.ElementCount)
            .First();
        var allocated = entries
            .OrderByDescending(static item => 順位値を取得(item.Row.左右シェア.メモリシェア))
            .ThenBy(static item => item.ElementCount)
            .First();
        return new(
            key,
            time.SourceTab,
            time.Category,
            time.Row.MethodName,
            time.Row.Benchmark,
            time.ElementCount,
            allocated.ElementCount,
            time.Row.左右シェア.時間シェア,
            allocated.Row.左右シェア.メモリシェア,
            time.Row.Left,
            time.Row.Right,
            allocated.Row.Left,
            allocated.Row.Right,
            time.Row.TypeFullName,
            FirstText(time.Row.ArgumentKey,allocated.Row.ArgumentKey),
            time.Row.Arguments.Count>0 ? time.Row.Arguments : allocated.Row.Arguments);
    }

    private static IEnumerable<BenchmarkImprovementEntry> CreateEntries(ProgressState state){
        foreach(var group in state.Jobs
            .Where(static job => job.Status==JobStatus.Completed)
            .GroupBy(static job => new { job.SourceTab,job.ElementCount })
            .OrderBy(static group => BenchmarkSourceTabs.Get(group.Key.SourceTab).Id,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static group => group.Key.ElementCount)){
            var sourceTab = BenchmarkSourceTabs.Get(group.Key.SourceTab);
            var reportRows = group.Select(static job => job.ToReportRow()).ToArray();
            if(reportRows.Length==0){
                continue;
            }

            IReadOnlyList<BenchmarkComparisonRow> rows;
            try{
                rows=BenchmarkComparisonFormatter.BuildComparisonRows(reportRows,sourceTab,group.Key.ElementCount);
            }catch(InvalidDataException){
                continue;
            }

            foreach(var row in rows){
                yield return new BenchmarkImprovementEntry(
                    sourceTab,
                    BenchmarkImprovementCategoryClassifier.Classify(row.MethodName),
                    group.Key.ElementCount,
                    row);
            }
        }
    }

    private static void AppendCandidate(StringBuilder builder,BenchmarkImprovementCandidate candidate){
        builder.AppendLine("- "+candidate.MethodName+" ("+candidate.SourceTab.DisplayName+")");
        builder.AppendLine("  - 比較: "+candidate.SourceTab.LeftProject+" / "+candidate.SourceTab.RightProject);
        builder.AppendLine("  - 時間差: N="+FormatElementCount(candidate.TimeElementCount)+" / シェア Left:Right "+シェアを整形(candidate.時間シェア)+" / Left "+FormatTime(candidate.TimeLeft.TimeNanoseconds)+" / Right "+FormatTime(candidate.TimeRight.TimeNanoseconds));
        builder.AppendLine("  - メモリ差: N="+FormatElementCount(candidate.AllocatedElementCount)+" / シェア Left:Right "+シェアを整形(candidate.メモリシェア)+" / Left "+FormatAllocated(candidate.AllocatedLeft.AllocatedBytes)+" / Right "+FormatAllocated(candidate.AllocatedRight.AllocatedBytes));
        if(!string.IsNullOrWhiteSpace(candidate.TypeFullName)){
            builder.AppendLine("  - 型: "+candidate.TypeFullName);
        }
        if(!string.IsNullOrWhiteSpace(candidate.ArgumentKey)){
            builder.AppendLine("  - 引数: "+candidate.ArgumentKey);
        }
        builder.AppendLine("  - 検索ヒント: rg \""+candidate.MethodName+"|"+candidate.SourceTab.LeftProject+"|"+candidate.SourceTab.LeftAssemblyName+"\"");
    }

    internal static double 順位値を取得(シェア値 share){
        if(share.左シェア is { } value&&value>0.5){
            return value;
        }
        return double.NegativeInfinity;
    }

    internal static double 回帰重要度を取得(シェア値 timeShare,シェア値 allocatedShare){
        var timeSeverity=0.0;
        if(timeShare.左シェア is { } timeLeft&&timeLeft>0.5){
            timeSeverity=(timeLeft-0.5)*2;
        }
        var allocatedSeverity=0.0;
        if(allocatedShare.左シェア is { } allocatedLeft&&allocatedLeft>0.5){
            allocatedSeverity=(allocatedLeft-0.5)*2;
        }
        return Math.Max(timeSeverity,allocatedSeverity);
    }

    internal static string シェアを整形(シェア値 share) => share.表示文字列;

    internal static string FormatTime(double? value) =>
        value is { } number&&double.IsFinite(number)
            ? BenchmarkFormatting.FormatNanoseconds(number)
            : "-";

    internal static string FormatAllocated(long? value) =>
        value is { } number
            ? BenchmarkFormatting.FormatBytes(number)
            : "-";

    private static string FormatElementCount(int value) =>
        value.ToString("N0",CultureInfo.InvariantCulture);

    private static string FirstText(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))??string.Empty;

    private sealed record BenchmarkImprovementEntry(
        BenchmarkSourceTabDefinition SourceTab,
        string Category,
        int ElementCount,
        BenchmarkComparisonRow Row);
}

internal static class BenchmarkImprovementCategoryClassifier{
    public static string Classify(string methodName){
        if(methodName.StartsWith("Aggregate",StringComparison.OrdinalIgnoreCase)){
            return "Aggregate";
        }
        if(methodName.StartsWith("Any",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("All",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Count",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Contains",StringComparison.OrdinalIgnoreCase)){
            return "Predicate/Count";
        }
        if(methodName.StartsWith("Distinct",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Except",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Intersect",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Union",StringComparison.OrdinalIgnoreCase)){
            return "Set";
        }
        if(methodName.StartsWith("Order",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Then",StringComparison.OrdinalIgnoreCase)){
            return "Ordering";
        }
        if(methodName.StartsWith("Select",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Where",StringComparison.OrdinalIgnoreCase)){
            return "Projection/Filter";
        }
        if(methodName.StartsWith("Group",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("ToLookup",StringComparison.OrdinalIgnoreCase)){
            return "Grouping";
        }
        if(methodName.StartsWith("Skip",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Take",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("ElementAt",StringComparison.OrdinalIgnoreCase)){
            return "Indexing";
        }
        if(methodName.StartsWith("Append",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Prepend",StringComparison.OrdinalIgnoreCase)
            ||methodName.StartsWith("Concat",StringComparison.OrdinalIgnoreCase)){
            return "Concatenation";
        }
        return "Other";
    }
}

public sealed record BenchmarkImprovementCandidate(
    string Key,
    BenchmarkSourceTabDefinition SourceTab,
    string Category,
    string MethodName,
    string DisplayName,
    int TimeElementCount,
    int AllocatedElementCount,
    シェア値 時間シェア,
    シェア値 メモリシェア,
    ProjectMetrics TimeLeft,
    ProjectMetrics TimeRight,
    ProjectMetrics AllocatedLeft,
    ProjectMetrics AllocatedRight,
    string TypeFullName,
    string ArgumentKey,
    IReadOnlyList<BenchmarkArgument> Arguments){
    public bool 時間回帰か => BenchmarkImprovementCandidateBuilder.順位値を取得(this.時間シェア)>0.5;
    public bool メモリ回帰か => this.メモリシェア.左シェア>0.5;
    public bool 回帰あり => this.時間回帰か||this.メモリ回帰か;
}
