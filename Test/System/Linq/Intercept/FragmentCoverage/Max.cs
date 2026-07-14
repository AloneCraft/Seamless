namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Max {
    [Fact, Trait("Category","Unit")]
    public static void noArg() {
        GeneratedInterceptorAssert.ContainsMethod<Max>(nameof(noArgImpl));
        var expected = new[] { 1,2,3,4 }.Max();
        var actual = noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl() {
        return new[] { 1,2,3,4 }.Max();
    }

    [Fact, Trait("Category","Unit")]
    public static void noArg_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<Max>(nameof(noArg_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.Max();
        var actual = noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.Max();
    }
}
