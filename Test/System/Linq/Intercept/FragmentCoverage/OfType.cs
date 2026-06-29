namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class OfType {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<OfType>(nameof(noArgImpl));
        IEnumerable src=new object?[]{1,"x",2,null};
        var expected=src.OfType<int>().ToList();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> noArgImpl(){
        IEnumerable src=new object?[]{1,"x",2,null};
        return src.OfType<int>().ToList();
    }
}
