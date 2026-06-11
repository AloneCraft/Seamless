using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: 冗長な AsEnumerable() の排除。後続に別の Enumerable 拡張がある中段でのみ除去（最外段は素通しのため残す）。
// AsEnumerable は元々ゼロコストの素通しなので、いずれの場合も結果は不変。
public sealed class RedundantAsEnumerable:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void LeadingAsEnumerableRemoved() => LeadingAsEnumerableRemovedImpl();

    private static void LeadingAsEnumerableRemovedImpl(){
        // src.AsEnumerable().Where(p) -> src.Where(p)。
        var expected=SystemEnumerable.Where(SystemEnumerable.AsEnumerable(Numbers()),static x => x>3).ToList();
        var actual=new Consumer<int>();
        Numbers().AsEnumerable().Where(static x => x>3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void MiddleAsEnumerableRemoved() => MiddleAsEnumerableRemovedImpl();

    private static void MiddleAsEnumerableRemovedImpl(){
        // src.Where(p).AsEnumerable().Select(s) -> src.Where(p).Select(s)。
        var expected=SystemEnumerable.Select(SystemEnumerable.AsEnumerable(SystemEnumerable.Where(Numbers(),static x => x>3)),static x => x*2).ToList();
        var actual=new Consumer<int>();
        Numbers().Where(static x => x>3).AsEnumerable().Select(static x => x*2).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void OutermostAsEnumerableKept() => OutermostAsEnumerableKeptImpl();

    private static void OutermostAsEnumerableKeptImpl(){
        // src.Where(p).AsEnumerable() -> 最外段の AsEnumerable は残るが結果は不変。
        var expected=SystemEnumerable.AsEnumerable(SystemEnumerable.Where(Numbers(),static x => x>3)).ToList();
        var actual=new Consumer<int>();
        Numbers().Where(static x => x>3).AsEnumerable().Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
