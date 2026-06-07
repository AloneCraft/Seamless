using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Take {
    [Fact,Trait("Category","Unit")]
    public static void Take_count(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(Take_countImpl));
        var expected=SystemEnumerable.Take(new[]{1,2,3,4},2).ToList();
        var actual=Take_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Take_countImpl(){
        return new[]{1,2,3,4}.Take(2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Take_count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Take>(nameof(Take_count_enumerableImpl));
        var expected=SystemEnumerable.Take(new[]{1,2,3,4},2).ToList();
        var actual=Take_count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Take_count_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.Take(2).ToList();
    }
}
