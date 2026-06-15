using System.Collections.Generic;
using System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Single {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Single>(nameof(noArgImpl));
        var expected=new[]{42}.Single();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{42}.Single();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Single>(nameof(noArg_enumerableImpl));
        var expected=new[]{42}.Single();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=[42];
        return src.Single();
    }
}
