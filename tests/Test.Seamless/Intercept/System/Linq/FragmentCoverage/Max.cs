using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Max {
    [Fact,Trait("Category","Unit")]
    public static void Max_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Max>(nameof(Max_noArgImpl));
        var expected=SystemEnumerable.Max(new[]{1,2,3,4});
        var actual=Max_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Max_noArgImpl(){
        return new[]{1,2,3,4}.Max();
    }

    [Fact,Trait("Category","Unit")]
    public static void Max_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Max>(nameof(Max_noArg_enumerableImpl));
        var expected=SystemEnumerable.Max(new[]{1,2,3,4});
        var actual=Max_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Max_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Max();
    }
}
