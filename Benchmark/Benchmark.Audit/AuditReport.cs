using System.Text;

namespace Benchmark.Audit;

/// <summary>監査結果を Markdown の表にする。数値は「ループ内/ループ外」。</summary>
internal static class AuditReport {
    public static string ToMarkdown(IReadOnlyList<AuditRow> rows) {
        var builder = new StringBuilder();
        builder.AppendLine("| アセンブリ | ソース | ベンチ | 置換 | IL | ループ(深さ) | IF呼出 | Invoke | 割当 | box | 計数器 | 外部LINQ | 補助型(未使用) | ライブラリ呼出 |");
        builder.AppendLine("|---|---|---|---|---:|---|---|---|---|---|---|---:|---|---|");
        foreach(var row in rows.OrderBy(static row => row.Assembly,StringComparer.Ordinal).ThenBy(static row => row.DeclaringType,StringComparer.Ordinal).ThenBy(static row => row.Method,StringComparer.Ordinal)) {
            var intercepted = "×";
            if(row.Intercepted) {
                intercepted="○";
            }
            builder.Append("| ").Append(ShortAssembly(row.Assembly))
                .Append(" | ").Append(row.Source)
                .Append(" | ").Append(row.Method)
                .Append(" | ").Append(intercepted)
                .Append(" | ").Append(row.IlSize)
                .Append(" | ").Append(row.LoopCount).Append('(').Append(row.MaxLoopDepth).Append(')')
                .Append(" | ").Append(row.InterfaceCallsInLoop).Append('/').Append(row.InterfaceCallsOutsideLoop)
                .Append(" | ").Append(row.DelegateInvokesInLoop).Append('/').Append(row.DelegateInvokesOutsideLoop)
                .Append(" | ").Append(row.AllocationsInLoop).Append('/').Append(row.AllocationsOutsideLoop)
                .Append(" | ").Append(row.BoxesInLoop).Append('/').Append(row.BoxesOutsideLoop)
                .Append(" | ").Append(row.CounterCallsInLoop).Append('/').Append(row.CounterCallsOutsideLoop)
                .Append(" | ").Append(row.ExternalLinqCalls)
                .Append(" | ").Append(row.HelperTypes).Append('(').Append(row.UnusedHelperTypes).Append(')')
                .Append(" | ").Append(string.Join("<br>",row.LibraryCalls.Select(ShortCall)))
                .AppendLine(" |");
        }
        return builder.ToString();
    }

    private static string ShortAssembly(string name) => name.Replace("Benchmark.System.Linq.Enumerable","SLE",StringComparison.Ordinal);

    private static string ShortCall(string call) {
        var separator = call.LastIndexOf("::",StringComparison.Ordinal);
        if(separator<0) {
            return call;
        }
        var type = call[..separator];
        var dot = type.LastIndexOf('.');
        return type[(dot+1)..]+call[separator..];
    }
}
