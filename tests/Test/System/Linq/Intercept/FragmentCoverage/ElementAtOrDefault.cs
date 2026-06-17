using System.Collections.Generic;
using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class ElementAtOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void index(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAtOrDefault>(nameof(indexImpl));
        var expected=new[]{1,2,3,4}.ElementAtOrDefault(1);
        var actual=indexImpl();
        Assert.Equal(expected,actual);
    }
    private static int indexImpl(){
        return new[]{1,2,3,4}.ElementAtOrDefault(1);
    }

    [Fact,Trait("Category","Unit")]
    public static void index_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAtOrDefault>(nameof(index_enumerableImpl));
        var expected=new[]{1,2,3,4}.ElementAtOrDefault(1);
        var actual=index_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static int index_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.ElementAtOrDefault(1);
    }
}
