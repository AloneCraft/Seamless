using System.Text;
using System.Text.Json;

namespace Benchmark.Audit;

/// <summary>multi-run の1行(1ケース)から取り出した実装ごとの計測。</summary>
internal sealed record MeasuredCase(string Key,string Tab,string Type,string Method,bool DigestMatched,IReadOnlyDictionary<string,MeasuredImplementation> Implementations);

internal sealed record MeasuredImplementation(string Key,double MedianNanoseconds,double[] Samples,double AllocatedBytes,string WarmupState);

/// <summary>行の判定。</summary>
internal enum GapClass {
    /// <summary>2回とも、ぶれを超えて理想より遅い(改善対象)。</summary>
    改善対象,
    /// <summary>ぶれは超えるが差が3%未満(記録のみ)。</summary>
    小さい差,
    /// <summary>2回とも、ぶれを超えて理想より速い(理想版の見直し対象)。</summary>
    理想より速い,
    /// <summary>ぶれの範囲内。</summary>
    同程度,
    /// <summary>ダイジェスト不一致・時間切れ・欠測で判定しない。</summary>
    判定不能,
}

internal sealed record GapRow(string Key,string Tab,string Type,string Method,double Noise,double Gap1,double Gap2,double FLinqNanoseconds,double IdealNanoseconds,double BaselineNanoseconds,GapClass Class,string Note);

/// <summary>
/// 同じビルドを2回測った multi-run の JSONL から、行ごとのぶれ e と、FLinq の理想比 g=ln(FLinq/理想) を求めて分類する。
/// e は「2回の間の各実装の対数比の最大」と「1回の中の各実装の(四分位範囲/中央値)/2 の最大」の大きい方。
/// 改善対象は min(g1,g2)&gt;e の行。採否基準(3%)に届かないものは小さい差として記録だけする。
/// </summary>
internal static class MeasurementAnalysis {
    private const double AcceptanceThreshold = 0.0295588022415444; // ln(1.03)

    public static IReadOnlyList<MeasuredCase> Read(string path) {
        var cases = new List<MeasuredCase>();
        foreach(var line in File.ReadLines(path)) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var result = root.GetProperty("Result");
            var implementations = new Dictionary<string,MeasuredImplementation>(StringComparer.Ordinal);
            var matched = result.TryGetProperty("DigestMatched",out var digest)&&digest.GetBoolean();
            if(result.TryGetProperty("Implementations",out var list)) {
                foreach(var item in list.EnumerateArray()) {
                    var key = item.GetProperty("Key").GetString()!;
                    if(item.GetProperty("Measurement").ValueKind!=JsonValueKind.Object) {
                        continue;
                    }
                    var measurement = item.GetProperty("Measurement");
                    implementations[key]=new MeasuredImplementation(
                        key,
                        measurement.GetProperty("中央値ナノ秒").GetDouble(),
                        measurement.GetProperty("ナノ秒サンプル").EnumerateArray().Select(static value => value.GetDouble()).ToArray(),
                        measurement.GetProperty("割当バイト数").GetDouble(),
                        item.GetProperty("WarmupState").GetString()??string.Empty);
                }
            }
            var tab = root.GetProperty("Tab").GetString()!;
            var type = root.GetProperty("Type").GetString()!;
            var method = root.GetProperty("Method").GetString()!;
            var argument = string.Empty;
            if(root.TryGetProperty("ArgumentKey",out var argumentKey)) {
                argument=argumentKey.GetString()??string.Empty;
            }
            cases.Add(new MeasuredCase(tab+"|"+type+"|"+method+"|"+argument,tab,type,method,matched,implementations));
        }
        return cases;
    }

    /// <summary>1回の中のぶれ。(四分位範囲/中央値)/2。</summary>
    public static double WithinRunSpread(IReadOnlyList<double> samples) {
        if(samples.Count<4) {
            return 0;
        }
        var ordered = samples.Order().ToArray();
        var median = ordered[ordered.Length/2];
        if(median<=0) {
            return 0;
        }
        var lower = ordered[ordered.Length/4];
        var upper = ordered[ordered.Length*3/4];
        return (upper-lower)/median/2;
    }

    public static IReadOnlyList<GapRow> Classify(IReadOnlyList<MeasuredCase> first,IReadOnlyList<MeasuredCase> second) {
        var secondByKey = new Dictionary<string,MeasuredCase>(StringComparer.Ordinal);
        foreach(var item in second) {
            secondByKey[item.Key]=item;
        }
        var rows = new List<GapRow>();
        foreach(var a in first) {
            if(!secondByKey.TryGetValue(a.Key,out var b)) {
                continue;
            }
            rows.Add(ClassifyOne(a,b));
        }
        return rows;
    }

    private static GapRow ClassifyOne(MeasuredCase a,MeasuredCase b) {
        string[] keys = ["FLinq","Ideal","Baseline"];
        var complete = a.DigestMatched&&b.DigestMatched&&keys.All(key => a.Implementations.ContainsKey(key)&&b.Implementations.ContainsKey(key));
        if(!complete) {
            return new GapRow(a.Key,a.Tab,a.Type,a.Method,0,0,0,0,0,0,GapClass.判定不能,"ダイジェスト不一致または欠測");
        }
        var timedOut = keys.Any(key => a.Implementations[key].WarmupState!="安定"||b.Implementations[key].WarmupState!="安定");
        var gap1 = Math.Log(a.Implementations["FLinq"].MedianNanoseconds/a.Implementations["Ideal"].MedianNanoseconds);
        var gap2 = Math.Log(b.Implementations["FLinq"].MedianNanoseconds/b.Implementations["Ideal"].MedianNanoseconds);
        // 判定するのは比なので、ぶれも比の周回間の差で測る。プロセスごとの速さの変動は3者に共通にかかる分が比で打ち消され、
        // 実装ごとの周回間の差(2026-10-02 のベースラインで中央値11%)より比の差(同5%)のほうが小さい。
        // 周回内のばらつきは比較する2つ(FLinq・理想)のものを下限にする。
        var noise = Math.Abs(gap1-gap2);
        foreach(var key in (string[])["FLinq","Ideal"]) {
            noise=Math.Max(noise,WithinRunSpread(a.Implementations[key].Samples));
            noise=Math.Max(noise,WithinRunSpread(b.Implementations[key].Samples));
        }
        var flinq = Math.Sqrt(a.Implementations["FLinq"].MedianNanoseconds*b.Implementations["FLinq"].MedianNanoseconds);
        var ideal = Math.Sqrt(a.Implementations["Ideal"].MedianNanoseconds*b.Implementations["Ideal"].MedianNanoseconds);
        var baseline = Math.Sqrt(a.Implementations["Baseline"].MedianNanoseconds*b.Implementations["Baseline"].MedianNanoseconds);
        GapClass gapClass;
        if(Math.Min(gap1,gap2)>noise) {
            if(Math.Min(gap1,gap2)<AcceptanceThreshold) {
                gapClass=GapClass.小さい差;
            } else {
                gapClass=GapClass.改善対象;
            }
        } else if(Math.Max(gap1,gap2)< -noise) {
            gapClass=GapClass.理想より速い;
        } else {
            gapClass=GapClass.同程度;
        }
        var note = string.Empty;
        if(timedOut) {
            note="JIT待機の時間切れあり";
        }
        return new GapRow(a.Key,a.Tab,a.Type,a.Method,noise,gap1,gap2,flinq,ideal,baseline,gapClass,note);
    }

    public static string ToMarkdown(IReadOnlyList<GapRow> rows) {
        var builder = new StringBuilder();
        builder.AppendLine("| 分類 | タブ | 型 | ベンチ | FLinq/理想 | ぶれ | FLinq ns | 理想 ns | 比較相手 ns | 備考 |");
        builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---|");
        foreach(var row in rows.OrderBy(static row => row.Class).ThenByDescending(static row => Math.Min(row.Gap1,row.Gap2))) {
            var type = row.Type[(row.Type.LastIndexOf('.')+1)..];
            builder.Append("| ").Append(row.Class)
                .Append(" | ").Append(row.Tab)
                .Append(" | ").Append(type)
                .Append(" | ").Append(row.Method)
                .Append(" | ").Append(Math.Exp((row.Gap1+row.Gap2)/2).ToString("F2",System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(row.Noise.ToString("P1",System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(row.FLinqNanoseconds.ToString("F0",System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(row.IdealNanoseconds.ToString("F0",System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(row.BaselineNanoseconds.ToString("F0",System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(row.Note)
                .AppendLine(" |");
        }
        return builder.ToString();
    }
}
