using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

using Seamless.FLinq;
namespace Test.SourceGenerator;

// Union/Except への Where 分配、および By系(ExceptBy等)が押し下げ対象外のままであることが
// 生成コードへ実際に現れることを確認する。意味保存テストは押し下げが起きなくても緑になるため、
// 形状はここで固定する。
//
// 判定方法: 二項の集合演算 struct(Union/Except/ExceptBy いずれも第1・第2ソースの2つの
// ジェネリック型引数を持つ、例 "Struct1<TEnum1, TEnum2>")の宣言位置と、述語リテラル(777)の
// 最初の出現位置を比較する。押し下げられていれば述語は二項struct宣言より前(=上流の
// 独立した Where 段の中)に現れる。押し下げられていなければ述語は二項struct宣言より後
// (=集合演算 struct 自身の本体、つまり演算結果に対する事後フィルタ)に現れる。
// 演算子ごとに生成される型引数名(TEnum1/TEnum2 or TEnum/TSecond0 等)は異なるため、
// 固定文字列ではなく正規表現で二項 struct 宣言を検出する。
[LinqIntercept(false)]
public sealed class SetOperationPushdownShapeTests(ITestOutputHelper output) {
    private const string UnionSample = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(int[] outer,int[] inner) =>
                outer.Union(inner).Where(static v => v>777).ToList();
            public void Run(int[] outer,int[] inner) => _ = Filter(outer,inner);
        }
        """;

    private const string ExceptSample = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<int> Filter(int[] outer,int[] inner) =>
                outer.Except(inner).Where(static v => v>777).ToList();
            public void Run(int[] outer,int[] inner) => _ = Filter(outer,inner);
        }
        """;

    private const string ExceptBySample = """
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed record Item(int Id,int Key);
        public class Container {
            [LinqIntercept(true,Optimizations=OPTIMIZATIONS)]
            private static List<Item> Filter(Item[] outer,int[] keys) =>
                outer.ExceptBy(keys,static x => x.Key).Where(static x => x.Id>777).ToList();
            public void Run(Item[] outer,int[] keys) => _ = Filter(outer,keys);
        }
        """;

    [Fact]
    public void UnionDistributesToBothSides() {
        var generated = GeneratedSource(UnionSample.Replace("OPTIMIZATIONS","LinqOptimizations.All",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            IsPredicateBeforeBinaryStage(generated),
            "Where after Union should be pushed before the binary Union stage (distributed to both sources).",
            generated,
            output);
    }

    [Fact]
    public void DisabledPredicatePushdownKeepsWhereAfterUnion() {
        var generated = GeneratedSource(UnionSample.Replace(
            "OPTIMIZATIONS",
            "LinqOptimizations.All&~LinqOptimizations.PredicatePushdown",
            StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            !IsPredicateBeforeBinaryStage(generated),
            "Disabling PredicatePushdown must keep Where fused after the Union stage, not pushed before it.",
            generated,
            output);
    }

    // Except は PipelineFusion が有効だと、押し下げの有無に関わらず述語チェックが Except struct
    // 自身の本体に現れる形へ再構成され、struct 宣言順による判定が効かなくなる(実際に検証して確認した
    // 既知の事情。Union は別経路で先に独立ステージ化されるため All のままでも判定できる)。
    // そのため Except の形状テストだけは PredicatePushdown の効果を単離して検証する。
    [Fact]
    public void ExceptPushesToFirstSourceOnly() {
        var generated = GeneratedSource(ExceptSample.Replace("OPTIMIZATIONS","LinqOptimizations.Rewrite|LinqOptimizations.PredicatePushdown",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            IsPredicateBeforeBinaryStage(generated),
            "Where after Except should be pushed before the binary Except stage (applied to the first source only).",
            generated,
            output);
    }

    [Fact]
    public void DisabledPredicatePushdownKeepsWhereAfterExcept() {
        var generated = GeneratedSource(ExceptSample.Replace("OPTIMIZATIONS","LinqOptimizations.Rewrite",StringComparison.Ordinal));

        GeneratedSourceAssert.True(
            !IsPredicateBeforeBinaryStage(generated),
            "Disabling PredicatePushdown must keep Where fused after the Except stage, not pushed before it.",
            generated,
            output);
    }

    [Fact]
    public void ExceptByKeepsWhereAfterOperation() {
        var generated = GeneratedSource(ExceptBySample.Replace("OPTIMIZATIONS","LinqOptimizations.Rewrite|LinqOptimizations.PredicatePushdown",StringComparison.Ordinal));

        // ExceptBy はキー重複時の代表選択が壊れるため押し下げ対象外(意図的な既存バグ修正)。
        // PredicatePushdown を有効にしても、述語は二項 struct(ExceptBy 自身)より後、
        // つまり演算結果に対する事後フィルタのままであることを固定する。
        GeneratedSourceAssert.True(
            !IsPredicateBeforeBinaryStage(generated),
            "ExceptBy must not receive a pushed-down Where; the predicate must remain a post-operation filter.",
            generated,
            output);
    }

    // 述語リテラル(777)の最初の出現が、二項集合演算 struct の宣言より前かどうかを判定する。
    private static bool IsPredicateBeforeBinaryStage(string generated) {
        var binaryStage = Regex.Match(generated,@"struct \S+<\w+,\s*\w+>\s*:");
        Assert.True(binaryStage.Success,"Expected to find a binary set-operation struct declaration in the generated source.");
        var predicateIndex = generated.IndexOf("777",StringComparison.Ordinal);
        Assert.True(predicateIndex>=0,"Expected the predicate literal 777 to appear in the generated source.");
        return predicateIndex<binaryStage.Index;
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
