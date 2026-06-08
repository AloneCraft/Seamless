using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Skip {
    [Fact,Trait("Category","Unit")]
    public static void count(){
        GeneratedInterceptorAssert.ContainsMethod<Skip>(nameof(countImpl));
        var expected=SystemEnumerable.Skip([1,2,3,4],1).ToList();
        var actual=countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> countImpl(){
        return new[]{1,2,3,4}.Skip(1).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<Skip>(nameof(count_enumerableImpl));
        var expected=SystemEnumerable.Skip([1,2,3,4],1).ToList();
        var actual=count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> count_enumerableImpl(){
        IEnumerable<int>src=[1,2,3,4];
        return src.Skip(1).ToList();
    }
}
