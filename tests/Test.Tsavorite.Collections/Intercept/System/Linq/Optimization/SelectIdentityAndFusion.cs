using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: Select(f).Select(g) 融合（ループ融合で達成済み）と 恒等 Select(x=>x) 除去。
// 恒等 Select は「後続に別の Enumerable 拡張がある」中段でのみ除去（最外段は素通しのため残す）。
public sealed class SelectIdentityAndFusion:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void SelectSelect() {
        var expected=Numbers().Select(static x => x+1).Select(static x => x*x).ToList();
        SelectSelectOptimized(expected);
        SelectSelectNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SelectSelectOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Select(static x => x+1).Select(static x => x*x).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SelectSelectNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Select(static x => x+1).Select(static x => x*x).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectSelectArithmetic() {
        var expected=Numbers().Select(static p => p+1).Select(static q => q*q).ToList();
        SelectSelectArithmeticOptimized(expected);
        SelectSelectArithmeticNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SelectSelectArithmeticOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Select(static p => p+1).Select(static q => q*q).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void SelectSelectArithmeticNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Select(static p => p+1).Select(static q => q*q).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void IdentitySelectRemovedMidChain() {
                // src.Select(x=>x).Where(p) -> src.Where(p)（中段の恒等 Select 除去。後続に Where あり）。
        var expected=Numbers().Select(static x => x).Where(static x => x>3).ToList();
        IdentitySelectRemovedMidChainOptimized(expected);
        IdentitySelectRemovedMidChainNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void IdentitySelectRemovedMidChainOptimized(dynamic expected) {
                // src.Select(x=>x).Where(p) -> src.Where(p)（中段の恒等 Select 除去。後続に Where あり）。
        var actual=new Consumer<int>();
                Numbers().Select(static x => x).Where(static x => x>3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void IdentitySelectRemovedMidChainNoOptimized(dynamic expected) {
                // src.Select(x=>x).Where(p) -> src.Where(p)（中段の恒等 Select 除去。後続に Where あり）。
        var actual=new Consumer<int>();
                Numbers().Select(static x => x).Where(static x => x>3).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void IdentitySelectOutermostKept() {
                // src.Where(p).Select(x=>x) -> 最外段の恒等 Select は除去されないが結果は不変。
        var expected=Numbers().Where(static x => x>3).Select(static x => x).ToList();
        IdentitySelectOutermostKeptOptimized(expected);
        IdentitySelectOutermostKeptNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void IdentitySelectOutermostKeptOptimized(dynamic expected) {
                // src.Where(p).Select(x=>x) -> 最外段の恒等 Select は除去されないが結果は不変。
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>3).Select(static x => x).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void IdentitySelectOutermostKeptNoOptimized(dynamic expected) {
                // src.Where(p).Select(x=>x) -> 最外段の恒等 Select は除去されないが結果は不変。
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>3).Select(static x => x).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
