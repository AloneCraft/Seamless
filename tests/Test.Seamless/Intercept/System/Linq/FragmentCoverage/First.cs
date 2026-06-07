using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class First {
    [Fact,Trait("Category","Unit")]
    public static void First_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<First>(nameof(First_noArgImpl));
        var expected=SystemEnumerable.First(new[]{1,2,3,4});
        var actual=First_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int First_noArgImpl(){
        return new[]{1,2,3,4}.First();
    }

    [Fact,Trait("Category","Unit")]
    public static void First_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<First>(nameof(First_noArg_enumerableImpl));
        var expected=SystemEnumerable.First(new[]{1,2,3,4});
        var actual=First_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int First_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.First();
    }
}
