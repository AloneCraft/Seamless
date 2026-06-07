using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Append {
    [Fact,Trait("Category","Unit")]
    public static void element(){
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(elementImpl));
        var expected=SystemEnumerable.Append(new[]{1,2,3,4},99).ToList();
        var actual=elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> elementImpl(){
        return new[]{1,2,3,4}.Append(99).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void element_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(element_enumerableImpl));
        var expected=SystemEnumerable.Append(new[]{1,2,3,4},99).ToList();
        var actual=element_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> element_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Append(99).ToList();
    }
}
