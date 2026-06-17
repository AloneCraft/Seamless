using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class InfiniteSequence {
    [Fact,Trait("Category","Unit")]
    public static void start_step() {
        GeneratedInterceptorAssert.ContainsMethod<InfiniteSequence>(nameof(start_stepImpl));
        var expected=Enumerable.InfiniteSequence(1L,3L).Take(4).ToList();
        var actual=start_stepImpl();
        Assert.Equal(expected,actual);
    }

    private static List<long> start_stepImpl() {
        return Enumerable.InfiniteSequence(1L,3L).Take(4).ToList();
    }
}
