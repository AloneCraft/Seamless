namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class MinBy {
    [Fact, Trait("Category","Unit")]
    public static void keySelector() {
        GeneratedInterceptorAssert.ContainsMethod<MinBy>(nameof(keySelectorImpl));
        var expected = new[] { 1,2,3,4 }.MinBy(static v => v);
        var actual = keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int keySelectorImpl() {
        return new[] { 1,2,3,4 }.MinBy(static v => v);
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<MinBy>(nameof(keySelector_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.MinBy(static v => v);
        var actual = keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int keySelector_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.MinBy(static v => v);
    }
}
