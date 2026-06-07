using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class MinBy {
    [Fact,Trait("Category","Unit")]
    public static void MinBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<MinBy>(nameof(MinBy_keySelectorImpl));
        var expected=SystemEnumerable.MinBy(new[]{1,2,3,4},static v => v);
        var actual=MinBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int MinBy_keySelectorImpl(){
        return new[]{1,2,3,4}.MinBy(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void MinBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<MinBy>(nameof(MinBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.MinBy(new[]{1,2,3,4},static v => v);
        var actual=MinBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int MinBy_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.MinBy(static v => v);
    }
}
