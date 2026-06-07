using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class SkipLast {
    [Fact,Trait("Category","Unit")]
    public static void SkipLast_count(){
        GeneratedInterceptorAssert.ContainsMethod<SkipLast>(nameof(SkipLast_countImpl));
        var expected=SystemEnumerable.SkipLast(new[]{1,2,3,4},1).ToList();
        var actual=SkipLast_countImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SkipLast_countImpl(){
        return new[]{1,2,3,4}.SkipLast(1).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void SkipLast_count_enumerable(){
        GeneratedInterceptorAssert.ContainsMethod<SkipLast>(nameof(SkipLast_count_enumerableImpl));
        var expected=SystemEnumerable.SkipLast(new[]{1,2,3,4},1).ToList();
        var actual=SkipLast_count_enumerableImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> SkipLast_count_enumerableImpl(){
        IEnumerable<int>src=new[]{1,2,3,4};
        return src.SkipLast(1).ToList();
    }
}
