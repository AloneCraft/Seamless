namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Join {
    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector() {
        GeneratedInterceptorAssert.ContainsMethod<Join>(nameof(keySelectors_resultSelectorImpl));
        var expected = new[] { 1,2,3 }.Join([2,3,4],static o => o,static i => i,static (o,i) => o+i).ToList();
        var actual = keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectors_resultSelectorImpl() {
        return new[] { 1,2,3 }.Join([2,3,4],static o => o,static i => i,static (o,i) => o+i).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelectors_resultSelector_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<Join>(nameof(keySelectors_resultSelector_enumerableImpl));
        var expected = new[] { 1,2,3 }.Join([2,3,4],static o => o,static i => i,static (o,i) => o+i).ToList();
        var actual = keySelectors_resultSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectors_resultSelector_enumerableImpl() {
        IEnumerable<int> outer = [1,2,3];
        IEnumerable<int> inner = [2,3,4];
        return outer.Join(inner,static o => o,static i => i,static (o,i) => o+i).ToList();
    }
}
