using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class TakeLast {
    [Fact,Trait("Category","Unit")]
    public static void TakeLast_count(){
        GeneratedInterceptorAssert.ContainsMethod<TakeLast>(nameof(TakeLast_countImpl));
        var expected=SystemEnumerable.TakeLast(new[]{1,2,3,4},2).ToList();
        var actual=TakeLast_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> TakeLast_countImpl(){
        return new[]{1,2,3,4}.TakeLast(2).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void TakeLast_count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<TakeLast>(nameof(TakeLast_count_enumerableImpl));
        var expected=SystemEnumerable.TakeLast(new[]{1,2,3,4},2).ToList();
        var actual=TakeLast_count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> TakeLast_count_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.TakeLast(2).ToList();
    }
}
