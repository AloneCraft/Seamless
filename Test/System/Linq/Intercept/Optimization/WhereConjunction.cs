using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.Optimization;

// §1: Where(f).Where(g) -> Where(f&&g)（Pipeline.MergeWhereStages の単一短絡ステージ化）。意味保存を確認。
public sealed class WhereConjunction:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void TwoWhere() {
        var expected=Numbers().Where(static x => x>2).Where(static x => x%2==0).ToList();
        TwoWhereOptimized(expected);
        TwoWhereNoOptimized(expected);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void TwoWhereOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>2).Where(static x => x%2==0).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
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

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void ThreeWhereOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>1).Where(static x => x<9).Where(static x => x%2==1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static void ThreeWhereNoOptimized(dynamic expected) {
        var actual=new Consumer<int>();
                Numbers().Where(static x => x>1).Where(static x => x<9).Where(static x => x%2==1).Consume(actual);
                AssertSequenceEqual(expected,actual.Items);
    }
}
