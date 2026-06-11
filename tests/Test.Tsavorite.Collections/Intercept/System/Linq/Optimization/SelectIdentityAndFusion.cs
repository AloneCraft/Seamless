using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: Select(f).Select(g) 融合（ループ融合で達成済み）と 恒等 Select(x=>x) 除去。
// 恒等 Select は「後続に別の Enumerable 拡張がある」中段でのみ除去（最外段は素通しのため残す）。
public sealed class SelectIdentityAndFusion:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void SelectSelect() => SelectSelectImpl();

    private static void SelectSelectImpl(){
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Numbers(),static x => x+1),static x => x*x).ToList();
        var actual=new Consumer<int>();
        Numbers().Select(static x => x+1).Select(static x => x*x).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectSelectArithmetic() => SelectSelectArithmeticImpl();

    private static void SelectSelectArithmeticImpl(){
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Numbers(),static p => p+1),static q => q*q).ToList();
        var actual=new Consumer<int>();
        Numbers().Select(static p => p+1).Select(static q => q*q).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void IdentitySelectRemovedMidChain() => IdentitySelectRemovedMidChainImpl();

    private static void IdentitySelectRemovedMidChainImpl(){
        // src.Select(x=>x).Where(p) -> src.Where(p)（中段の恒等 Select 除去。後続に Where あり）。
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Numbers(),static x => x),static x => x>3).ToList();
        var actual=new Consumer<int>();
        Numbers().Select(static x => x).Where(static x => x>3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void IdentitySelectOutermostKept() => IdentitySelectOutermostKeptImpl();

    private static void IdentitySelectOutermostKeptImpl(){
        // src.Where(p).Select(x=>x) -> 最外段の恒等 Select は除去されないが結果は不変。
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Numbers(),static x => x>3),static x => x).ToList();
        var actual=new Consumer<int>();
        Numbers().Where(static x => x>3).Select(static x => x).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
