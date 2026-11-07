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
            ReadOnlySpan<int> span = source.AsSpan();
            int count = 0;
            foreach (var p in span) {
                if (p % 2 == 0) count++;
            }
            var res = GC.AllocateUninitializedArray<(int p, int q)>(count);
            int idx = 0;
            foreach (var p in span) {
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
            foreach (var p in span) {
                if (p % 2 == 0) count++;
            }
            var res = GC.AllocateUninitializedArray<(int p, int q)>(count);
            int idx = 0;
            foreach (var p in span) {
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

        // Q02: Trade[] GroupBy(Status).Select((Status, Count)).ToArray()
        Register(new RealisticQueryCase(
            "Q02",
            "2.Aggregate",
            "FinApp",
            "Trade",
            null,
            "trades.GroupBy(t => (byte)t.Status).Select(g => (Status: g.Key, Count: g.Count())).ToArray()",
            null,
            // Array
            """
            ReadOnlySpan<Trade> span = trades.AsSpan();
            if (span.Length == 0) return Array.Empty<(byte Status, int Count)>();
            Span<int> counts = stackalloc int[4];
            counts.Clear();
            Span<byte> order = stackalloc byte[4];
            int groupCount = 0;
            for (int i = 0; i < span.Length; i++) {
                byte s = (byte)span[i].Status;
                if (counts[s]++ == 0) {
                    order[groupCount++] = s;
                }
            }
            var res = GC.AllocateUninitializedArray<(byte Status, int Count)>(groupCount);
            for (int i = 0; i < groupCount; i++) {
                byte s = order[i];
                res[i] = (s, counts[s]);
            }
            return res;
            """,
            // List
            """
            ReadOnlySpan<Trade> span = CollectionsMarshal.AsSpan(trades);
            if (span.Length == 0) return Array.Empty<(byte Status, int Count)>();
            Span<int> counts = stackalloc int[4];
            counts.Clear();
            Span<byte> order = stackalloc byte[4];
            int groupCount = 0;
            for (int i = 0; i < span.Length; i++) {
                byte s = (byte)span[i].Status;
                if (counts[s]++ == 0) {
                    order[groupCount++] = s;
                }
            }
            var res = GC.AllocateUninitializedArray<(byte Status, int Count)>(groupCount);
            for (int i = 0; i < groupCount; i++) {
                byte s = order[i];
                res[i] = (s, counts[s]);
            }
            return res;
            """,
            // Enumerable
            """
            Span<int> counts = stackalloc int[4];
            counts.Clear();
            Span<byte> order = stackalloc byte[4];
            int groupCount = 0;
            foreach (var t in trades) {
                byte s = (byte)t.Status;
                if (counts[s]++ == 0) {
                    order[groupCount++] = s;
                }
            }
            var res = GC.AllocateUninitializedArray<(byte Status, int Count)>(groupCount);
            for (int i = 0; i < groupCount; i++) {
                byte s = order[i];
                res[i] = (s, counts[s]);
            }
            return res;
            """,
            "Element",
            "Sequence"));

        // Q03: SalesOrder[] Where(Amount > 100 && Coupon != null).GroupBy(CustomerId).Select((CustomerId, Sum(Amount))).ToArray()
        Register(new RealisticQueryCase(
            "Q03",
            "2.Aggregate",
            "CommerceApp",
            "SalesOrder",
            null,
            "orders.Where(o => o.Amount > 100m && o.CouponCode != null).GroupBy(o => o.CustomerId).Select(g => (CustomerId: g.Key, Total: g.Sum(o => o.Amount))).ToArray()",
            null,
            // Array
            """
            ReadOnlySpan<SalesOrder> span = orders.AsSpan();
            if (span.Length == 0) return Array.Empty<(int CustomerId, decimal Total)>();
            using var map = new global::Seamless.Linq.DictionarySlim<int, decimal>(null);
            for (int i = 0; i < span.Length; i++) {
                var o = span[i];
                if (o.Amount > 100m && o.CouponCode != null) {
                    ref var sum = ref map.GetValueRefOrAddDefault(o.CustomerId, out _);
                    sum += o.Amount;
                }
            }
            var res = GC.AllocateUninitializedArray<(int CustomerId, decimal Total)>(map.Count);
            for (int i = 0; i < map.Count; i++) {
                map.GetKeyAndValue(i, out var k, out var v);
                res[i] = (k, v);
            }
            return res;
            """,
            // List
            """
            ReadOnlySpan<SalesOrder> span = CollectionsMarshal.AsSpan(orders);
            if (span.Length == 0) return Array.Empty<(int CustomerId, decimal Total)>();
            using var map = new global::Seamless.Linq.DictionarySlim<int, decimal>(null);
            for (int i = 0; i < span.Length; i++) {
                var o = span[i];
                if (o.Amount > 100m && o.CouponCode != null) {
                    ref var sum = ref map.GetValueRefOrAddDefault(o.CustomerId, out _);
                    sum += o.Amount;
                }
            }
            var res = GC.AllocateUninitializedArray<(int CustomerId, decimal Total)>(map.Count);
            for (int i = 0; i < map.Count; i++) {
                map.GetKeyAndValue(i, out var k, out var v);
                res[i] = (k, v);
            }
            return res;
            """,
            // Enumerable
            """
            using var map = new global::Seamless.Linq.DictionarySlim<int, decimal>(null);
            foreach (var o in orders) {
                if (o.Amount > 100m && o.CouponCode != null) {
                    ref var sum = ref map.GetValueRefOrAddDefault(o.CustomerId, out _);
                    sum += o.Amount;
                }
            }
            var res = GC.AllocateUninitializedArray<(int CustomerId, decimal Total)>(map.Count);
            for (int i = 0; i < map.Count; i++) {
                map.GetKeyAndValue(i, out var k, out var v);
                res[i] = (k, v);
            }
            return res;
            """,
            "Element",
            "Sequence",
            Unordered: true));

        // Q04: Trade[] Join Account[] on AccountId == Id
        Register(new RealisticQueryCase(
            "Q04",
            "3.Join",
            "FinApp",
            "Trade",
            "Account",
            "trades.Join(accounts, t => t.AccountId, a => a.Id, (t, a) => (t.Id, a.CustomerId, Total: t.Quantity * t.Price)).ToArray()",
            null,
            // Array
            """
            ReadOnlySpan<Trade> tSpan = trades.AsSpan();
            ReadOnlySpan<Account> aSpan = accounts.AsSpan();
            if (tSpan.Length == 0 || aSpan.Length == 0) return Array.Empty<(long, long, decimal)>();
            using var map = new global::Seamless.Linq.DictionarySlim<long, long>(aSpan.Length, null);
            for (int i = 0; i < aSpan.Length; i++) {
                map.GetValueRefOrAddDefault(aSpan[i].Id, out _) = aSpan[i].CustomerId;
            }
            var list = new List<(long, long, decimal)>(tSpan.Length);
            for (int i = 0; i < tSpan.Length; i++) {
                var t = tSpan[i];
                ref readonly var custId = ref map.GetValueRefOrNullRef(t.AccountId);
                if (!System.Runtime.CompilerServices.Unsafe.IsNullRef(in custId)) {
                    list.Add((t.Id, custId, t.Quantity * t.Price));
                }
            }
            return list.ToArray();
            """,
            // List
            """
            ReadOnlySpan<Trade> tSpan = CollectionsMarshal.AsSpan(trades);
            ReadOnlySpan<Account> aSpan = CollectionsMarshal.AsSpan(accounts);
            if (tSpan.Length == 0 || aSpan.Length == 0) return Array.Empty<(long, long, decimal)>();
            using var map = new global::Seamless.Linq.DictionarySlim<long, long>(aSpan.Length, null);
            for (int i = 0; i < aSpan.Length; i++) {
                map.GetValueRefOrAddDefault(aSpan[i].Id, out _) = aSpan[i].CustomerId;
            }
            var list = new List<(long, long, decimal)>(tSpan.Length);
            for (int i = 0; i < tSpan.Length; i++) {
                var t = tSpan[i];
                ref readonly var custId = ref map.GetValueRefOrNullRef(t.AccountId);
                if (!System.Runtime.CompilerServices.Unsafe.IsNullRef(in custId)) {
                    list.Add((t.Id, custId, t.Quantity * t.Price));
                }
            }
            return list.ToArray();
            """,
            // Enumerable
            """
            using var map = new global::Seamless.Linq.DictionarySlim<long, long>(null);
            foreach (var a in accounts) {
                map.GetValueRefOrAddDefault(a.Id, out _) = a.CustomerId;
            }
            var list = new List<(long, long, decimal)>();
            foreach (var t in trades) {
                ref readonly var custId = ref map.GetValueRefOrNullRef(t.AccountId);
                if (!System.Runtime.CompilerServices.Unsafe.IsNullRef(in custId)) {
                    list.Add((t.Id, custId, t.Quantity * t.Price));
                }
            }
            return list.ToArray();
            """,
            "Element",
            "Sequence"));

        // Q05: Trade[] OrderByDescending(Price).ThenBy(Quantity).Take(10).ToArray()
        Register(new RealisticQueryCase(
            "Q05",
            "4.OrderTake",
            "FinApp",
            "Trade",
            null,
            "trades.OrderByDescending(t => t.Price).ThenBy(t => t.Quantity).Take(10).ToArray()",
            null,
            // Array
            """
            ReadOnlySpan<Trade> span = trades.AsSpan();
            if (span.Length == 0) return Array.Empty<Trade>();
            int k = Math.Min(10, span.Length);
            Span<int> topIndices = stackalloc int[10];
            int count = 0;
            for (int i = 0; i < span.Length; i++) {
                ref readonly var t = ref span[i];
                if (count < 10) {
                    int pos = count;
                    while (pos > 0) {
                        ref readonly var prev = ref span[topIndices[pos - 1]];
                        int cmp = t.Price.CompareTo(prev.Price);
                        if (cmp > 0 || (cmp == 0 && t.Quantity.CompareTo(prev.Quantity) < 0)) {
                            topIndices[pos] = topIndices[pos - 1];
                            pos--;
                        } else {
                            break;
                        }
                    }
                    topIndices[pos] = i;
                    count++;
                } else {
                    ref readonly var last = ref span[topIndices[9]];
                    int cmp = t.Price.CompareTo(last.Price);
                    if (cmp > 0 || (cmp == 0 && t.Quantity.CompareTo(last.Quantity) < 0)) {
                        int pos = 9;
                        while (pos > 0) {
                            ref readonly var prev = ref span[topIndices[pos - 1]];
                            int c = t.Price.CompareTo(prev.Price);
                            if (c > 0 || (c == 0 && t.Quantity.CompareTo(prev.Quantity) < 0)) {
                                topIndices[pos] = topIndices[pos - 1];
                                pos--;
                            } else {
                                break;
                            }
                        }
                        topIndices[pos] = i;
                    }
                }
            }
            var res = GC.AllocateUninitializedArray<Trade>(count);
            for (int i = 0; i < count; i++) {
                res[i] = span[topIndices[i]];
            }
            return res;
            """,
            // List
            """
            ReadOnlySpan<Trade> span = CollectionsMarshal.AsSpan(trades);
            if (span.Length == 0) return Array.Empty<Trade>();
            int k = Math.Min(10, span.Length);
            Span<int> topIndices = stackalloc int[10];
            int count = 0;
            for (int i = 0; i < span.Length; i++) {
                ref readonly var t = ref span[i];
                if (count < 10) {
                    int pos = count;
                    while (pos > 0) {
                        ref readonly var prev = ref span[topIndices[pos - 1]];
                        int cmp = t.Price.CompareTo(prev.Price);
                        if (cmp > 0 || (cmp == 0 && t.Quantity.CompareTo(prev.Quantity) < 0)) {
                            topIndices[pos] = topIndices[pos - 1];
                            pos--;
                        } else {
                            break;
                        }
                    }
                    topIndices[pos] = i;
                    count++;
                } else {
                    ref readonly var last = ref span[topIndices[9]];
                    int cmp = t.Price.CompareTo(last.Price);
                    if (cmp > 0 || (cmp == 0 && t.Quantity.CompareTo(last.Quantity) < 0)) {
                        int pos = 9;
                        while (pos > 0) {
                            ref readonly var prev = ref span[topIndices[pos - 1]];
                            int c = t.Price.CompareTo(prev.Price);
                            if (c > 0 || (c == 0 && t.Quantity.CompareTo(prev.Quantity) < 0)) {
                                topIndices[pos] = topIndices[pos - 1];
                                pos--;
                            } else {
                                break;
                            }
                        }
                        topIndices[pos] = i;
                    }
                }
            }
            var res = GC.AllocateUninitializedArray<Trade>(count);
            for (int i = 0; i < count; i++) {
                res[i] = span[topIndices[i]];
            }
            return res;
            """,
            // Enumerable
            """
            var top = new global::Benchmark.Dynamic.Core.Realistic.Trade[10];
            int count = 0;
            foreach (var t in trades) {
                if (count < 10) {
                    int pos = count;
                    while (pos > 0) {
                        ref readonly var prev = ref top[pos - 1];
                        int cmp = t.Price.CompareTo(prev.Price);
                        if (cmp > 0 || (cmp == 0 && t.Quantity.CompareTo(prev.Quantity) < 0)) {
                            top[pos] = top[pos - 1];
                            pos--;
                        } else {
                            break;
                        }
                    }
                    top[pos] = t;
                    count++;
                } else {
                    ref readonly var last = ref top[9];
                    int cmp = t.Price.CompareTo(last.Price);
                    if (cmp > 0 || (cmp == 0 && t.Quantity.CompareTo(last.Quantity) < 0)) {
                        int pos = 9;
                        while (pos > 0) {
                            ref readonly var prev = ref top[pos - 1];
                            int c = t.Price.CompareTo(prev.Price);
                            if (c > 0 || (c == 0 && t.Quantity.CompareTo(prev.Quantity) < 0)) {
                                top[pos] = top[pos - 1];
                                pos--;
                            } else {
                                break;
                            }
                        }
                        top[pos] = t;
                    }
                }
            }
            if (count == 0) return global::System.Array.Empty<global::Benchmark.Dynamic.Core.Realistic.Trade>();
            if (count < 10) {
                var trimmed = new global::Benchmark.Dynamic.Core.Realistic.Trade[count];
                global::System.Array.Copy(top, trimmed, count);
                return trimmed;
            }
            return top;
            """,
            "Element",
            "Sequence"));

        // Q06: Account[] GroupJoin Trade[] on Id == AccountId -> (Id, TradeCount, TotalVolume)
        Register(new RealisticQueryCase(
            "Q06",
            "3.Join",
            "FinApp",
            "Account",
            "Trade",
            "accounts.GroupJoin(trades, a => a.Id, t => t.AccountId, (a, g) => (a.Id, TradeCount: g.Count(), TotalVolume: g.Sum(t => (long)t.Quantity))).ToArray()",
            null,
            // Array
            """
            ReadOnlySpan<Account> aSpan = accounts.AsSpan();
            ReadOnlySpan<Trade> tSpan = trades.AsSpan();
            if (aSpan.Length == 0) return Array.Empty<(long, int, long)>();
            using var tradeStats = new global::Seamless.Linq.DictionarySlim<long, (int Count, long Volume)>(tSpan.Length, null);
            for (int i = 0; i < tSpan.Length; i++) {
                ref var stat = ref tradeStats.GetValueRefOrAddDefault(tSpan[i].AccountId, out _);
                stat.Count++;
                stat.Volume += tSpan[i].Quantity;
            }
            var res = GC.AllocateUninitializedArray<(long, int, long)>(aSpan.Length);
            for (int i = 0; i < aSpan.Length; i++) {
                var a = aSpan[i];
                ref readonly var stat = ref tradeStats.GetValueRefOrNullRef(a.Id);
                if (!System.Runtime.CompilerServices.Unsafe.IsNullRef(in stat)) {
                    res[i] = (a.Id, stat.Count, stat.Volume);
                } else {
                    res[i] = (a.Id, 0, 0L);
                }
            }
            return res;
            """,
            // List
            """
            ReadOnlySpan<Account> aSpan = CollectionsMarshal.AsSpan(accounts);
            ReadOnlySpan<Trade> tSpan = CollectionsMarshal.AsSpan(trades);
            if (aSpan.Length == 0) return Array.Empty<(long, int, long)>();
            using var tradeStats = new global::Seamless.Linq.DictionarySlim<long, (int Count, long Volume)>(tSpan.Length, null);
            for (int i = 0; i < tSpan.Length; i++) {
                ref var stat = ref tradeStats.GetValueRefOrAddDefault(tSpan[i].AccountId, out _);
                stat.Count++;
                stat.Volume += tSpan[i].Quantity;
            }
            var res = GC.AllocateUninitializedArray<(long, int, long)>(aSpan.Length);
            for (int i = 0; i < aSpan.Length; i++) {
                var a = aSpan[i];
                ref readonly var stat = ref tradeStats.GetValueRefOrNullRef(a.Id);
                if (!System.Runtime.CompilerServices.Unsafe.IsNullRef(in stat)) {
                    res[i] = (a.Id, stat.Count, stat.Volume);
                } else {
                    res[i] = (a.Id, 0, 0L);
                }
            }
            return res;
            """,
            // Enumerable
            """
            using var tradeStats = new global::Seamless.Linq.DictionarySlim<long, (int Count, long Volume)>(null);
            foreach (var t in trades) {
                ref var stat = ref tradeStats.GetValueRefOrAddDefault(t.AccountId, out _);
                stat.Count++;
                stat.Volume += t.Quantity;
            }
            var list = new List<(long, int, long)>();
            foreach (var a in accounts) {
                ref readonly var stat = ref tradeStats.GetValueRefOrNullRef(a.Id);
                if (!System.Runtime.CompilerServices.Unsafe.IsNullRef(in stat)) {
                    list.Add((a.Id, stat.Count, stat.Volume));
                } else {
                    list.Add((a.Id, 0, 0L));
                }
            }
            return list.ToArray();
            """,
            "Element",
            "Sequence"));

        // Q07: Trade[] Where(IsCash).Select(Price * Quantity).Sum()
        Register(new RealisticQueryCase(
            "Q07",
            "1.FilterProject",
            "FinApp",
            "Trade",
            null,
            "trades.Where(t => t.IsCash).Select(t => t.Price * t.Quantity).Sum()",
            null,
            // Array
            """
            ReadOnlySpan<Trade> span = trades.AsSpan();
            decimal sum = 0m;
            foreach (var t in span) {
                if (t.IsCash) {
                    sum += t.Price * t.Quantity;
                }
            }
            return sum;
            """,
            // List
            """
            ReadOnlySpan<Trade> span = CollectionsMarshal.AsSpan(trades);
            decimal sum = 0m;
            foreach (var t in span) {
                if (t.IsCash) {
                    sum += t.Price * t.Quantity;
                }
            }
            return sum;
            """,
            // Enumerable
            """
            decimal sum = 0m;
            foreach (var t in trades) {
                if (t.IsCash) {
                    sum += t.Price * t.Quantity;
                }
            }
            return sum;
            """,
            "Element",
            "Scalar"));

        // Q08: Customer[] Where(IsPremium).Select(Id).Distinct().Count()
        Register(new RealisticQueryCase(
            "Q08",
            "2.Aggregate",
            "CommerceApp",
            "Customer",
            null,
            "customers.Where(c => c.IsPremium).Select(c => c.Id).Distinct().Count()",
            null,
            // Array
            """
            ReadOnlySpan<Customer> span = customers.AsSpan();
            if (span.Length == 0) return 0;
            using var set = new global::Seamless.Linq.ValueHashSetSlim<int>(span.Length, null);
            int count = 0;
            for (int i = 0; i < span.Length; i++) {
                var c = span[i];
                if (c.IsPremium && set.Add(c.Id)) {
                    count++;
                }
            }
            return count;
            """,
            // List
            """
            ReadOnlySpan<Customer> span = CollectionsMarshal.AsSpan(customers);
            if (span.Length == 0) return 0;
            using var set = new global::Seamless.Linq.ValueHashSetSlim<int>(span.Length, null);
            int count = 0;
            for (int i = 0; i < span.Length; i++) {
                var c = span[i];
                if (c.IsPremium && set.Add(c.Id)) {
                    count++;
                }
            }
            return count;
            """,
            // Enumerable
            """
            using var set = new global::Seamless.Linq.ValueHashSetSlim<int>(16, null);
            int count = 0;
            foreach (var c in customers) {
                if (c.IsPremium && set.Add(c.Id)) {
                    count++;
                }
            }
            return count;
            """,
            "Int",
            "Scalar"));

        // Q09: Security[] Where(Sector == 'Tech').OrderBy(LastPrice).Select(Symbol).FirstOrDefault()
        Register(new RealisticQueryCase(
            "Q09",
            "4.OrderTake",
            "FinApp",
            "Security",
            null,
            "securities.Where(s => s.Sector == \"Technology\").OrderBy(s => s.LastPrice).Select(s => s.Symbol).FirstOrDefault()",
            null,
            // Array
            """
            ReadOnlySpan<Security> span = securities.AsSpan();
            string? bestSymbol = null;
            decimal minPrice = decimal.MaxValue;
            foreach (var s in span) {
                if (s.Sector == "Technology" && (bestSymbol == null || s.LastPrice < minPrice)) {
                    minPrice = s.LastPrice;
                    bestSymbol = s.Symbol;
                }
            }
            return bestSymbol ?? "";
            """,
            // List
            """
            ReadOnlySpan<Security> span = CollectionsMarshal.AsSpan(securities);
            string? bestSymbol = null;
            decimal minPrice = decimal.MaxValue;
            foreach (var s in span) {
                if (s.Sector == "Technology" && (bestSymbol == null || s.LastPrice < minPrice)) {
                    minPrice = s.LastPrice;
                    bestSymbol = s.Symbol;
                }
            }
            return bestSymbol ?? "";
            """,
            // Enumerable
            """
            string? bestSymbol = null;
            decimal minPrice = decimal.MaxValue;
            foreach (var s in securities) {
                if (s.Sector == "Technology" && (bestSymbol == null || s.LastPrice < minPrice)) {
                    minPrice = s.LastPrice;
                    bestSymbol = s.Symbol;
                }
            }
            return bestSymbol ?? "";
            """,
            "Element",
            "Scalar"));

        // Q10: SalesOrder[] Any(Status == Delivered && Amount > 1000)
        Register(new RealisticQueryCase(
            "Q10",
            "1.FilterProject",
            "CommerceApp",
            "SalesOrder",
            null,
            "orders.Any(o => o.Status == OrderStatus.Delivered && o.Amount > 1000m)",
            null,
            // Array
            """
            ReadOnlySpan<SalesOrder> span = orders.AsSpan();
            foreach (var o in span) {
                if (o.Status == OrderStatus.Delivered && o.Amount > 1000m) {
                    return true;
                }
            }
            return false;
            """,
            // List
            """
            ReadOnlySpan<SalesOrder> span = CollectionsMarshal.AsSpan(orders);
            foreach (var o in span) {
                if (o.Status == OrderStatus.Delivered && o.Amount > 1000m) {
                    return true;
                }
            }
            return false;
            """,
            // Enumerable
            """
            foreach (var o in orders) {
                if (o.Status == OrderStatus.Delivered && o.Amount > 1000m) {
                    return true;
                }
            }
            return false;
            """,
            "Bool",
            "Scalar"));
    }
}
