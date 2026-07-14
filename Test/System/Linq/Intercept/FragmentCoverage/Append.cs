namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Append {
    [Fact, Trait("Category","Unit")]
    public static void element() {
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(elementImpl));
        var expected = new[] { 1,2,3,4 }.Append(99).ToList();
        var actual = elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> elementImpl() {
        return new[] { 1,2,3,4 }.Append(99).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void element_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(element_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.Append(99).ToList();
        var actual = element_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> element_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.Append(99).ToList();
    }
}
