using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class LastOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void LastOrDefault_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<LastOrDefault>(nameof(LastOrDefault_noArgImpl));
        var expected=SystemEnumerable.LastOrDefault(new[]{1,2,3,4});
        var actual=LastOrDefault_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int LastOrDefault_noArgImpl(){
        return new[]{1,2,3,4}.LastOrDefault();
    }

    [Fact,Trait("Category","Unit")]
    public static void LastOrDefault_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<LastOrDefault>(nameof(LastOrDefault_noArg_enumerableImpl));
        var expected=SystemEnumerable.LastOrDefault(new[]{1,2,3,4});
        var actual=LastOrDefault_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int LastOrDefault_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.LastOrDefault();
    }
}
