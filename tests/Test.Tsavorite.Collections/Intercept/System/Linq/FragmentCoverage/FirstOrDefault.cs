using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class FirstOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<FirstOrDefault>(nameof(noArgImpl));
        var expected=SystemEnumerable.FirstOrDefault([1,2,3,4]);
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{1,2,3,4}.FirstOrDefault();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<FirstOrDefault>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.FirstOrDefault([1,2,3,4]);
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.FirstOrDefault();
    }
}
