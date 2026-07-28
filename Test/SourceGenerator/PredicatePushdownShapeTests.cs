using Microsoft.CodeAnalysis;

using Seamless.FLinq;
namespace Test.SourceGenerator;

// Join(...).Where(p) の葉移動が生成コードへ実際に現れることを確認する。
// 意味保存テストは押し下げが起きなくても緑になるため、形状はここで固定する。
[LinqIntercept(false)]
public sealed class PredicatePushdownShapeTests(ITestOutputHelper output) {
    private const string BothSideConjuncts = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(Item[] outer,Item[] inner) =>
                outer.Join(inner,static o => o.Key,static i => i.Key,static (o,i) => (o,i))
                    .Where(static x => x.o.Id>2&&x.i.Id<13)
                    .Select(static x => x.o.Id*100+x.i.Id)
                    .ToList();
            public void Run(Item[] outer,Item[] inner) => _ = Filter(outer,inner);
        }
        """;

    private const string ResidualConjunct = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public class Container {
            [LinqIntercept(true,Optimizations=LinqOptimizations.All)]
            private static List<int> Filter(Item[] outer,Item[] inner) =>
                outer.Join(inner,static o => o.Key,static i => i.Key,static (o,i) => (o,i))
                    .Where(static x => x.o.Id>2&&x.o.Id+8<x.i.Id&&x.i.Id<13)
                    .Select(static x => x.o.Id*100+x.i.Id)
                    .ToList();
            public void Run(Item[] outer,Item[] inner) => _ = Filter(outer,inner);
        }
        """;

    // Join の inner だけへ押し下がると Join が単段になり、PipelineBuilder の単段許可リストに Join が
    // 無いため Fragment 辞書経路へ落ちる。その経路で引数位置の inner がフラグメント化されないと
    // 押し下げた Where が素の System.Linq.Where(boxing+デリゲート+interface dispatch)として
    // 生成コードへ残る。ここでその形を禁止する。
    private const string InnerOnlyConjunctsSingleStageJoin = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public sealed class Consumer<T> {
            public void Consume(T item) {
            }
        }
        public static class ConsumeSupport {
            public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                foreach(var item in source) {
                    consumer.Consume(item);
                }
            }
        }
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static void Filter(Item[] outer,Item[] inner,Consumer<(Item o,Item i)> consumer) =>
                outer.Join(inner,static o => o.Key,static i => i.Key,static (o,i) => (o,i))
                    .Where(static x => x.i.Id<13&&x.i.Id>2)
                    .Consume(consumer);
            public void Run(Item[] outer,Item[] inner,Consumer<(Item o,Item i)> consumer) => Filter(outer,inner,consumer);
        }
        """;

    [Fact]
    public void InnerOnlyConjunctsFragmentizeTheSecondSource() {
        var generated = GeneratedSource(InnerOnlyConjunctsSingleStageJoin.Replace("OPTIMIZATIONS","LinqOptimizations.All",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            !generated.Contains(".Where(",StringComparison.Ordinal)
            &&!generated.Contains("Enumerable.Where",StringComparison.Ordinal)
            &&!generated.Contains(".i.Id<13&&",StringComparison.Ordinal)
            &&generated.Contains(".Id<13&&",StringComparison.Ordinal),
            "Inner-only pushdown must fuse the predicate into a generated struct instead of leaving a raw Where call on the Join argument.",
            generated,
            output);
    }

    [Fact]
    public void DisabledPredicatePushdownKeepsInnerConjunctsAfterJoin() {
        var generated = GeneratedSource(InnerOnlyConjunctsSingleStageJoin.Replace(
            "OPTIMIZATIONS",
            "LinqOptimizations.All&~LinqOptimizations.PredicatePushdown",
            StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            generated.Contains(".i.Id<13&&",StringComparison.Ordinal)
            &&!generated.Contains(".Where(",StringComparison.Ordinal),
            "Disabling PredicatePushdown must keep the inner-only predicate on the Join result.",
            generated,
            output);
    }

    [Fact]
    public void BothSideConjunctsSplitToEachLeaf() {
        var generated = GeneratedSource(BothSideConjuncts.Replace("OPTIMIZATIONS","LinqOptimizations.All",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            !generated.Contains(".o.Id>2&&",StringComparison.Ordinal)
            &&generated.Contains(".Id>2)",StringComparison.Ordinal)
            &&generated.Contains(".Id<13)",StringComparison.Ordinal),
            "Both-side AND predicate should split into one Where on each Join leaf.",
            generated,
            output);
    }

    [Fact]
    public void DisabledPredicatePushdownKeepsWhereAfterJoin() {
        var generated = GeneratedSource(BothSideConjuncts.Replace(
            "OPTIMIZATIONS",
            "LinqOptimizations.All&~LinqOptimizations.PredicatePushdown",
            StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            generated.Contains(".o.Id>2&&",StringComparison.Ordinal),
            "Disabling PredicatePushdown must keep the whole predicate on the Join result.",
            generated,
            output);
    }

    [Fact]
    public void ResidualConjunctStaysAfterJoin() {
        var generated = GeneratedSource(ResidualConjunct);

        GeneratedSourceAssert.True(
            generated.Contains(".o.Id+8<",StringComparison.Ordinal)
            &&!generated.Contains(".o.Id>2&&",StringComparison.Ordinal)
            &&generated.Contains(".Id<13)",StringComparison.Ordinal),
            "Only the both-side conjunct should remain after the Join; the rest moves to the leaves.",
            generated,
            output);
    }

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
