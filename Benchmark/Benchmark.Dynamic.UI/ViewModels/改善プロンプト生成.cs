using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic.UI;

public static class 改善プロンプト生成 {
    public static string 速度(動的結果行 行) {
        ArgumentNullException.ThrowIfNull(行);
        var Builder=共通情報(行);
        Builder.AppendLine("## 依頼");
        Builder.AppendLine("このLINQ Queryの速度改善案を分析してください。");
        Builder.AppendLine("意味を変えず、System.Linqへの実行時フォールバックは禁止です。");
        Builder.AppendLine("FLinq/ZLinqの経路と最適化Traceを確認し、改善候補、根拠、想定リスク、追加測定案を示してください。");
        Builder.AppendLine("1ms程度の測定値はノイズの影響を受けるため、中央値・分布・再現性を考慮してください。");
        Builder.AppendLine();
        Builder.AppendLine("## 速度計測");
        行を追加(Builder,"System median ns/op",行.System中央値);
        行を追加(Builder,"FLinq median ns/op",行.FLinq中央値);
        行を追加(Builder,"FLinq/System ratio",行.FLinq比);
        行を追加(Builder,"ZLinq median ns/op",行.ZLinq中央値);
        行を追加(Builder,"ZLinq/System ratio",行.ZLinq比);
        計測概要を追加(Builder,行.計測詳細,速度指標:true);
        Builder.AppendLine("\n制約: 意味保存、System.Linqフォールバック禁止、同一条件で再現可能な改善であること。");
        return Builder.ToString();
    }

    public static string メモリ(動的結果行 行) {
        ArgumentNullException.ThrowIfNull(行);
        var Builder=共通情報(行);
        Builder.AppendLine("## 依頼");
        Builder.AppendLine("このLINQ Queryのメモリ割り当て削減案を分析してください。");
        Builder.AppendLine("bytes/op、raw allocation、GC差分を根拠にし、速度だけを理由にした変更は避けてください。");
        Builder.AppendLine("中間materialize、buffer、配列生成、列挙器、ValueEnumerableの寿命を確認してください。");
        Builder.AppendLine("意味保存、Dispose、ALC解放、同一thread制約を維持し、推測ではなく測定可能な仮説として提案してください。");
        Builder.AppendLine();
        Builder.AppendLine("## メモリ計測");
        行を追加(Builder,"System allocated bytes/op",行.System割当);
        行を追加(Builder,"FLinq allocated bytes/op",行.FLinq割当);
        行を追加(Builder,"ZLinq allocated bytes/op",行.ZLinq割当);
        計測概要を追加(Builder,行.計測詳細,速度指標:false);
        Builder.AppendLine("\n制約: fixture／ALCの寿命を越えて値を返さないこと。GC、UI、DB、コンパイル時間は割り当て測定へ混入させないこと。");
        return Builder.ToString();
    }

    private static StringBuilder 共通情報(動的結果行 行) {
        var Builder=new StringBuilder();
        Builder.AppendLine("# Benchmark.Dynamic 改善分析依頼");
        Builder.AppendLine("以下は保存済みの1 Queryから生成した情報です。未取得値は推測せず、未取得として扱ってください。");
        Builder.AppendLine();
        Builder.AppendLine("## Query");
        行を追加(Builder,"Run ID",行.実行ID);
        行を追加(Builder,"Query ID",行.QueryID);
        行を追加(Builder,"Query Index",行.QueryIndex.ToString(CultureInfo.InvariantCulture));
        行を追加(Builder,"SourceTarget",行.対象.ToString());
        行を追加(Builder,"Expression",安全な式(行));
        行を追加(Builder,"Short expression",行.短縮式);
        行を追加(Builder,"N",行.N.ToString(CultureInfo.InvariantCulture));
        行を追加(Builder,"Depth",行.深度.ToString(CultureInfo.InvariantCulture));
        行を追加(Builder,"Base seed",行.BaseSeed);
        行を追加(Builder,"Query seed",行.QuerySeed);
        行を追加(Builder,"Input element count",行.入力要素数);
        行を追加(Builder,"Operator set",行.演算子集合);
        行を追加(Builder,"Status",行.状態);
        行を追加(Builder,"Meaning",行.意味一致);
        行を追加(Builder,"Compile time",行.コンパイル時間);
        行を追加(Builder,"Rewrite count",行.RewriteCount.ToString(CultureInfo.InvariantCulture));
        行を追加(Builder,"Fixed point",行.FixedPoint);
        Builder.AppendLine();
        コードを追加(Builder,"IR",行.IR);
        コードを追加(Builder,"Generated source",行.生成Source);
        コードを追加(Builder,"Validation results",行.結果);
        コードを追加(Builder,"Optimization trace",行.最適化Trace);
        コードを追加(Builder,"Diagnostics",行.診断);
        return Builder;
    }

    private static void 計測概要を追加(StringBuilder Builder,string JSON,bool 速度指標) {
        if(string.IsNullOrWhiteSpace(JSON)) {
            Builder.AppendLine("未取得");
            return;
        }
        try {
            using var Document=JsonDocument.Parse(JSON);
            var Root=Document.RootElement;
            if(プロパティ(Root,"measurement",out var Measurement)
                &&プロパティ(Measurement,"measurements",out var Measurements)
                &&Measurements.ValueKind==JsonValueKind.Array) {
                foreach(var Summary in Measurements.EnumerateArray()) {
                    var Implementation=文字列(Summary,"implementation");
                    Builder.AppendLine($"### {Implementation}");
                    if(速度指標) {
                        数値を追加(Builder,Summary,"medianNanoseconds","median ns/op");
                        数値を追加(Builder,Summary,"meanNanoseconds","mean ns/op");
                        数値を追加(Builder,Summary,"minimumNanoseconds","minimum ns/op");
                        数値を追加(Builder,Summary,"maximumNanoseconds","maximum ns/op");
                        数値を追加(Builder,Summary,"standardDeviationNanoseconds","standard deviation ns/op");
                        数値を追加(Builder,Summary,"standardErrorNanoseconds","standard error ns/op");
                    }else{
                        数値を追加(Builder,Summary,"allocatedBytesPerOperation","allocated bytes/op");
                        数値を追加(Builder,Summary,"gen0Collections","Gen0 collections");
                        数値を追加(Builder,Summary,"gen1Collections","Gen1 collections");
                        数値を追加(Builder,Summary,"gen2Collections","Gen2 collections");
                    }
                    数値を追加(Builder,Summary,"operationCount","operation count");
                    数値を追加(Builder,Summary,"warmupCount","warmup count");
                    数値を追加(Builder,Summary,"sampleCount","sample count");
                    コードを追加(Builder,"raw samples",Summary.GetRawText());
                }
            }else{
                コードを追加(Builder,"measurement JSON",JSON);
            }
        }catch(JsonException){
            コードを追加(Builder,"measurement JSON",JSON);
        }
    }

    private static void 数値を追加(StringBuilder Builder,JsonElement Element,string Name,string Label) {
        Builder.AppendLine($"- {Label}: {文字列(Element,Name)}");
    }

    private static string 文字列(JsonElement Element,string Name) {
        return プロパティ(Element,Name,out var Value)
            ?Value.ValueKind switch {
                JsonValueKind.String=>Value.GetString()??"未取得",
                JsonValueKind.Number=>Value.GetRawText(),
                JsonValueKind.True=>"true",
                JsonValueKind.False=>"false",
                _=>Value.GetRawText(),
            }
            :"未取得";
    }

    private static bool プロパティ(JsonElement Element,string Name,out JsonElement Value) {
        if(Element.ValueKind==JsonValueKind.Object) {
            foreach(var Property in Element.EnumerateObject()) {
                if(string.Equals(Property.Name,Name,StringComparison.OrdinalIgnoreCase)) {
                    Value=Property.Value;
                    return true;
                }
            }
        }
        Value=default;
        return false;
    }

    private static void 行を追加(StringBuilder Builder,string Name,string? Value) {
        Builder.Append("- ").Append(Name).Append(": ").AppendLine(string.IsNullOrWhiteSpace(Value)?"未取得":Value);
    }

    private static string 安全な式(動的結果行 行) {
        try {
            return 行.クエリ式;
        }catch(Exception 例外) when(例外 is JsonException
            or ArgumentException
            or InvalidOperationException
            or NullReferenceException) {
            return 行.短縮式;
        }
    }

    private static void コードを追加(StringBuilder Builder,string Name,string? Value) {
        Builder.AppendLine($"## {Name}");
        Builder.AppendLine("~~~");
        Builder.AppendLine(string.IsNullOrWhiteSpace(Value)?"未取得":Value);
        Builder.AppendLine("~~~");
    }
}
