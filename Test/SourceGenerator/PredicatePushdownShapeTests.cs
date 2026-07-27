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
