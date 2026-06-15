using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Average {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(noArgImpl));
        var expected=new[]{1,2,3,4}.Average();
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static double noArgImpl(){
        return new[]{1,2,3,4}.Average();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(selectorImpl));
        var expected=new[]{1,2,3,4}.Average(static v => v*2);
        var actual=selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static double selectorImpl(){
        return new[]{1,2,3,4}.Average(static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(noArg_enumerableImpl));
        var expected=new[]{1,2,3,4}.Average();
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static double noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Average();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(selector_enumerableImpl));
        var expected=new[]{1,2,3,4}.Average(static v => v*2);
        var actual=selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static double selector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Average(static v => v*2);
    }
}
