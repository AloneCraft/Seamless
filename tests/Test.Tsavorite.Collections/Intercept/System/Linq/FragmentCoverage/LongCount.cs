using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Tsavorite.Intercept.System.Linq.FragmentCoverage;

public sealed class LongCount {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<LongCount>(nameof(noArgImpl));
        var expected=SystemEnumerable.LongCount([1,2,3,4]);
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static long noArgImpl(){
        return new[]{1,2,3,4}.LongCount();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<LongCount>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.LongCount([1,2,3,4]);
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static long noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.LongCount();
    }
}
