namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class DefaultIfEmpty {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<DefaultIfEmpty>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.DefaultIfEmpty().ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        return new[]{1,2,3,4}.DefaultIfEmpty().ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<DefaultIfEmpty>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.DefaultIfEmpty().ToList();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.DefaultIfEmpty().ToList();
    }
}
