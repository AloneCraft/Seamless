using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: Where(f).Where(g) -> Where(f&&g)（Pipeline.MergeWhereStages の単一短絡ステージ化）。意味保存を確認。
public sealed class WhereConjunction:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void TwoWhere() => TwoWhereImpl();

    private static void TwoWhereImpl(){
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Numbers(),static x => x>2),static x => x%2==0).ToList();
        var actual=new Consumer<int>();
        Numbers().Where(static x => x>2).Where(static x => x%2==0).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void ThreeWhere() => ThreeWhereImpl();

    private static void ThreeWhereImpl(){
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(SystemEnumerable.Where(Numbers(),static x => x>1),static x => x<9),static x => x%2==1).ToList();
        var actual=new Consumer<int>();
        Numbers().Where(static x => x>1).Where(static x => x<9).Where(static x => x%2==1).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
