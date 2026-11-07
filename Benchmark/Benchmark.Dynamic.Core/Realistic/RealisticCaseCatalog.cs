using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic.Core.Realistic;

public enum RealisticSourceKind {
    Array,
    List,
    IEnumerable,
}

public sealed record RealisticQueryCase(
    string ID,
    string Category,
    string Origin,
    string PrimaryType,
    string? SecondaryType,
    string Expression,
    string? ZLinqExpression,
    string? IdealArrayBody,
    string? IdealListBody,
    string? IdealEnumerableBody,
    string ResultType,
    string Cardinality,
    bool Unordered = false);

public static class RealisticCaseCatalog {
    private static readonly List<RealisticQueryCase> Cases = [];

    static RealisticCaseCatalog() {
        // 初期サンプルクエリ登録（Q01〜Q05等、取り込みフォーマットに合わせた初期定義）
        RegisterDefaults();
    }

    public static IReadOnlyList<RealisticQueryCase> All => Cases;

    public static void Register(RealisticQueryCase queryCase) {
        Cases.RemoveAll(c => c.ID == queryCase.ID);
        Cases.Add(queryCase);
    }

    public static string Emit(RealisticQueryCase item, RealisticSourceKind sourceKind, int flags = 15) {
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Runtime.InteropServices;");
        builder.AppendLine("using ZLinq;");
        builder.AppendLine("using Benchmark.Dynamic.Core;");
        builder.AppendLine("using Benchmark.Dynamic.Core.Realistic;");
        builder.AppendLine("namespace Benchmark.Dynamic.Generated;");
        builder.AppendLine("public sealed class RealisticQuery {");

        var order = item.Unordered ? "Unordered" : "Ordered";
        var materialize = $"global::Benchmark.Dynamic.Core.DynamicQueryValue.FromObject(result, global::Benchmark.Dynamic.Core.QueryValueType.{item.ResultType}, global::Benchmark.Dynamic.Core.QueryCardinality.{item.Cardinality}, global::Benchmark.Dynamic.Core.QueryOrderSemantics.{order})";

        var (primaryCast, secondaryCast) = GetCastExpressions(item, sourceKind);

        // System, FLinqUnoptimized, FLinq
        foreach (var method in new[] { "RunSystem", "RunFLinqUnoptimized", "RunFLinq" }) {
            var core = method;
            if (method != "RunSystem") {
                core += "Core";
                builder.AppendLine($"public static global::Benchmark.Dynamic.Core.DynamicQueryValue {method}(object? primary, object? secondary) => {core}(primary, secondary);");
                var options = method == "RunFLinq" ? flags : 0;
                builder.AppendLine($"[global::Seamless.FLinq.LinqIntercept(true, Optimizations=(global::Seamless.FLinq.LinqOptimizations){options}, ConvergenceMode=global::Seamless.FLinq.LinqConvergenceMode.GlobalFixedPoint, EmitOptimizationTrace=true)]");
            }
            builder.AppendLine($"public static global::Benchmark.Dynamic.Core.DynamicQueryValue {core}(object? primary, object? secondary) {{");
            builder.AppendLine($"    var {primaryCast};");
            if (!string.IsNullOrEmpty(secondaryCast)) {
                builder.AppendLine($"    var {secondaryCast};");
            }
            builder.AppendLine($"    var result = {item.Expression};");
            builder.AppendLine($"    return {materialize};");
            builder.AppendLine("}");
        }

        // ZLinq
        if (!string.IsNullOrEmpty(item.ZLinqExpression)) {
            builder.AppendLine("public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunZLinq(object? primary, object? secondary) {");
            builder.AppendLine($"    var {primaryCast};");
            if (!string.IsNullOrEmpty(secondaryCast)) {
                builder.AppendLine($"    var {secondaryCast};");
            }
            builder.AppendLine($"    var result = {item.ZLinqExpression};");
            builder.AppendLine($"    return {materialize};");
            builder.AppendLine("}");
        }

        // Ideal
        var idealBody = sourceKind switch {
            RealisticSourceKind.Array => item.IdealArrayBody,
            RealisticSourceKind.List => item.IdealListBody,
            RealisticSourceKind.IEnumerable => item.IdealEnumerableBody,
            _ => item.IdealArrayBody
        };

        if (!string.IsNullOrEmpty(idealBody)) {
            builder.AppendLine("public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunIdeal(object? primary, object? secondary) {");
            builder.AppendLine($"    var result = IdealBody(primary, secondary);");
            builder.AppendLine($"    return {materialize};");
            builder.AppendLine("}");
            builder.AppendLine("private static object IdealBody(object? primary, object? secondary) {");
            builder.AppendLine($"    var {primaryCast};");
            if (!string.IsNullOrEmpty(secondaryCast)) {
                builder.AppendLine($"    var {secondaryCast};");
            }
            builder.AppendLine(idealBody);
            builder.AppendLine("}");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static (string PrimaryCast, string? SecondaryCast) GetCastExpressions(RealisticQueryCase item, RealisticSourceKind kind) {
        var pType = item.PrimaryType;
        var pVar = GetVariableName(pType);
        var sType = item.SecondaryType;
        var sVar = sType != null ? GetVariableName(sType) : null;

        var pTypeString = FormatType(pType, kind);
        var pCast = $"{pVar} = ({pTypeString})primary!";

        string? sCast = null;
        if (sType != null && sVar != null) {
            var sTypeString = FormatType(sType, kind);
            sCast = $"{sVar} = ({sTypeString})secondary!";
        }

        return (pCast, sCast);
    }

    private static string FormatType(string elementTypeName, RealisticSourceKind kind) {
        return kind switch {
            RealisticSourceKind.Array => $"{elementTypeName}[]",
            RealisticSourceKind.List => $"global::System.Collections.Generic.List<{elementTypeName}>",
            RealisticSourceKind.IEnumerable => $"global::System.Collections.Generic.IEnumerable<{elementTypeName}>",
            _ => $"{elementTypeName}[]"
        };
    }

    private static string GetVariableName(string elementTypeName) {
        return elementTypeName switch {
            "Trade" => "trades",
            "Account" => "accounts",
            "Security" => "securities",
            "Customer" => "customers",
            "SalesOrder" => "orders",
            "int" => "source",
            _ => "source"
        };
    }

    private static void RegisterDefaults() {
        // Q01: int[] Where(p % 2 == 0).Select((p, p + 1)).ToArray()
        Register(new RealisticQueryCase(
            "Q01",
            "1.FilterProject",
            "App",
            "int",
            null,
            "source.Where(p => p % 2 == 0).Select(p => (p, q: p + 1)).ToArray()",
            "source.AsValueEnumerable().Where(p => p % 2 == 0).Select(p => (p, q: p + 1)).ToArray()",
            // Array
            """
            int count = 0;
            for (int i = 0; i < source.Length; i++) {
                if (source[i] % 2 == 0) count++;
            }
            var res = new (int p, int q)[count];
            int idx = 0;
            for (int i = 0; i < source.Length; i++) {
                int p = source[i];
                if (p % 2 == 0) {
                    res[idx++] = (p, p + 1);
                }
            }
            return res;
            """,
            // List
            """
            ReadOnlySpan<int> span = CollectionsMarshal.AsSpan(source);
            int count = 0;
            for (int i = 0; i < span.Length; i++) {
                if (span[i] % 2 == 0) count++;
            }
            var res = new (int p, int q)[count];
            int idx = 0;
            for (int i = 0; i < span.Length; i++) {
                int p = span[i];
                if (p % 2 == 0) {
                    res[idx++] = (p, p + 1);
                }
            }
            return res;
            """,
            // Enumerable
            """
            var list = new List<(int p, int q)>();
            foreach (var p in source) {
                if (p % 2 == 0) {
                    list.Add((p, p + 1));
                }
            }
            return list.ToArray();
            """,
            "Element",
            "Sequence"));
    }
}
