using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class MaxBy {
    [Fact,Trait("Category","Unit")]
    public static void keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<MaxBy>(nameof(keySelectorImpl));
        var expected=SystemEnumerable.MaxBy([1,2,3,4],static v => v);
        var actual=keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int keySelectorImpl(){
        return new[]{1,2,3,4}.MaxBy(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<MaxBy>(nameof(keySelector_enumerableImpl));
        var expected=SystemEnumerable.MaxBy([1,2,3,4],static v => v);
        var actual=keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int keySelector_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.MaxBy(static v => v);
    }
}
