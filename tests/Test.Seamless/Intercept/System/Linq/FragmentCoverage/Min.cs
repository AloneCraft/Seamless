using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Min {
    [Fact,Trait("Category","Unit")]
    public static void Min_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Min>(nameof(Min_noArgImpl));
        var expected=SystemEnumerable.Min(new[]{1,2,3,4});
        var actual=Min_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Min_noArgImpl(){
        return new[]{1,2,3,4}.Min();
    }

    [Fact,Trait("Category","Unit")]
    public static void Min_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Min>(nameof(Min_noArg_enumerableImpl));
        var expected=SystemEnumerable.Min(new[]{1,2,3,4});
        var actual=Min_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Min_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Min();
    }
}
