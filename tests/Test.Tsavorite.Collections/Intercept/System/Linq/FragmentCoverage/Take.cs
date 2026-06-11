using System.Collections.Generic;
using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Take {
    [Fact,Trait("Category","Unit")]
    public static void count(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(countImpl));
        var expected=SystemEnumerable.Take([1,2,3,4],2).ToList();
        var actual=countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> countImpl(){
        return new[]{1,2,3,4}.Take(2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(count_enumerableImpl));
        var expected=SystemEnumerable.Take([1,2,3,4],2).ToList();
        var actual=count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> count_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Take(2).ToList();
    }
}
