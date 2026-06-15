using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

public sealed class ReverseIdentityAfterJoin:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void LeftJoinReverseReverseToArray() {
        var expected=Numbers().Distinct().Take(4).LeftJoin(SecondNumbers(),static x => x%3,static y => y%3,static (x,y) => x+y).Reverse().Reverse().ToArray();
        AssertSequenceEqual(expected,LeftJoinReverseReverseToArrayOptimized());
        AssertSequenceEqual(expected,LeftJoinReverseReverseToArrayNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int[] LeftJoinReverseReverseToArrayOptimized() =>
        Numbers()
            .Distinct()
            .Take(4)
            .LeftJoin(
                SecondNumbers(),
                static x => x%3,
                static y => y%3,
                static (x,y) => x+y)
            .Reverse()
            .Reverse()
            .ToArray();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static int[] LeftJoinReverseReverseToArrayNoOptimized() =>
        Numbers()
            .Distinct()
            .Take(4)
            .LeftJoin(
                SecondNumbers(),
                static x => x%3,
                static y => y%3,
                static (x,y) => x+y)
            .Reverse()
            .Reverse()
            .ToArray();

    private static int[] SecondNumbers() => [0,2,5,8];
}
