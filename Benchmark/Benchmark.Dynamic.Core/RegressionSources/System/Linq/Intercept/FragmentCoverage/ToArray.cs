namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class ToArray {
    [Fact, Trait("Category","Unit")]
    public static void noArg() {
        GeneratedInterceptorAssert.ContainsMethod<ToArray>(nameof(noArgImpl));
        var expected = new[] { 1,2,3,4 }.ToArray();
        var actual = noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int[] noArgImpl() {
        return new[] { 1,2,3,4 }.ToArray();
    }

    [Fact, Trait("Category","Unit")]
    public static void noArg_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<ToArray>(nameof(noArg_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.ToArray();
        var actual = noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int[] noArg_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.ToArray();
    }
}
