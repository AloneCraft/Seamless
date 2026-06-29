namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class SkipWhile {
    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<SkipWhile>(nameof(predicateImpl));
        var expected=new[]{1,2,3,4}.SkipWhile(static v => v<3).ToList();
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> predicateImpl(){
        return new[]{1,2,3,4}.SkipWhile(static v => v<3).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SkipWhile>(nameof(predicate_enumerableImpl));
        var expected=new[]{1,2,3,4}.SkipWhile(static v => v<3).ToList();
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> predicate_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.SkipWhile(static v => v<3).ToList();
    }
}
