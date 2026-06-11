using System.Collections.Generic;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Sum {
    [Fact,Trait("Category","Unit")]
    public static void noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(noArgImpl));
        var expected=SystemEnumerable.Sum([1,2,3,4]);
        var actual=noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArgImpl(){
        return new[]{1,2,3,4}.Sum();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(selectorImpl));
        var expected=SystemEnumerable.Sum([1,2,3,4],static v => v*2);
        var actual=selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int selectorImpl(){
        return new[]{1,2,3,4}.Sum(static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(noArg_enumerableImpl));
        var expected=SystemEnumerable.Sum([1,2,3,4]);
        var actual=noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int noArg_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Sum();
    }

    [Fact,Trait("Category","Unit")]
    public static void selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(selector_enumerableImpl));
        var expected=SystemEnumerable.Sum([1,2,3,4],static v => v*2);
        var actual=selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int selector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Sum(static v => v*2);
    }
}
