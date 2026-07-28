using Microsoft.CodeAnalysis;

using Seamless.FLinq;
namespace Test.SourceGenerator;

// 射影融合(Select(f).Select(g)、Join の result selector と後続 Select)が生成コードへ実際に現れることを確認する。
// 意味保存テストは融合が起きなくても緑になるため、融合が起きた証拠はここでしか取れない。
//
// carrier に名前付きの record struct(Sample.Pair)を使い、「生成コードに Pair が出てこない」ことを
// 融合成功の判定に使う。融合されなければ Select_selector フラグメントの ElementType として必ず現れる。
[LinqIntercept(false)]
public sealed class ProjectionMergeShapeTests(ITestOutputHelper output) {
    private const string SelectSelect = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public readonly record struct Pair(int Left,int Right);
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Project(Item[] source) =>
                source.Select(static o => new Pair(o.Id,o.Key))
                    .Select(static p => p.Left+p.Right)
                    .ToList();
            public void Run(Item[] source) => _ = Project(source);
        }
        """;

    private const string JoinSelect = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public readonly record struct Pair(int Left,int Right);
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Project(Item[] outer,Item[] inner) =>
                outer.Join(inner,static o => o.Key,static i => i.Key,static (o,i) => new Pair(o.Id,i.Id))
                    .Select(static p => p.Left*100+p.Right)
                    .ToList();
            public void Run(Item[] outer,Item[] inner) => _ = Project(outer,inner);
        }
        """;

    // Right はメソッド呼び出し(Tier: Arbitrary)で、後続 Select から一度も参照されない。
    // 引き戻すと Compute の評価が消えてしまうので融合してはいけない。
    private const string DroppedSideEffect = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public readonly record struct Pair(int Left,int Right);
        public class Container {
            private static int Compute(Item item) => item.Key*3;
            [LinqIntercept(true,Optimizations=LinqOptimizations.All)]
            private static List<int> Project(Item[] source) =>
                source.Select(static o => new Pair(o.Id,Compute(o)))
                    .Select(static p => p.Left)
                    .ToList();
            public void Run(Item[] source) => _ = Project(source);
        }
        """;

    [Fact]
    public void SelectSelectFusesIntoSingleSelector() {
        var generated = GeneratedSource(SelectSelect.Replace("OPTIMIZATIONS","LinqOptimizations.All",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            !generated.Contains("Sample.Pair",StringComparison.Ordinal),
            "Select(carrier).Select(member) should fuse and drop the record struct carrier entirely.",
            generated,
            output);
    }

    [Fact]
    public void DisabledProjectionMergeKeepsCarrier() {
        var generated = GeneratedSource(SelectSelect.Replace(
            "OPTIMIZATIONS",
            "LinqOptimizations.All&~LinqOptimizations.ProjectionMerge",
            StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            generated.Contains("Sample.Pair",StringComparison.Ordinal),
            "Disabling ProjectionMerge must keep the intermediate carrier construction.",
            generated,
            output);
    }

    [Fact]
    public void JoinResultSelectorAbsorbsSelect() {
        var generated = GeneratedSource(JoinSelect.Replace("OPTIMIZATIONS","LinqOptimizations.All",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            !generated.Contains("Sample.Pair",StringComparison.Ordinal),
            "Join(...,resultSelector).Select(g) should absorb g into the result selector and drop the carrier.",
            generated,
            output);
    }

    [Fact]
    public void DisabledProjectionMergeKeepsJoinCarrier() {
        var generated = GeneratedSource(JoinSelect.Replace(
            "OPTIMIZATIONS",
            "LinqOptimizations.All&~LinqOptimizations.ProjectionMerge",
            StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            generated.Contains("Sample.Pair",StringComparison.Ordinal),
            "Disabling ProjectionMerge must keep the Join carrier.",
            generated,
            output);
    }

    [Fact]
    public void UnreferencedArbitraryMemberBlocksFusion() {
        var generated = GeneratedSource(DroppedSideEffect);

        GeneratedSourceAssert.True(
            generated.Contains("Sample.Pair",StringComparison.Ordinal),
            "A carrier member that is a method call and never read must block fusion so its evaluation is not dropped.",
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
