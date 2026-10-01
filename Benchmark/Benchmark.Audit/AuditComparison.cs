using System.Text;

namespace Benchmark.Audit;

/// <summary>FLinq と手書き理想版の同じ行の IL 指標の組。</summary>
internal sealed record ComparisonRow(string Key,SourceKind Source,string Method,AuditRow FLinq,AuditRow Ideal) {
    /// <summary>ループ内の動的な呼び出し・割り当てが理想版より多い指標の名前(多ければ理想形から外れている)。</summary>
    public IReadOnlyList<string> WorseThanIdeal {
        get {
            var worse = new List<string>();
            if(this.FLinq.InterfaceCallsInLoop>this.Ideal.InterfaceCallsInLoop) {
                worse.Add("ループ内IF呼出");
            }
            if(this.FLinq.DelegateInvokesInLoop>this.Ideal.DelegateInvokesInLoop) {
                worse.Add("ループ内Invoke");
            }
            if(this.FLinq.AllocationsInLoop>this.Ideal.AllocationsInLoop) {
                worse.Add("ループ内割当");
            }
            if(this.FLinq.BoxesInLoop>this.Ideal.BoxesInLoop) {
                worse.Add("ループ内box");
            }
            if(this.FLinq.CounterCallsInLoop+this.FLinq.CounterCallsOutsideLoop>0) {
                worse.Add("計数器");
            }
            if(this.FLinq.UnusedHelperTypes>0) {
                worse.Add("未使用補助型");
            }
            return worse;
        }
    }
}

/// <summary>FLinq と理想版の監査結果を行ごとに突き合わせる。</summary>
internal static class AuditComparison {
    /// <summary>行キー。単一演算子は「ソース種別|メソッド名」、複合連鎖は「型の完全名|メソッド名」(型名は全最適化版と理想版で同じ)。</summary>
    public static string KeyOf(AuditRow row) {
        var type = row.DeclaringType;
        if(type is "Shared.Linq.Base`1" or "Shared.Linq.Ideal.IdealBase`1" or "Shared.Linq.Range_start_count" or "Shared.Linq.IdealRange.Range_start_count") {
            return row.Source+"|"+row.Method;
        }
        return type+"|"+row.Method;
    }

    public static IReadOnlyList<ComparisonRow> Compare(IReadOnlyList<AuditRow> flinq,IReadOnlyList<AuditRow> ideal) {
        var idealByKey = new Dictionary<string,AuditRow>(StringComparer.Ordinal);
        foreach(var row in ideal) {
            idealByKey.TryAdd(KeyOf(row),row);
        }
        var rows = new List<ComparisonRow>();
        foreach(var row in flinq) {
            if(idealByKey.TryGetValue(KeyOf(row),out var match)) {
                rows.Add(new ComparisonRow(KeyOf(row),row.Source,row.Method,row,match));
            }
        }
        return rows;
    }

    public static string ToMarkdown(IReadOnlyList<ComparisonRow> rows) {
        var builder = new StringBuilder();
        builder.AppendLine("数値は「FLinq / 理想」。IF呼出・Invoke・割当は「ループ内」の静的な出現数。");
        builder.AppendLine();
        builder.AppendLine("| ソース | 行 | IL | ループ内IF呼出 | ループ内Invoke | ループ内割当 | 計数器 | 理想より多い指標 |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach(var row in rows.OrderBy(static row => row.Source).ThenBy(static row => row.Key,StringComparer.Ordinal)) {
            var name = row.Key[(row.Key.IndexOf('|')+1)..];
            if(row.Key.Contains('.',StringComparison.Ordinal)) {
                var type = row.Key[..row.Key.IndexOf('|')];
                name=type[(type.LastIndexOf('.')+1)..]+"."+name;
            }
            builder.Append("| ").Append(row.Source)
                .Append(" | ").Append(name)
                .Append(" | ").Append(row.FLinq.IlSize).Append('/').Append(row.Ideal.IlSize)
                .Append(" | ").Append(row.FLinq.InterfaceCallsInLoop).Append('/').Append(row.Ideal.InterfaceCallsInLoop)
                .Append(" | ").Append(row.FLinq.DelegateInvokesInLoop).Append('/').Append(row.Ideal.DelegateInvokesInLoop)
                .Append(" | ").Append(row.FLinq.AllocationsInLoop).Append('/').Append(row.Ideal.AllocationsInLoop)
                .Append(" | ").Append(row.FLinq.CounterCallsInLoop+row.FLinq.CounterCallsOutsideLoop)
                .Append(" | ").Append(string.Join("、",row.WorseThanIdeal))
                .AppendLine(" |");
        }
        return builder.ToString();
    }
}
