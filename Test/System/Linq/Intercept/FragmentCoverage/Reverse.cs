namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Reverse {
    [Fact, Trait("Category","Unit")]
    public static void noArg() {
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(noArgImpl));
        var expected = new[] { 1,2,3,4 }.Reverse().ToList();
        var actual = noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl() {
        return new[] { 1,2,3,4 }.Reverse().ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void noArg_enumerable() {
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(noArg_enumerableImpl));
        var expected = new[] { 1,2,3,4 }.Reverse().ToList();
        var actual = noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl() {
        IEnumerable<int> src = [1,2,3,4];
        return src.Reverse().ToList();
    }
}
