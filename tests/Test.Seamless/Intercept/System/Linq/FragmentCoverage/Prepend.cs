using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Prepend {
    [Fact,Trait("Category","Unit")]
    public static void Prepend_element(){
        GeneratedInterceptorAssert.ContainsMethod<Prepend>(nameof(Prepend_elementImpl));
        var expected=SystemEnumerable.Prepend(new[]{1,2,3,4},0).ToList();
        var actual=Prepend_elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Prepend_elementImpl(){
        return new[]{1,2,3,4}.Prepend(0).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Prepend_element_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Prepend>(nameof(Prepend_element_enumerableImpl));
        var expected=SystemEnumerable.Prepend(new[]{1,2,3,4},0).ToList();
        var actual=Prepend_element_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Prepend_element_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Prepend(0).ToList();
    }
}
