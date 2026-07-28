using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

using Seamless.FLinq;
namespace Test.SourceGenerator;

// 単項演算(Distinct/Reverse/Order 系)の前への Where 移動が生成コードへ実際に現れることと、
// Order 系のガード(KeepsSortedSpanFastPath)が効く側・効かない側の両方を固定する。
// 意味保存テストは押し下げが起きなくても緑になるため、形状はここで押さえる。
[LinqIntercept(false)]
public sealed partial class UnaryPushdownShapeTests(ITestOutputHelper output) {
    private const string DistinctSource = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(int[] src) => src.Distinct().Where(static v => v>777).ToList();
            public void Run(int[] src) => _ = Filter(src);
        }
        """;

    private const string ReverseSource = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(int[] src) => src.Reverse().Where(static v => v>777).ToList();
            public void Run(int[] src) => _ = Filter(src);
        }
        """;

    // ソースが配列なので KeepsSortedSpanFastPath が押し下げを止める。
    private const string OrderOnArraySource = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(int[] src) => src.Order().Where(static v => v>777).ToList();
            public void Run(int[] src) => _ = Filter(src);
        }
        """;

    // Order のソースが LINQ 呼び出しなのでガードが外れて押し下がる。Skip(0) は AlgebraicRewriter の
    // 除去対象(恒等 Select / AsEnumerable)ではないので上流ステージとして残る。
    private const string OrderAfterLinqCallSource = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(int[] src) => src.Skip(0).Order().Where(static v => v>777).ToList();
            public void Run(int[] src) => _ = Filter(src);
        }
        """;

    [Fact]
    public void DistinctPushesWhereBeforeTheHashSet() {
        var enabled = GeneratedSource(WithPushdown(DistinctSource));
        var disabled = GeneratedSource(WithoutPushdown(DistinctSource));

        GeneratedSourceAssert.True(
            PredicatePrecedesSetInsert(enabled)&&!PredicatePrecedesSetInsert(disabled),
            "Distinct pushdown must evaluate the predicate before the hash set insert; disabling it must keep the original order.",
            enabled,
            output);
    }

    [Fact]
    public void ReversePushesWhereBeforeReverse() {
        var enabled = Normalize(GeneratedSource(WithPushdown(ReverseSource)));
        var disabled = Normalize(GeneratedSource(WithoutPushdown(ReverseSource)));

        GeneratedSourceAssert.True(
            enabled!=disabled,
            "Reverse has no fast-path guard, so the predicate must move in front of it.",
            enabled,
            output);
    }

    [Fact]
    public void OrderOnArrayKeepsSortedSpanFastPath() {
        var enabled = Normalize(GeneratedSource(WithPushdown(OrderOnArraySource)));
        var disabled = Normalize(GeneratedSource(WithoutPushdown(OrderOnArraySource)));

        GeneratedSourceAssert.True(
            enabled==disabled,
            "KeepsSortedSpanFastPath must block the pushdown when Order sits directly on a span-capable source.",
            enabled,
            output);
    }

    [Fact]
    public void OrderAfterLinqCallIsPushed() {
        var enabled = GeneratedSource(WithPushdown(OrderAfterLinqCallSource));
        var disabled = GeneratedSource(WithoutPushdown(OrderAfterLinqCallSource));

        GeneratedSourceAssert.True(
            PredicatePrecedes(enabled,"Sort")&&!PredicatePrecedes(disabled,"Sort"),
            "The fast path is already lost when Order sits on another LINQ call, so the predicate must be evaluated before the sort.",
            enabled,
            output);
    }

    // 融合ループ内で述語チェックが HashSetSlim.Add より前に出ているかを見る。
    private static bool PredicatePrecedesSetInsert(string generated) => PredicatePrecedes(generated,".Add(");

    private static bool PredicatePrecedes(string generated,string marker) {
        var predicateIndex = generated.IndexOf("777",StringComparison.Ordinal);
        var markerIndex = generated.IndexOf(marker,StringComparison.Ordinal);
        return predicateIndex>=0&&markerIndex>=0&&predicateIndex<markerIndex;
    }

    // InterceptsLocationAttribute の第2引数はソース本文のチェックサムを含む base64 なので、
    // 属性テキスト(Optimizations の値)が違うだけで必ず変化する。全文比較の前に落とす。
    private static string Normalize(string generated) =>
        InterceptsLocationArgument().Replace(generated,"InterceptsLocationAttribute(_)");

    [GeneratedRegex(@"InterceptsLocationAttribute\([^)]*\)")]
    private static partial Regex InterceptsLocationArgument();

    private static string WithPushdown(string source) =>
        source.Replace("OPTIMIZATIONS","LinqOptimizations.All",StringComparison.Ordinal);

    private static string WithoutPushdown(string source) =>
        source.Replace("OPTIMIZATIONS","LinqOptimizations.All&~LinqOptimizations.PredicatePushdown",StringComparison.Ordinal);

    private static string GeneratedSource(string source) {
        var comp = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        GeneratorDriver driver = GeneratorTestHelpers.CreateDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(comp,out var outputCompilation,out var generatorDiagnostics);
        var generated = string.Concat(driver.GetRunResult().Results.SelectMany(static r => r.GeneratedSources).Select(static s => s.SourceText.ToString()));
        var diagnostics = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static d => d.Severity==DiagnosticSeverity.Error)
            .Where(static d => d.Id!="CS9137")
            .ToArray();
        GeneratedSourceAssert.NoErrors(diagnostics,generated,null);
        return generated;
    }
}
