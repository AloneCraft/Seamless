using System.Collections.Generic;
using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class First {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<First>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.First();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{1,2,3,4}.First();
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<First>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.First();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.First();
    }
}
