using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class All {
    [Fact,Trait("Category","Unit")]
    public static void predicate(){
        GeneratedInterceptorAssert.ContainsMethod<All>(nameof(predicateImpl));
        var expected=SystemEnumerable.All([1,2,3,4],static v => v>0);
        var actual=predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static bool predicateImpl(){
        return new[]{1,2,3,4}.All(static v => v>0);
    }

    [Fact,Trait("Category","Unit")]
    public static void predicate_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<All>(nameof(predicate_enumerableImpl));
        var expected=SystemEnumerable.All([1,2,3,4],static v => v>0);
        var actual=predicate_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static bool predicate_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.All(static v => v>0);
    }
}
