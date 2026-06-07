using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class ElementAt {
    [Fact,Trait("Category","Unit")]
    public static void ElementAt_index(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAt>(nameof(ElementAt_indexImpl));
        var expected=SystemEnumerable.ElementAt(new[]{1,2,3,4},1);
        var actual=ElementAt_indexImpl();
        Assert.Equal(expected,actual);
    }
    private static int ElementAt_indexImpl(){
        return new[]{1,2,3,4}.ElementAt(1);
    }

    [Fact,Trait("Category","Unit")]
    public static void ElementAt_index_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAt>(nameof(ElementAt_index_enumerableImpl));
        var expected=SystemEnumerable.ElementAt(new[]{1,2,3,4},1);
        var actual=ElementAt_index_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int ElementAt_index_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.ElementAt(1);
    }
}
