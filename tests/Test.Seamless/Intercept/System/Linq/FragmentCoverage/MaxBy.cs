using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class MaxBy {
    [Fact,Trait("Category","Unit")]
    public static void MaxBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<MaxBy>(nameof(MaxBy_keySelectorImpl));
        var expected=SystemEnumerable.MaxBy(new[]{1,2,3,4},static v => v);
        var actual=MaxBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int MaxBy_keySelectorImpl(){
        return new[]{1,2,3,4}.MaxBy(static v => v);
    }

    [Fact,Trait("Category","Unit")]
    public static void MaxBy_keySelector_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<MaxBy>(nameof(MaxBy_keySelector_enumerableImpl));
        var expected=SystemEnumerable.MaxBy(new[]{1,2,3,4},static v => v);
        var actual=MaxBy_keySelector_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int MaxBy_keySelector_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.MaxBy(static v => v);
    }
}
