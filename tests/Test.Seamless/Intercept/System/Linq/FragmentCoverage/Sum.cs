using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Sum {
    [Fact,Trait("Category","Unit")]
    public static void Sum_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(Sum_noArgImpl));
        var expected=SystemEnumerable.Sum(new[]{1,2,3,4});
        var actual=Sum_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Sum_noArgImpl(){
        return new[]{1,2,3,4}.Sum();
    }

    [Fact,Trait("Category","Unit")]
    public static void Sum_selector(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(Sum_selectorImpl));
        var expected=SystemEnumerable.Sum(new[]{1,2,3,4},static v => v*2);
        var actual=Sum_selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int Sum_selectorImpl(){
        return new[]{1,2,3,4}.Sum(static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void Sum_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(Sum_noArg_enumerableImpl));
        var expected=SystemEnumerable.Sum(new[]{1,2,3,4});
        var actual=Sum_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Sum_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Sum();
    }

    [Fact,Trait("Category","Unit")]
    public static void Sum_selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(Sum_selector_enumerableImpl));
        var expected=SystemEnumerable.Sum(new[]{1,2,3,4},static v => v*2);
        var actual=Sum_selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int Sum_selector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Sum(static v => v*2);
    }
}
