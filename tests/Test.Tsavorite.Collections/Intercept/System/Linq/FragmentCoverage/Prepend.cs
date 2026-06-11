using System.Collections.Generic;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Prepend {
    [Fact,Trait("Category","Unit")]
    public static void element(){
        GeneratedInterceptorAssert.ContainsMethod<Prepend>(nameof(elementImpl));
        var expected=SystemEnumerable.Prepend([1,2,3,4],0).ToList();
        var actual=elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> elementImpl(){
        return new[]{1,2,3,4}.Prepend(0).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void element_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Prepend>(nameof(element_enumerableImpl));
        var expected=SystemEnumerable.Prepend([1,2,3,4],0).ToList();
        var actual=element_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> element_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Prepend(0).ToList();
    }
}
