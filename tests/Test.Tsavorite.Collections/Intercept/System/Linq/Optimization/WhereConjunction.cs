using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: Where(f).Where(g) -> Where(f&&g)（Pipeline.MergeWhereStages の単一短絡ステージ化）。意味保存を確認。
public sealed class WhereConjunction:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void TwoWhere() {
        var expected=Numbers().Where(static x => x>2).Where(static x => x%2==0).ToList();
        TwoWhereOptimized(expected);
        TwoWhereNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void TwoWhereOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>2).Where(static x => x%2==0).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void TwoWhereNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>2).Where(static x => x%2==0).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void ThreeWhere() {
        var expected=Numbers().Where(static x => x>1).Where(static x => x<9).Where(static x => x%2==1).ToList();
        ThreeWhereOptimized(expected);
        ThreeWhereNoOptimized(expected);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void ThreeWhereOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>1).Where(static x => x<9).Where(static x => x%2==1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static void ThreeWhereNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>1).Where(static x => x<9).Where(static x => x%2==1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
