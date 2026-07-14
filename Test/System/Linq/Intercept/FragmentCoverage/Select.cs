namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Select {
    [Fact, Trait("Category","Unit")]
    public static void selector() {
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(selectorImpl));
        var expected = new[] { 1,2,3,4 }.Select(static v => v*2).ToList();
        var actual = selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> selectorImpl() {
        return new[] { 1,2,3,4 }.Select(static v => v*2).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void indexSelector() {
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(indexSelectorImpl));
        var expected = new[] { 1,2,3,4 }.Select(static (v,i) => v+i).ToList();
        var actual = indexSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexSelectorImpl() {
        return new[] { 1,2,3,4 }.Select(static (v,i) => v+i).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void selector_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(selector_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.Select(static v => v*2).ToList();
        var actual = selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> selector_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.Select(static v => v*2).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void indexSelector_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<Select>(nameof(indexSelector_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.Select(static (v,i) => v+i).ToList();
        var actual = indexSelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> indexSelector_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.Select(static (v,i) => v+i).ToList();
    }
}
