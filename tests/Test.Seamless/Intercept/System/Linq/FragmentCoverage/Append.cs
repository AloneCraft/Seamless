using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Append {
    [Fact,Trait("Category","Unit")]
    public static void Append_element(){
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(Append_elementImpl));
        var expected=SystemEnumerable.Append(new[]{1,2,3,4},99).ToList();
        var actual=Append_elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Append_elementImpl(){
        return new[]{1,2,3,4}.Append(99).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Append_element_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(Append_element_enumerableImpl));
        var expected=SystemEnumerable.Append(new[]{1,2,3,4},99).ToList();
        var actual=Append_element_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Append_element_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Append(99).ToList();
    }
}
