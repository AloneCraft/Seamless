namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class ExceptBy {
    [Fact, Trait("Category","Unit")]
    public static void keySelector() {
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(keySelectorImpl));
        var expected = new[] { 1,2,3,4 }.ExceptBy([3,4,5],static v => v).ToList();
        var actual = keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelectorImpl() {
        return new[] { 1,2,3,4 }.ExceptBy([3,4,5],static v => v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer() {
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(comparerImpl));
        var expected = new[] { 1,2,3,4 }.ExceptBy([3,4,5],static v => v,EqualityComparer<int>.Default).ToList();
        var actual = comparerImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparerImpl() {
        return new[] { 1,2,3,4 }.ExceptBy([3,4,5],static v => v,EqualityComparer<int>.Default).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void keySelector_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(keySelector_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.ExceptBy([3,4,5],static v => v).ToList();
        var actual = keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> keySelector_enumerableImpl() {
        IEnumerable<int> first = [1,2,3,4];
        IEnumerable<int> second = [3,4,5];
        return first.ExceptBy(second,static v => v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void comparer_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<ExceptBy>(nameof(comparer_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.ExceptBy([3,4,5],static v => v,EqualityComparer<int>.Default).ToList();
        var actual = comparer_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> comparer_enumerableImpl() {
        IEnumerable<int> first = [1,2,3,4];
        IEnumerable<int> second = [3,4,5];
        return first.ExceptBy(second,static v => v,EqualityComparer<int>.Default).ToList();
    }
}
