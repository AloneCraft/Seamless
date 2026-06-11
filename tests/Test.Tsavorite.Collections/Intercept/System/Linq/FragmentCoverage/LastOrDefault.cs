using System.Collections.Generic;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class LastOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<LastOrDefault>(nameof(noArgImpl));
        var expected=SystemEnumerable.LastOrDefault([1,2,3,4]);
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{1,2,3,4}.LastOrDefault();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<LastOrDefault>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.LastOrDefault([1,2,3,4]);
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.LastOrDefault();
    }
}
