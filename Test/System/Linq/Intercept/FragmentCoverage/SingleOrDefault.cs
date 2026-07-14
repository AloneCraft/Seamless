namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class SingleOrDefault {
    [Fact, Trait("Category","Unit")]
    public static void noArg() {
        GeneratedInterceptorAssert.ContainsMethod<SingleOrDefault>(nameof(noArgImpl));
        var expected = new[] { 42 }.SingleOrDefault();
        var actual = noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl() {
        return new[] { 42 }.SingleOrDefault();
    }

    [Fact, Trait("Category","Unit")]
    public static void noArg_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<SingleOrDefault>(nameof(noArg_enumerableImpl));
        var expected = new[] { 42 }.SingleOrDefault();
        var actual = noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl() {
        IEnumerable<int> src = [42];
        return src.SingleOrDefault();
    }
}
