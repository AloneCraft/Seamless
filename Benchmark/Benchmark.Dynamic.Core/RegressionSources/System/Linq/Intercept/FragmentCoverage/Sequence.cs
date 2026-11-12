using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Sequence {
    [Fact, Trait("Category","Unit")]
    public static void start_end_step() {
        GeneratedInterceptorAssert.ContainsMethod<Sequence>(nameof(start_end_stepImpl));
        var expected = SystemEnumerable.Sequence(2L,10L,2L).ToList();
        var actual = start_end_stepImpl();
        Assert.Equal(expected,actual);
    }

    private static List<long> start_end_stepImpl() {
        return Enumerable.Sequence(2L,10L,2L).ToList();
    }
}
