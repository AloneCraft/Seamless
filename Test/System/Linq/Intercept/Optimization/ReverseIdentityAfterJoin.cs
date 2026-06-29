namespace Test.System.Linq.Intercept.Optimization;

public sealed class ReverseIdentityAfterJoin:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void LeftJoinReverseReverseToArray() {
        var expected=Numbers().Distinct().Take(4).LeftJoin(SecondNumbers(),static x => x%3,static y => y%3,static (x,y) => x+y).Reverse().Reverse().ToArray();
        AssertSequenceEqual(expected,LeftJoinReverseReverseToArrayOptimized());
        AssertSequenceEqual(expected,LeftJoinReverseReverseToArrayNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
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

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
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
