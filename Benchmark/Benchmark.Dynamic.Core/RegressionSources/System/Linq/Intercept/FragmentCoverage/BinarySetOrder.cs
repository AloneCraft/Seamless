namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class BinarySetOrder {
    [Fact, Trait("Category","Unit")]
    public static void Union_OrderBy_ThenBy() {
        GeneratedInterceptorAssert.ContainsMethod<BinarySetOrder>(nameof(Union_OrderBy_ThenByImpl));
        var expected = new[] { 3,1,2,3,4,2 }.Union([2,5,1,6]).OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
        var actual = Union_OrderBy_ThenByImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_OrderBy_ThenByImpl() {
        return new[] { 3,1,2,3,4,2 }.Union([2,5,1,6]).OrderBy(static v => v%2).ThenByDescending(static v => v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void Except_OrderBy_ThenBy() {
        GeneratedInterceptorAssert.ContainsMethod<BinarySetOrder>(nameof(Except_OrderBy_ThenByImpl));
        var expected = new[] { 7,3,1,2,3,4,2,8 }.Except([2,8]).OrderByDescending(static v => v%3).ThenBy(static v => v).ToList();
        var actual = Except_OrderBy_ThenByImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Except_OrderBy_ThenByImpl() {
        return new[] { 7,3,1,2,3,4,2,8 }.Except([2,8]).OrderByDescending(static v => v%3).ThenBy(static v => v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void IntersectBy_Select_OrderBy_ThenBy() {
        GeneratedInterceptorAssert.ContainsMethod<BinarySetOrder>(nameof(IntersectBy_Select_OrderBy_ThenByImpl));
        var expected = new[] { 7,3,1,2,3,4,2,8 }.IntersectBy([1,2],static v => v%3).Select(static v => v*10).OrderBy(static v => v%4).ThenBy(static v => v).ToList();
        var actual = IntersectBy_Select_OrderBy_ThenByImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> IntersectBy_Select_OrderBy_ThenByImpl() {
        return new[] { 7,3,1,2,3,4,2,8 }.IntersectBy([1,2],static v => v%3).Select(static v => v*10).OrderBy(static v => v%4).ThenBy(static v => v).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void Union_Take_OrderBy() {
        GeneratedInterceptorAssert.ContainsMethod<BinarySetOrder>(nameof(Union_Take_OrderByImpl));
        var expected = new[] { 4,3,2,1 }.Union([5,6,7]).Take(3).OrderBy(static v => v).ToList();
        var actual = Union_Take_OrderByImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Union_Take_OrderByImpl() {
        return new[] { 4,3,2,1 }.Union([5,6,7]).Take(3).OrderBy(static v => v).ToList();
    }
}
