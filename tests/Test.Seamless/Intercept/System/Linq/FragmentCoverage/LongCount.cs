using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class LongCount {
    [Fact,Trait("Category","Unit")]
    public static void LongCount_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<LongCount>(nameof(LongCount_noArgImpl));
        var expected=SystemEnumerable.LongCount(new[]{1,2,3,4});
        var actual=LongCount_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static long LongCount_noArgImpl(){
        return new[]{1,2,3,4}.LongCount();
    }

    [Fact,Trait("Category","Unit")]
    public static void LongCount_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<LongCount>(nameof(LongCount_noArg_enumerableImpl));
        var expected=SystemEnumerable.LongCount(new[]{1,2,3,4});
        var actual=LongCount_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static long LongCount_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.LongCount();
    }
}
