namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class Take {
    [Fact,Trait("Category","Unit")]
    public static void count(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(countImpl));
        var expected=new[]{1,2,3,4}.Take(2).ToList();
        var actual=countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> countImpl(){
        return new[]{1,2,3,4}.Take(2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(count_enumerableImpl));
        var expected=new[]{1,2,3,4}.Take(2).ToList();
        var actual=count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> count_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Take(2).ToList();
    }
}
