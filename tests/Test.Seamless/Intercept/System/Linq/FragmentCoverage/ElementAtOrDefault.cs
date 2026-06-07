using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class ElementAtOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void index(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAtOrDefault>(nameof(indexImpl));
        var expected=SystemEnumerable.ElementAtOrDefault(new[]{1,2,3,4},1);
        var actual=indexImpl();
        Assert.Equal(expected,actual);
    }
    private static int indexImpl(){
        return new[]{1,2,3,4}.ElementAtOrDefault(1);
    }

    [Fact,Trait("Category","Unit")]
    public static void index_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAtOrDefault>(nameof(index_enumerableImpl));
        var expected=SystemEnumerable.ElementAtOrDefault(new[]{1,2,3,4},1);
        var actual=index_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int index_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.ElementAtOrDefault(1);
    }
}
