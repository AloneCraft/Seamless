using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Average {
    [Fact,Trait("Category","Unit")]
    public static void Average_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(Average_noArgImpl));
        var expected=SystemEnumerable.Average(new[]{1,2,3,4});
        var actual=Average_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static double Average_noArgImpl(){
        return new[]{1,2,3,4}.Average();
    }

    [Fact,Trait("Category","Unit")]
    public static void Average_selector(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(Average_selectorImpl));
        var expected=SystemEnumerable.Average(new[]{1,2,3,4},static v => v*2);
        var actual=Average_selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static double Average_selectorImpl(){
        return new[]{1,2,3,4}.Average(static v => v*2);
    }

    [Fact,Trait("Category","Unit")]
    public static void Average_noArg_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(Average_noArg_enumerableImpl));
        var expected=SystemEnumerable.Average(new[]{1,2,3,4});
        var actual=Average_noArg_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static double Average_noArg_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Average();
    }

    [Fact,Trait("Category","Unit")]
    public static void Average_selector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(Average_selector_enumerableImpl));
        var expected=SystemEnumerable.Average(new[]{1,2,3,4},static v => v*2);
        var actual=Average_selector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static double Average_selector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Average(static v => v*2);
    }
}
