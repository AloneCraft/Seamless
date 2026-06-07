using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Last {
    [Fact,Trait("Category","Unit")]
    public static void Last_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Last>(nameof(Last_noArgImpl));
        var expected=SystemEnumerable.Last(new[]{1,2,3,4});
        var actual=Last_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Last_noArgImpl(){
        return new[]{1,2,3,4}.Last();
    }

    [Fact,Trait("Category","Unit")]
    public static void Last_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Last>(nameof(Last_noArg_enumerableImpl));
        var expected=SystemEnumerable.Last(new[]{1,2,3,4});
        var actual=Last_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Last_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Last();
    }
}
